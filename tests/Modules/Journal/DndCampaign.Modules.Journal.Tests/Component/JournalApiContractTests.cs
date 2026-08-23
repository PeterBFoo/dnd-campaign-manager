using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Characters.Contracts.ActiveCharacters;
using DndCampaign.Modules.Journal.Infrastructure.Persistence;
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

namespace DndCampaign.Modules.Journal.Tests.Component;

public sealed class JournalApiContractTests
{
    private static readonly Guid CampaignId = Guid.NewGuid();
    private static readonly Guid DmId = Guid.NewGuid();
    private static readonly Guid FirstPlayerId = Guid.NewGuid();
    private static readonly Guid SecondPlayerId = Guid.NewGuid();
    private static readonly Guid PlayerWithoutCharacterId = Guid.NewGuid();

    [Fact]
    public async Task Journal_contract_enforces_collaborative_editing_original_authorship_and_creator_deletion()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("IDENTITY_TEST_DATABASE is required for Journal HTTP contract tests.");
        }

        await using var factory = CreateFactory(connectionString);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<JournalDbContext>();
            await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await database.Entries.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        using var client = factory.CreateClient();
        SetUser(client, FirstPlayerId);
        using var createdResponse = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/journal/entries",
            new { content = "Pista original" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var entryId = created.GetProperty("id").GetGuid();
        Assert.Equal("Personaje inicial", created.GetProperty("authorCharacterName").GetString());
        Assert.True(created.GetProperty("canDelete").GetBoolean());
        Assert.False(created.TryGetProperty("createdByUserId", out _));

        SetUser(client, SecondPlayerId);
        using var updatedResponse = await client.PutAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/journal/entries/{entryId}",
            new { content = "Pista editada por otro jugador" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Personaje inicial", updated.GetProperty("authorCharacterName").GetString());
        Assert.True(updated.GetProperty("canEdit").GetBoolean());
        Assert.False(updated.GetProperty("canDelete").GetBoolean());

        using var forbiddenDelete = await client.DeleteAsync(
            $"/api/v1/campaigns/{CampaignId}/journal/entries/{entryId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        SetUser(client, DmId);
        using var listResponse = await client.GetAsync(
            $"/api/v1/campaigns/{CampaignId}/journal/entries?limit=20",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var dmEntry = page.GetProperty("items")[0];
        Assert.False(dmEntry.GetProperty("canEdit").GetBoolean());
        Assert.False(dmEntry.GetProperty("canDelete").GetBoolean());

        using var dmWrite = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/journal/entries",
            new { content = "No autorizada" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, dmWrite.StatusCode);

        SetUser(client, PlayerWithoutCharacterId);
        using var withoutCharacter = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/journal/entries",
            new { content = "Sin personaje" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, withoutCharacter.StatusCode);

        SetUser(client, FirstPlayerId);
        using var deleted = await client.DeleteAsync(
            $"/api/v1/campaigns/{CampaignId}/journal/entries/{entryId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OTEL_SDK_DISABLED", "true");
            builder.UseSetting("ConnectionStrings:Campaigns", connectionString);
            builder.UseSetting("Database:ApplyMigrations", "false");
            builder.UseSetting("Email:OutboxWorkerEnabled", "false");
            builder.UseSetting("Identity:BootstrapToken", "journal-tests-bootstrap-token-00000000");
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
                services.RemoveAll<IActiveCharacterReader>();
                services.AddSingleton<IActiveCharacterReader, TestActiveCharacterReader>();
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
            Guid campaignId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (campaignId != CampaignId) return Task.FromResult(new CampaignAccess(false, null));
            if (userId == DmId) return Task.FromResult(new CampaignAccess(true, CampaignRole.Dm));
            var isPlayer = userId == FirstPlayerId || userId == SecondPlayerId || userId == PlayerWithoutCharacterId;
            return Task.FromResult(new CampaignAccess(true, isPlayer ? CampaignRole.Player : null));
        }
    }

    private sealed class TestActiveCharacterReader : IActiveCharacterReader
    {
        public Task<ActiveCharacterSnapshot?> GetActiveAsync(
            Guid campaignId,
            Guid userId,
            CancellationToken cancellationToken = default) => Task.FromResult(
                campaignId == CampaignId && userId == FirstPlayerId
                    ? new ActiveCharacterSnapshot(Guid.NewGuid(), "Personaje inicial")
                    : campaignId == CampaignId && userId == SecondPlayerId
                        ? new ActiveCharacterSnapshot(Guid.NewGuid(), "Segundo personaje")
                        : null);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "JournalTests";
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
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
