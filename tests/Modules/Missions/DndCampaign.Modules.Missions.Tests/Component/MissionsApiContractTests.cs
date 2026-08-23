using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Characters.Contracts.ActiveCharacters;
using DndCampaign.Modules.Missions.Infrastructure.Persistence;
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

namespace DndCampaign.Modules.Missions.Tests.Component;

public sealed class MissionsApiContractTests
{
    private static readonly Guid CampaignId = Guid.NewGuid();
    private static readonly Guid DmId = Guid.NewGuid();
    private static readonly Guid FirstPlayerId = Guid.NewGuid();
    private static readonly Guid SecondPlayerId = Guid.NewGuid();
    private static readonly Guid PlayerWithoutCharacterId = Guid.NewGuid();

    [Fact]
    public async Task Contract_has_no_functional_dates_and_enforces_authorship_main_and_deletion()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("IDENTITY_TEST_DATABASE is required for Missions HTTP contract tests.");
        }

        await using var factory = CreateFactory(connectionString);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<MissionsDbContext>();
            await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await database.Missions.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        using var client = factory.CreateClient();
        SetUser(client, FirstPlayerId);
        using var createdResponse = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/missions",
            new { title = "Primera misión", description = "Descripción", isMain = true },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var firstMissionId = created.GetProperty("id").GetGuid();
        Assert.Equal("Personaje inicial", created.GetProperty("authorDisplayName").GetString());
        Assert.True(created.GetProperty("isMain").GetBoolean());
        Assert.True(created.GetProperty("canDelete").GetBoolean());
        Assert.False(created.TryGetProperty("createdByUserId", out _));
        Assert.False(created.TryGetProperty("acceptedOn", out _));
        Assert.False(created.TryGetProperty("dueOn", out _));

        SetUser(client, SecondPlayerId);
        using var updatedResponse = await client.PutAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/missions/{firstMissionId}",
            new { title = "Misión compartida", description = (string?)null, status = "active" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Personaje inicial", updated.GetProperty("authorDisplayName").GetString());
        Assert.False(updated.GetProperty("canDelete").GetBoolean());

        using var forbiddenDelete = await client.DeleteAsync(
            $"/api/v1/campaigns/{CampaignId}/missions/{firstMissionId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        using var secondCreatedResponse = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/missions",
            new { title = "Segunda misión", description = (string?)null, isMain = true },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, secondCreatedResponse.StatusCode);
        var secondCreated = await secondCreatedResponse.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var secondMissionId = secondCreated.GetProperty("id").GetGuid();

        using var list = await client.GetAsync(
            $"/api/v1/campaigns/{CampaignId}/missions",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listed = await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var items = listed.GetProperty("items");
        Assert.Equal(secondMissionId, items[0].GetProperty("id").GetGuid());
        Assert.True(items[0].GetProperty("isMain").GetBoolean());
        Assert.False(items[1].GetProperty("isMain").GetBoolean());

        SetUser(client, PlayerWithoutCharacterId);
        using var withoutCharacter = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/missions",
            new { title = "Sin personaje", description = (string?)null, isMain = false },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, withoutCharacter.StatusCode);

        SetUser(client, DmId);
        using var dmCreatedResponse = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{CampaignId}/missions",
            new { title = "Misión de dirección", description = (string?)null, isMain = false },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, dmCreatedResponse.StatusCode);
        var dmCreated = await dmCreatedResponse.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        Assert.Equal("Dirección de campaña", dmCreated.GetProperty("authorDisplayName").GetString());
        Assert.Null(dmCreated.GetProperty("authorCharacterId").GetString());

        using var dmDeleted = await client.DeleteAsync(
            $"/api/v1/campaigns/{CampaignId}/missions/{secondMissionId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, dmDeleted.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OTEL_SDK_DISABLED", "true");
            builder.UseSetting("ConnectionStrings:Campaigns", connectionString);
            builder.UseSetting("Database:ApplyMigrations", "false");
            builder.UseSetting("Email:OutboxWorkerEnabled", "false");
            builder.UseSetting("Identity:BootstrapToken", "missions-tests-bootstrap-token-000000");
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
            var isPlayer = userId == FirstPlayerId
                || userId == SecondPlayerId
                || userId == PlayerWithoutCharacterId;
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
        public const string SchemeName = "MissionsTests";
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
