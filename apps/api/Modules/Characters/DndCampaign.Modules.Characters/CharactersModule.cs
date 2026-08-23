using Azure.Identity;
using Azure.Storage.Blobs;
using DndCampaign.Modules.Characters.Api;
using DndCampaign.Modules.Characters.Application.Characters;
using DndCampaign.Modules.Characters.Application.Ports;
using DndCampaign.Modules.Characters.Infrastructure.Observability;
using DndCampaign.Modules.Characters.Infrastructure.Persistence;
using DndCampaign.Modules.Characters.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Metrics;

namespace DndCampaign.Modules.Characters;

public static class CharactersModule
{
    public static IServiceCollection AddCharactersModule(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddControllers()
            .AddApplicationPart(typeof(CharactersModule).Assembly)
            .ConfigureApplicationPartManager(manager =>
            {
                if (manager.FeatureProviders.All(provider => provider is not InternalControllerFeatureProvider))
                {
                    manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
                }
            });
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<CharactersDbContext>(options =>
            options.UseNpgsql(ResolveDatabaseConnectionString(configuration)));
        services.AddScoped<ICharacterRepository, CharacterRepository>();
        services.AddSingleton<ICharacterImageStore, AzureBlobCharacterImageStore>();
        services.AddSingleton(CreateBlobContainerClient(configuration, environment));
        services.AddSingleton<ICharacterMetrics, CharacterMetrics>();
        services.AddScoped<ListCharactersHandler>();
        services.AddScoped<ListCharacterOwnersHandler>();
        services.AddScoped<CreateCharacterHandler>();
        services.AddScoped<UpdateCharacterHandler>();
        services.AddScoped<ActivateCharacterHandler>();
        services.AddScoped<DeleteCharacterHandler>();
        services.AddScoped<GetCharacterImageHandler>();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(CharacterMetrics.MeterName));
        return services;
    }

    public static IEndpointRouteBuilder MapCharactersModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        return endpoints;
    }

    public static async Task ApplyCharactersMigrationsAsync(
        this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<CharactersDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }

    private static BlobContainerClient CreateBlobContainerClient(
        IConfiguration configuration, IHostEnvironment environment)
    {
        var containerName = configuration["Storage:Characters:Container"] ?? "character-images";
        var clientOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2025_11_05);
        var connectionString = configuration["Storage:Characters:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new BlobContainerClient(connectionString, containerName, clientOptions);
        }

        var serviceUri = configuration["Storage:Characters:ServiceUri"];
        if (!string.IsNullOrWhiteSpace(serviceUri))
        {
            return new BlobContainerClient(
                new Uri(new Uri(serviceUri.TrimEnd('/') + "/"), containerName),
                new DefaultAzureCredential(),
                clientOptions);
        }

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            return new BlobContainerClient("UseDevelopmentStorage=true", containerName, clientOptions);
        }

        throw new InvalidOperationException(
            "Character image storage requires Storage:Characters:ServiceUri or ConnectionString.");
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
