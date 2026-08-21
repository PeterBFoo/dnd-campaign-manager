using System.Diagnostics;
using System.Globalization;
using System.Threading.RateLimiting;
using System.Text.Json;
using DndCampaign.Api.Api.Middleware;
using DndCampaign.Api.Composition;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Infrastructure.Background;
using DndCampaign.Api.Infrastructure.Identity;
using DndCampaign.Api.Infrastructure.Observability;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string serviceName = "dnd-campaign-api";

Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;

var builder = WebApplication.CreateBuilder(args);
var connectionString = DatabaseConnectionString.Resolve(builder.Configuration);
var identitySecurity = IdentitySecurityOptionsFactory.FromConfiguration(builder.Configuration, builder.Environment);
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddPersistence(connectionString);
builder.Services.AddApplication(identitySecurity);
builder.Services.AddEmail(builder.Configuration);
if (builder.Configuration.GetValue("Email:OutboxWorkerEnabled", false))
{
    builder.Services.AddHostedService<InvitationOutboxWorker>();
}
builder.Services
    .AddAuthentication(SessionAuthenticationHandler.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationHandler.AuthenticationScheme,
        _ => { });
builder.Services.AddAuthorization(options => options.AddPolicy(
    "platform-admin",
    policy => policy.RequireClaim("platform_admin", "true")));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(
                1,
                (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };
    options.AddPolicy("bootstrap", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        }));
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("invitation-acceptance", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .WithMethods("GET", "POST", "DELETE")
        .WithHeaders("Accept", "Content-Type", "Authorization")
        .WithExposedHeaders("X-Correlation-Id")));
}

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: serviceName,
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
            options.RecordException = true;
        })
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(ApiTelemetry.MeterName)
        .AddMeter("DndCampaign.Api.Email")
        .AddMeter(IdentityTelemetry.MeterName)
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.AddOtlpExporter();
});

var app = builder.Build();


app.UseForwardedHeaders();
app.UseExceptionHandler();
if (allowedOrigins.Length > 0)
{
    app.UseCors("frontend");
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Use(async (context, next) =>
{
    var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.TryAdd("X-Correlation-Id", correlationId);
        return Task.CompletedTask;
    });
    await next();
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponseAsync,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync,
});

app.MapGet("/api/v1/platform/status", async (
    HealthCheckService healthChecks,
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    ApiTelemetry.PlatformStatusRequests.Add(1);
    var report = await healthChecks.CheckHealthAsync(
        registration => registration.Name == "postgres",
        cancellationToken);
    var databaseHealthy = report.Entries.TryGetValue("postgres", out var postgres)
        && postgres.Status == HealthStatus.Healthy;
    var status = databaseHealthy ? "operational" : "degraded";

    return Results.Ok(new
    {
        service = serviceName,
        status,
        environment = environment.EnvironmentName,
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0",
        generatedAt = DateTimeOffset.UtcNow,
        dependencies = new
        {
            database = databaseHealthy ? "connected" : "unavailable",
            telemetry = "otlp",
        },
    });
});

if (builder.Configuration.GetValue("Database:ApplyMigrations", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
    await database.Database.MigrateAsync();
}

app.Run();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        status = report.Status.ToString().ToLowerInvariant(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString().ToLowerInvariant(),
        }),
    }));
}

public partial class Program;
