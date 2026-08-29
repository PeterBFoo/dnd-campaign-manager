using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.Campaigns.Api;
using DndCampaign.Modules.Campaigns.Application.Campaigns;
using DndCampaign.Modules.Campaigns.Application.Ports;
using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Campaigns.Infrastructure.Access;
using DndCampaign.Modules.Campaigns.Infrastructure.Observability;
using DndCampaign.Modules.Campaigns.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Metrics;

namespace DndCampaign.Modules.Campaigns;

public static class CampaignsModule
{
    public static IServiceCollection AddCampaignsModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddControllers()
            .AddApplicationPart(typeof(CampaignsModule).Assembly)
            .ConfigureApplicationPartManager(manager =>
            {
                if (manager.FeatureProviders.All(provider => provider is not InternalControllerFeatureProvider))
                {
                    manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
                }
            });
        services.AddDbContext<CampaignsDbContext>(options =>
            options.UseNpgsql(ResolveDatabaseConnectionString(configuration)));
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddSingleton<ICampaignMetrics, CampaignMetrics>();
        services.AddScoped<ICampaignInvitationContext, CampaignInvitationContext>();
        services.AddScoped<ICampaignAccessReader, CampaignAccessReader>();
        services.AddScoped<CreateCampaignHandler>();
        services.AddScoped<ListCampaignsHandler>();
        services.AddScoped<GetCampaignHandler>();
        services.AddScoped<DeleteCampaignHandler>();
        services.AddScoped<AssignAdventureModuleHandler>();
        services.AddScoped<RemoveAdventureModuleHandler>();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(CampaignMetrics.MeterName));
        return services;
    }

    public static IEndpointRouteBuilder MapCampaignsModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        return endpoints;
    }

    public static async Task ApplyCampaignsMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
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
            if (separator <= 0)
            {
                throw new InvalidOperationException("The PostgreSQL URI must include username and password.");
            }

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
        return !string.IsNullOrWhiteSpace(file)
            ? File.ReadAllText(file).Trim()
            : GetRequired(configuration, key);
    }

    private static string GetRequired(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing required configuration value '{key}'.");
}
