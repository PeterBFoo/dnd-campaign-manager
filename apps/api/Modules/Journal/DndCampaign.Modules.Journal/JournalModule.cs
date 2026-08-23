using DndCampaign.Modules.Journal.Api;
using DndCampaign.Modules.Journal.Application.Entries;
using DndCampaign.Modules.Journal.Application.Ports;
using DndCampaign.Modules.Journal.Infrastructure.Observability;
using DndCampaign.Modules.Journal.Infrastructure.Pagination;
using DndCampaign.Modules.Journal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Metrics;

namespace DndCampaign.Modules.Journal;

public static class JournalModule
{
    public static IServiceCollection AddJournalModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddControllers()
            .AddApplicationPart(typeof(JournalModule).Assembly)
            .ConfigureApplicationPartManager(manager =>
            {
                if (manager.FeatureProviders.All(provider => provider is not InternalControllerFeatureProvider))
                {
                    manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
                }
            });
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<JournalDbContext>(options =>
            options.UseNpgsql(ResolveDatabaseConnectionString(configuration)));
        services.AddScoped<IJournalEntryRepository, JournalRepository>();
        services.AddSingleton<IJournalCursorCodec, JournalCursorCodec>();
        services.AddSingleton<IJournalMetrics, JournalMetrics>();
        services.AddScoped<ListJournalEntriesHandler>();
        services.AddScoped<CreateJournalEntryHandler>();
        services.AddScoped<UpdateJournalEntryHandler>();
        services.AddScoped<DeleteJournalEntryHandler>();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(JournalMetrics.MeterName));
        return services;
    }

    public static IEndpointRouteBuilder MapJournalModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        return endpoints;
    }

    public static async Task ApplyJournalMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<JournalDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }

    private static string ResolveDatabaseConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("Campaigns");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!Uri.TryCreate(configured, UriKind.Absolute, out var databaseUri)
                || (databaseUri.Scheme != "postgres" && databaseUri.Scheme != "postgresql"))
            {
                return configured;
            }

            var separator = databaseUri.UserInfo.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0) throw new InvalidOperationException("The PostgreSQL URI must include credentials.");
            return new NpgsqlConnectionStringBuilder
            {
                Host = databaseUri.Host,
                Port = databaseUri.IsDefaultPort ? 5432 : databaseUri.Port,
                Database = Uri.UnescapeDataString(databaseUri.AbsolutePath.TrimStart('/')),
                Username = Uri.UnescapeDataString(databaseUri.UserInfo[..separator]),
                Password = Uri.UnescapeDataString(databaseUri.UserInfo[(separator + 1)..]),
            }.ConnectionString;
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = GetRequired(configuration, "Database:Host"),
            Port = configuration.GetValue("Database:Port", 5432),
            Database = GetRequired(configuration, "Database:Name"),
            Username = GetRequired(configuration, "Database:User"),
            Password = ReadRequiredSecret(configuration, "Database:Password"),
        }.ConnectionString;
    }

    private static string ReadRequiredSecret(IConfiguration configuration, string key)
    {
        var file = configuration[$"{key}_FILE"];
        return !string.IsNullOrWhiteSpace(file) ? File.ReadAllText(file).Trim() : GetRequired(configuration, key);
    }

    private static string GetRequired(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value : throw new InvalidOperationException($"Missing required configuration value '{key}'.");
}
