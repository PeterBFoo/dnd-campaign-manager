using Azure.Identity;
using Azure.Storage.Blobs;
using DndCampaign.Modules.AdventureCatalog.Api;
using DndCampaign.Modules.AdventureCatalog.Application.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Application.Chapters;
using DndCampaign.Modules.AdventureCatalog.Application.Maps;
using DndCampaign.Modules.AdventureCatalog.Contracts.Campaigns;
using DndCampaign.Modules.AdventureCatalog.Infrastructure.Observability;
using DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence;
using DndCampaign.Modules.AdventureCatalog.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Metrics;

namespace DndCampaign.Modules.AdventureCatalog;

public static class AdventureCatalogModule
{
    public static IServiceCollection AddAdventureCatalogModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddControllers()
            .AddApplicationPart(typeof(AdventureCatalogModule).Assembly)
            .ConfigureApplicationPartManager(manager =>
            {
                if (manager.FeatureProviders.All(provider => provider is not InternalControllerFeatureProvider))
                {
                    manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
                }
            });
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<AdventureCatalogDbContext>(options =>
            options.UseNpgsql(ResolveDatabaseConnectionString(configuration)));
        services.AddScoped<IAdventureModuleRepository, AdventureModuleRepository>();
        services.AddScoped<IAdventureChapterRepository, AdventureChapterRepository>();
        services.AddScoped<AdventureChapterService>();
        services.AddScoped<IAdventureMapRepository, AdventureMapRepository>();
        services.AddScoped<IAdventureModuleCampaignReader, AdventureModuleCampaignReader>();
        services.AddSingleton(new AdventureCatalogBlobContainer(
            CreateBlobContainerClient(configuration, environment)));
        services.AddSingleton<IAdventureModuleCoverStore, AzureBlobAdventureModuleCoverStore>();
        services.AddSingleton<IAdventureMapImageStore, AzureBlobAdventureMapImageStore>();
        services.AddSingleton<IAdventureCatalogMetrics, AdventureCatalogMetrics>();
        services.AddScoped<ListAdventureModulesHandler>();
        services.AddScoped<GetAdventureModuleHandler>();
        services.AddScoped<CreateAdventureModuleHandler>();
        services.AddScoped<UpdateAdventureModuleHandler>();
        services.AddScoped<DeleteAdventureModuleHandler>();
        services.AddScoped<GetAdventureModuleCoverHandler>();
        services.AddScoped<AdventureMapService>();
        services.AddOpenTelemetry().WithMetrics(metrics =>
            metrics.AddMeter(AdventureCatalogMetrics.MeterName));
        return services;
    }

    public static IEndpointRouteBuilder MapAdventureCatalogModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        return endpoints;
    }

    public static async Task ApplyAdventureCatalogMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AdventureCatalogDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }

    private static BlobContainerClient CreateBlobContainerClient(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        const string defaultContainerName = "adventure-module-images";
        var containerName = configuration["Storage:AdventureCatalog:Container"] ?? defaultContainerName;
        var options = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2025_11_05);
        var connectionString = configuration["Storage:AdventureCatalog:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new BlobContainerClient(connectionString, containerName, options);
        }

        var serviceUri = configuration["Storage:AdventureCatalog:ServiceUri"];
        if (!string.IsNullOrWhiteSpace(serviceUri))
        {
            return new BlobContainerClient(
                new Uri(new Uri(serviceUri.TrimEnd('/') + "/"), containerName),
                new DefaultAzureCredential(),
                options);
        }

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            return new BlobContainerClient("UseDevelopmentStorage=true", containerName, options);
        }

        throw new InvalidOperationException(
            "Adventure catalog storage requires Storage:AdventureCatalog:ServiceUri or ConnectionString.");
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
                throw new InvalidOperationException("The PostgreSQL URI must include credentials.");
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
