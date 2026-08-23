using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Characters.Contracts.CombatParticipants;
using DndCampaign.Modules.Combat.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DndCampaign.Modules.Combat.Tests.Component;

[Collection(CombatDatabaseCollection.Name)]
public sealed class CombatApiContractTests
{
    private static readonly Guid CampaignId = Guid.NewGuid();
    private static readonly Guid DmId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid CharacterId = Guid.NewGuid();

    [Fact]
    public async Task Dm_directs_encounter_and_player_projection_omits_private_enemy_fields()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("IDENTITY_TEST_DATABASE is required for Combat HTTP contract tests.");
        }
        await using var factory = CreateFactory(connectionString);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<CombatDbContext>();
            await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await database.Participants.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await database.Encounters.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        using var client = factory.CreateClient();
        SetUser(client, DmId);
        using var createdResponse = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters",
            new { name = "Encuentro de prueba" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var encounter = await ReadAsync(createdResponse);
        var encounterId = encounter.GetProperty("id").GetGuid();
        var version = encounter.GetProperty("version").GetInt64();

        using var characterResponse = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters/{encounterId}/characters",
            new { characterId = CharacterId, initiative = 18, expectedVersion = version },
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(HttpStatusCode.OK, characterResponse);
        encounter = await ReadAsync(characterResponse);
        version = encounter.GetProperty("version").GetInt64();

        using var enemyResponse = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters/{encounterId}/enemies",
            new { name = "Adversarios", initiative = 12, armorClass = 14, maximumHitPoints = 20, quantity = 3, expectedVersion = version },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, enemyResponse.StatusCode);
        encounter = await ReadAsync(enemyResponse);
        version = encounter.GetProperty("version").GetInt64();
        var dmEnemy = encounter.GetProperty("participants").EnumerateArray()
            .Single(item => item.GetProperty("kind").GetString() == "enemy");
        var enemyId = dmEnemy.GetProperty("id").GetGuid();
        var memberId = dmEnemy.GetProperty("members").EnumerateArray().First().GetProperty("id").GetGuid();
        Assert.Equal(3, dmEnemy.GetProperty("quantity").GetInt32());

        using var activatedResponse = await client.PutAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters/{encounterId}/active",
            new { expectedVersion = version },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, activatedResponse.StatusCode);
        encounter = await ReadAsync(activatedResponse);
        version = encounter.GetProperty("version").GetInt64();

        SetUser(client, PlayerId);
        using var activeResponse = await client.GetAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters/active",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        var active = (await ReadAsync(activeResponse)).GetProperty("encounter");
        var enemy = active.GetProperty("participants").EnumerateArray()
            .Single(item => item.GetProperty("kind").GetString() == "enemy");
        Assert.False(enemy.TryGetProperty("armorClass", out _));
        Assert.False(enemy.TryGetProperty("currentHitPoints", out _));
        Assert.False(enemy.TryGetProperty("maximumHitPoints", out _));
        Assert.False(enemy.TryGetProperty("members", out _));
        Assert.Equal(3, enemy.GetProperty("quantity").GetInt32());
        Assert.False(active.TryGetProperty("version", out _));

        using var playerAdvance = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters/{encounterId}/turns/advance",
            new { expectedVersion = version },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, playerAdvance.StatusCode);

        using var playerDelete = await client.DeleteAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters/{encounterId}?expectedVersion={version}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, playerDelete.StatusCode);

        SetUser(client, DmId);
        using var damageResponse = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters/{encounterId}/enemies/{enemyId}/members/{memberId}/hit-points",
            new { kind = "damage", amount = 7, expectedVersion = version },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, damageResponse.StatusCode);
        encounter = await ReadAsync(damageResponse);
        version = encounter.GetProperty("version").GetInt64();
        var damagedEnemy = encounter.GetProperty("participants").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == enemyId)
            .GetProperty("members").EnumerateArray().ToArray();
        Assert.Equal(13, damagedEnemy.Single(item => item.GetProperty("id").GetGuid() == memberId)
            .GetProperty("currentHitPoints").GetInt32());
        Assert.All(damagedEnemy.Where(item => item.GetProperty("id").GetGuid() != memberId),
            item => Assert.Equal(20, item.GetProperty("currentHitPoints").GetInt32()));

        using var finishedResponse = await client.PutAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters/{encounterId}/finished",
            new { expectedVersion = version },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, finishedResponse.StatusCode);

        SetUser(client, PlayerId);
        using var emptyActiveResponse = await client.GetAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters/active",
            TestContext.Current.CancellationToken);
        var empty = await ReadAsync(emptyActiveResponse);
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("encounter").ValueKind);

        SetUser(client, DmId);
        using var deleteResponse = await client.DeleteAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters/{encounterId}?expectedVersion={version + 1}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        using var deletedResponse = await client.GetAsync(
            $"/api/v1/campaigns/{CampaignId}/encounters/{encounterId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, deletedResponse.StatusCode);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == expected,
            $"Expected {(int)expected} {expected}, received {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OTEL_SDK_DISABLED", "true");
            builder.UseSetting("ConnectionStrings:Campaigns", connectionString);
            builder.UseSetting("Database:ApplyMigrations", "false");
            builder.UseSetting("Email:OutboxWorkerEnabled", "false");
            builder.UseSetting("Identity:BootstrapToken", "combat-tests-bootstrap-token-0000000");
            builder.UseSetting(
                "Identity:OutboxEncryptionKey",
                "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=");
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName, _ => { });
                services.RemoveAll<ICampaignAccessReader>();
                services.AddSingleton<ICampaignAccessReader, TestCampaignAccessReader>();
                services.RemoveAll<ICombatCharacterReader>();
                services.AddSingleton<ICombatCharacterReader, TestCharacterReader>();
            });
        });

    private static void SetUser(HttpClient client, Guid userId)
    {
        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.UserHeader);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, userId.ToString());
    }

    private sealed class TestCampaignAccessReader : ICampaignAccessReader
    {
        public Task<CampaignAccess> GetAccessAsync(
            Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
        {
            if (campaignId != CampaignId) return Task.FromResult(new CampaignAccess(false, null));
            if (userId == DmId) return Task.FromResult(new CampaignAccess(true, CampaignRole.Dm));
            return Task.FromResult(new CampaignAccess(
                true, userId == PlayerId ? CampaignRole.Player : null));
        }
    }

    private sealed class TestCharacterReader : ICombatCharacterReader
    {
        public Task<CombatCharacterSnapshot?> GetAsync(
            Guid campaignId, Guid characterId, CancellationToken cancellationToken = default) =>
            Task.FromResult(campaignId == CampaignId && characterId == CharacterId
                ? new CombatCharacterSnapshot(CharacterId, "Exploradora", 16)
                : null);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "CombatTests";
        public const string UserHeader = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var value)
                || !Guid.TryParse(value, out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
