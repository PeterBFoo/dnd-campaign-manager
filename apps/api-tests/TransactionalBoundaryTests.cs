using DndCampaign.Api.Application;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DndCampaign.Api.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class TransactionalBoundaryTests
{
    [Fact]
    public async Task ExecuteSerializableAsync_commits_two_writes_in_the_same_scope()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
            var boundary = scope.ServiceProvider.GetRequiredService<ITransactionalBoundary>();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var now = DateTimeOffset.UtcNow;

            var user = UserAccount.Create("player@example.com", "Player One", isPlatformAdmin: false, now);
            user.SetPasswordHash("hash");
            var issued = UserSession.Issue(user.Id, now);

            await boundary.ExecuteSerializableAsync(async ct =>
            {
                await store.AddUserAsync(user, ct);
                await store.AddSessionAsync(issued.Session, ct);
            }, cancellationToken);

            Assert.Equal(1, await database.Users.CountAsync(cancellationToken));
            Assert.Equal(1, await database.UserSessions.CountAsync(cancellationToken));
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task ExecuteSerializableAsync_rolls_back_when_the_action_throws()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
            var boundary = scope.ServiceProvider.GetRequiredService<ITransactionalBoundary>();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var now = DateTimeOffset.UtcNow;

            var user = UserAccount.Create("player@example.com", "Player One", isPlatformAdmin: false, now);
            user.SetPasswordHash("hash");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                boundary.ExecuteSerializableAsync(async ct =>
                {
                    await store.AddUserAsync(user, ct);
                    throw new InvalidOperationException("forced rollback");
                }, cancellationToken));

            Assert.Equal(0, await database.Users.CountAsync(cancellationToken));
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    private static ServiceProvider BuildServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContext<CampaignDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IIdentityStore, IdentityStore>();
        services.AddScoped<ITransactionalBoundary, SerializableTransactionalBoundary>();
        return services.BuildServiceProvider();
    }
}
