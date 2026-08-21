using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DndCampaign.Api.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class IdentityStoreTests
{
    [Fact]
    public async Task PersistLoginAsync_with_rehash_updates_only_password_hash_and_inserts_session()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var now = DateTimeOffset.UtcNow;

            var user = UserAccount.Create("player@example.com", "Player One", isPlatformAdmin: false, now);
            user.SetPasswordHash("original-hash-value");
            await store.AddUserAsync(user, cancellationToken);

            var originalEmail = user.Email;
            var originalDisplayName = user.DisplayName;
            var issued = UserSession.Issue(user.Id, now);
            await store.PersistLoginAsync(user.Id, "rehashed-password-value", issued.Session, cancellationToken);

            var persistedUser = await database.Users.AsNoTracking().SingleAsync(cancellationToken);
            Assert.Equal("rehashed-password-value", persistedUser.PasswordHash);
            Assert.Equal(originalEmail, persistedUser.Email);
            Assert.Equal(originalDisplayName, persistedUser.DisplayName);
            Assert.False(persistedUser.IsPlatformAdmin);

            var session = await database.UserSessions.AsNoTracking().SingleAsync(cancellationToken);
            Assert.Equal(user.Id, session.UserId);
            Assert.Equal(issued.Session.TokenHash, session.TokenHash);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task PersistLoginAsync_without_rehash_inserts_only_the_session()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var now = DateTimeOffset.UtcNow;

            var user = UserAccount.Create("player@example.com", "Player One", isPlatformAdmin: false, now);
            user.SetPasswordHash("original-hash-value");
            await store.AddUserAsync(user, cancellationToken);

            var issued = UserSession.Issue(user.Id, now);
            await store.PersistLoginAsync(user.Id, rehashedPasswordHash: null, issued.Session, cancellationToken);

            var persistedUser = await database.Users.AsNoTracking().SingleAsync(cancellationToken);
            Assert.Equal("original-hash-value", persistedUser.PasswordHash);

            Assert.Equal(1, await database.UserSessions.CountAsync(cancellationToken));
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task AddSessionAsync_does_not_persist_unrelated_user_account_changes()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var now = DateTimeOffset.UtcNow;

            var user = UserAccount.Create("player@example.com", "Player One", isPlatformAdmin: false, now);
            user.SetPasswordHash("original-hash-value");
            await store.AddUserAsync(user, cancellationToken);

            var trackedUser = await database.Users.SingleAsync(cancellationToken);
            database.Entry(trackedUser).Property(candidate => candidate.Email).CurrentValue = "hacked@example.com";
            var issued = UserSession.Issue(user.Id, now);
            await store.AddSessionAsync(issued.Session, cancellationToken);

            await using var assertScope = provider.CreateAsyncScope();
            var persisted = assertScope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var persistedUser = await persisted.Users.SingleAsync(cancellationToken);
            Assert.Equal("player@example.com", persistedUser.Email);
            Assert.Equal(1, await persisted.UserSessions.CountAsync(cancellationToken));
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task SaveSessionAsync_persists_the_received_session_without_unrelated_user_changes()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var now = DateTimeOffset.UtcNow;

            var user = UserAccount.Create("player@example.com", "Player One", isPlatformAdmin: false, now);
            user.SetPasswordHash("original-hash-value");
            await store.AddUserAsync(user, cancellationToken);
            var issued = UserSession.Issue(user.Id, now);
            await store.AddSessionAsync(issued.Session, cancellationToken);

            var trackedUser = await database.Users.SingleAsync(cancellationToken);
            database.Entry(trackedUser).Property(candidate => candidate.Email).CurrentValue = "hacked@example.com";

            var loaded = await store.FindSessionByIdAsync(issued.Session.Id, cancellationToken);
            Assert.NotNull(loaded);
            loaded.Revoke(now);
            await store.SaveSessionAsync(loaded, cancellationToken);

            await using var assertScope = provider.CreateAsyncScope();
            var persisted = assertScope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var persistedUser = await persisted.Users.SingleAsync(cancellationToken);
            Assert.Equal("player@example.com", persistedUser.Email);
            var persistedSession = await persisted.UserSessions.SingleAsync(cancellationToken);
            Assert.NotNull(persistedSession.RevokedAt);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task PersistLoginAsync_does_not_persist_unrelated_user_account_mutations()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var now = DateTimeOffset.UtcNow;

            var user = UserAccount.Create("player@example.com", "Player One", isPlatformAdmin: false, now);
            user.SetPasswordHash("original-hash-value");
            await store.AddUserAsync(user, cancellationToken);

            var trackedUser = await database.Users.SingleAsync(cancellationToken);
            database.Entry(trackedUser).Property(candidate => candidate.Email).CurrentValue = "hacked@example.com";
            database.Entry(trackedUser).Property(candidate => candidate.DisplayName).CurrentValue = "Hacked";
            var issued = UserSession.Issue(user.Id, now);
            await store.PersistLoginAsync(user.Id, "rehashed-password-value", issued.Session, cancellationToken);

            await using var assertScope = provider.CreateAsyncScope();
            var persisted = assertScope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var persistedUser = await persisted.Users.SingleAsync(cancellationToken);
            Assert.Equal("rehashed-password-value", persistedUser.PasswordHash);
            Assert.Equal("player@example.com", persistedUser.Email);
            Assert.Equal("Player One", persistedUser.DisplayName);
            Assert.Equal(1, await persisted.UserSessions.CountAsync(cancellationToken));
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task HasAnyUsers_find_by_email_and_membership_round_trip()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
            var now = DateTimeOffset.UtcNow;
            var campaignId = Guid.NewGuid();

            Assert.False(await store.HasAnyUsersAsync(cancellationToken));

            var user = UserAccount.Create("dm@example.com", "Dungeon Master", isPlatformAdmin: true, now);
            user.SetPasswordHash("hash");
            await store.AddUserAsync(user, cancellationToken);

            Assert.True(await store.HasAnyUsersAsync(cancellationToken));
            var found = await store.FindByEmailAsync(user.Email, cancellationToken);
            Assert.NotNull(found);
            Assert.Equal(user.Id, found.Id);

            await store.AddMembershipAsync(CampaignMembership.CreateDm(campaignId, user.Id, now), cancellationToken);
            Assert.True(await store.IsCampaignDmAsync(campaignId, user.Id, cancellationToken));
            Assert.True(await store.IsCampaignMemberAsync(campaignId, user.Id, cancellationToken));
            Assert.False(await store.IsCampaignDmAsync(Guid.NewGuid(), user.Id, cancellationToken));
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
        return services.BuildServiceProvider();
    }
}
