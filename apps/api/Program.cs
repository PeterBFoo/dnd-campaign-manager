using System.Diagnostics;
using System.Text.Json;
using DndCampaign.Api.Infrastructure.Observability;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<CampaignDbContext>(options => options.UseNpgsql(connectionString));
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
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.AddOtlpExporter();
});

var app = builder.Build();

app.UseExceptionHandler();
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

app.Run();

static string ResolveDatabaseConnectionString(IConfiguration configuration)
{
    var configuredConnectionString = configuration.GetConnectionString("Campaigns");
    if (!string.IsNullOrWhiteSpace(configuredConnectionString))
    {
        return configuredConnectionString;
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
