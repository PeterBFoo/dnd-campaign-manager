using System.Diagnostics;
using System.Globalization;
using System.Threading.RateLimiting;
using System.Text.Json;
using DndCampaign.Api.Api;
using DndCampaign.Api.Application.Email;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Infrastructure.Email;
using DndCampaign.Api.Infrastructure.Identity;
using DndCampaign.Api.Infrastructure.Observability;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string serviceName = "dnd-campaign-api";

Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;

var builder = WebApplication.CreateBuilder(args);
var connectionString = ResolveDatabaseConnectionString(builder.Configuration);
var identitySecurity = IdentitySecurityOptions.FromConfiguration(builder.Configuration, builder.Environment);
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddDbContext<CampaignDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(identitySecurity);
builder.Services.AddSingleton<InvitationTokenProtector>();
builder.Services.AddSingleton<InvitationEmailComposer>();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddScoped<IdentityService>();
builder.Services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
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
builder.Services.Configure<BrevoOptions>(builder.Configuration.GetSection(BrevoOptions.SectionName));
builder.Services.AddHttpClient<ITransactionalEmailSender, BrevoEmailSender>(client =>
{
    client.BaseAddress = new Uri("https://api.brevo.com/v3/");
    client.Timeout = TimeSpan.FromSeconds(15);
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
    .AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

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
    CampaignDbContext database,
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    ApiTelemetry.PlatformStatusRequests.Add(1);
    var databaseAvailable = await CanConnectAsync(database, cancellationToken);
    var status = databaseAvailable ? "operational" : "degraded";

    return Results.Ok(new
    {
        service = serviceName,
        status,
        environment = environment.EnvironmentName,
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0",
        generatedAt = DateTimeOffset.UtcNow,
        dependencies = new
        {
            database = databaseAvailable ? "connected" : "unavailable",
            telemetry = "otlp",
        },
    });
});

app.MapIdentityInvitationEndpoints();

if (builder.Configuration.GetValue("Database:ApplyMigrations", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
    await database.Database.MigrateAsync();
}

app.Run();

static string ResolveDatabaseConnectionString(IConfiguration configuration)
{
    var configuredConnectionString = configuration.GetConnectionString("Campaigns");
    if (!string.IsNullOrWhiteSpace(configuredConnectionString))
    {
        return NormalizeDatabaseConnectionString(configuredConnectionString);
    }

    return new NpgsqlConnectionStringBuilder
    {
        Host = GetRequiredConfiguration(configuration, "Database:Host"),
        Port = configuration.GetValue("Database:Port", 5432),
        Database = GetRequiredConfiguration(configuration, "Database:Name"),
        Username = GetRequiredConfiguration(configuration, "Database:User"),
        Password = ReadRequiredSecret(configuration, "Database:Password"),
    }.ConnectionString;
}

static string NormalizeDatabaseConnectionString(string connectionString)
{
    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var databaseUri)
        || (databaseUri.Scheme != "postgres" && databaseUri.Scheme != "postgresql"))
    {
        return connectionString;
    }

    var userInfoSeparator = databaseUri.UserInfo.IndexOf(':', StringComparison.Ordinal);
    if (userInfoSeparator <= 0)
    {
        throw new InvalidOperationException("The PostgreSQL URI must include username and password.");
    }

    var databaseName = Uri.UnescapeDataString(databaseUri.AbsolutePath.TrimStart('/'));
    if (string.IsNullOrWhiteSpace(databaseName))
    {
        throw new InvalidOperationException("The PostgreSQL URI must include a database name.");
    }

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = databaseUri.Host,
        Port = databaseUri.IsDefaultPort ? 5432 : databaseUri.Port,
        Database = databaseName,
        Username = Uri.UnescapeDataString(databaseUri.UserInfo[..userInfoSeparator]),
        Password = Uri.UnescapeDataString(databaseUri.UserInfo[(userInfoSeparator + 1)..]),
    };

    foreach (var queryParameter in databaseUri.Query.TrimStart('?').Split(
        '&',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var keyValue = queryParameter.Split('=', 2);
        if (keyValue.Length == 2
            && keyValue[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<SslMode>(Uri.UnescapeDataString(keyValue[1]), true, out var sslMode))
        {
            builder.SslMode = sslMode;
        }
    }

    return builder.ConnectionString;
}

static string GetRequiredConfiguration(IConfiguration configuration, string key)
{
    var value = configuration[key];
    return !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidOperationException($"Configuration '{key}' is required.");
}

static string ReadRequiredSecret(IConfiguration configuration, string key)
{
    var secretFile = configuration[$"{key}_FILE"];
    if (!string.IsNullOrWhiteSpace(secretFile))
    {
        try
        {
            var secret = File.ReadAllText(secretFile).Trim();
            return !string.IsNullOrWhiteSpace(secret)
                ? secret
                : throw new InvalidOperationException($"Secret file configured by '{key}_FILE' is empty.");
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                $"Secret file configured by '{key}_FILE' could not be read.",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InvalidOperationException(
                $"Secret file configured by '{key}_FILE' is not readable by the application user.",
                exception);
        }
    }

    return GetRequiredConfiguration(configuration, key);
}

static async Task<bool> CanConnectAsync(
    CampaignDbContext database,
    CancellationToken cancellationToken)
{
    try
    {
        return await database.Database.CanConnectAsync(cancellationToken);
    }
    catch
    {
        return false;
    }
}

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
