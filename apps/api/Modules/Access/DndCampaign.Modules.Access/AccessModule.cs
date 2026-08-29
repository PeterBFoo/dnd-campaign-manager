using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using DndCampaign.Modules.Access.Api;
using DndCampaign.Modules.Access.Infrastructure;
using DndCampaign.Modules.Access.Infrastructure.Observability;
using DndCampaign.Modules.Access.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Metrics;
using DndCampaign.Modules.Access.Infrastructure.Events;

namespace DndCampaign.Modules.Access;

/// <summary>Único punto público de composición del módulo Access.</summary>
public static class AccessModule
{
    public static IServiceCollection AddAccessModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.TryAddSingleton(TimeProvider.System);
        services.AddAccessApi();
        services.AddAccessInfrastructure(
            configuration,
            environment,
            ResolveDatabaseConnectionString(configuration));
        services.AddAuthorization(options => options.AddPolicy(
            "platform-admin",
            policy => policy.RequireClaim("platform_admin", "true")));
        services.AddAuthorization(options => options.AddPolicy(
            "event-grid-delivery",
            policy => policy.AddAuthenticationSchemes("EventGrid")
                .RequireAuthenticatedUser()
                .RequireClaim("roles", configuration["EventGrid:DeliveryRole"] ?? "AzureEventGridSecureWebhookSubscriber")));
        services.AddRateLimiter(ConfigureRateLimits);
        services.AddHealthChecks()
            .AddCheck<AccessPostgresHealthCheck>("postgres", tags: ["ready"]);
        services.AddOpenTelemetry().WithMetrics(metrics => metrics
            .AddMeter(AccessMetrics.MeterName)
            .AddMeter("DndCampaign.Api.Email"));
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(EventBrokerMetrics.MeterName));
        return services;
    }

    public static IEndpointRouteBuilder MapAccessModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        return endpoints;
    }

    public static async Task ApplyAccessMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }

    private static void ConfigureRateLimits(RateLimiterOptions options)
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
        AddFixedWindowPolicy(options, "bootstrap", permitLimit: 10, TimeSpan.FromMinutes(5));
        AddFixedWindowPolicy(options, "login", permitLimit: 10, TimeSpan.FromMinutes(1));
        AddFixedWindowPolicy(options, "invitation-acceptance", permitLimit: 20, TimeSpan.FromMinutes(1));
        options.AddPolicy("eligible-users", context =>
        {
            var actor = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous";
            var campaign = context.Request.RouteValues["campaignId"]?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                $"{actor}:{campaign}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
        });
    }

    private static void AddFixedWindowPolicy(
        RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window) =>
        options.AddPolicy(policyName, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
            }));

    private static string ResolveDatabaseConnectionString(IConfiguration configuration)
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

    private static string NormalizeDatabaseConnectionString(string connectionString)
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
                && keyValue[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                builder.SslMode = Enum.Parse<SslMode>(
                    Uri.UnescapeDataString(keyValue[1]),
                    ignoreCase: true);
            }
        }

        return builder.ConnectionString;
    }

    private static string ReadRequiredSecret(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        var filePath = configuration[$"{key}_FILE"];
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            try
            {
                value = File.ReadAllText(filePath).Trim();
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

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Missing required configuration value '{key}' or '{key}_FILE'.")
            : value;
    }

    private static string GetRequiredConfiguration(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing required configuration value '{key}'.");
}
