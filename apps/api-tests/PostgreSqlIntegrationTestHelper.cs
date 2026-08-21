using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DndCampaign.Api.Tests;

/// <summary>
/// Groups every suite that recreates the shared PostgreSQL database so they never run concurrently.
/// </summary>
[CollectionDefinition(PostgreSqlIntegrationCollection.Name, DisableParallelization = true)]
public sealed class PostgreSqlIntegrationCollection
{
    internal const string Name = "postgresql-integration";
}

internal static class PostgreSqlIntegrationTestHelper
{
    internal static string? RequireTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DATABASE");
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(connectionString),
            "IDENTITY_TEST_DATABASE is required for PostgreSQL integration tests.");
        Assert.Contains("_test", connectionString, StringComparison.OrdinalIgnoreCase);
        return connectionString;
    }

    internal static async Task ResetDatabaseAsync(
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
        await database.Database.EnsureDeletedAsync(cancellationToken);
        await database.Database.MigrateAsync(cancellationToken);
    }
}
