using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DndCampaign.Modules.AdventureCatalog;
using DndCampaign.Modules.Access.Infrastructure.Persistence;
using DndCampaign.Modules.Access.Infrastructure.Security;
using DndCampaign.Modules.Campaigns;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DndCampaign.Modules.Access.Tests.Component;

[CollectionDefinition("access-integration", DisableParallelization = true)]
public sealed class AccessIntegrationCollection;

[Collection("access-integration")]
public sealed class AccessApiContractIntegrationTests
{
    private const string BootstrapToken = "contract-bootstrap-token-with-32-characters";
    private const string AdminEmail = "admin.contract@example.com";
    private const string AdminPassword = "A-valid-admin-password-123!";

    [Fact]
    public async Task Invitation_event_endpoint_isolated_from_user_sessions()
    {
        var connectionString = RequireIntegrationDatabase();
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();

        using var anonymous = await client.PostAsJsonAsync(
            "/internal/events/invitation-email",
            Array.Empty<object>(),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Event-Grid-Test", "1");
        using var malformed = await client.PostAsJsonAsync(
            "/internal/events/invitation-email",
            Array.Empty<object>(),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);

        using var validationRequest = new HttpRequestMessage(
            HttpMethod.Options,
            "/internal/events/invitation-email");
        validationRequest.Headers.TryAddWithoutValidation(
            "WebHook-Request-Origin",
            "eventgrid.azure.net");
        using var validationResponse = await client.SendAsync(
            validationRequest,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, validationResponse.StatusCode);
        Assert.Equal(
            "eventgrid.azure.net",
            Assert.Single(validationResponse.Headers.GetValues("WebHook-Allowed-Origin")));
        Assert.Equal(
            "*",
            Assert.Single(validationResponse.Headers.GetValues("WebHook-Allowed-Rate")));
        Assert.Contains("POST", validationResponse.Headers.GetValues("Allow"));
    }

    [Fact]
    public async Task Identity_contract_supports_bootstrap_session_and_logout()
    {
        var connectionString = RequireIntegrationDatabase();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        await ResetDatabaseAsync(factory.Services, cancellationToken);

        try
        {
            using var initialStatus = await client.GetAsync(
                "/api/v1/identity/bootstrap",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, initialStatus.StatusCode);
            Assert.Equal(
                "required",
                (await initialStatus.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
                    .GetProperty("state")
                    .GetString());

            var admin = await BootstrapAsync(client, cancellationToken);

            using var completedStatus = await client.GetAsync(
                "/api/v1/identity/bootstrap",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, completedStatus.StatusCode);
            Assert.Equal(
                "completed",
                (await completedStatus.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
                    .GetProperty("state")
                    .GetString());

            var accessToken = await LoginAsync(client, cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var me = await client.GetAsync("/api/v1/identity/me", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            var currentUser = await me.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Assert.Equal(admin.GetProperty("id").GetGuid(), currentUser.GetProperty("id").GetGuid());
            Assert.Equal(AdminEmail, currentUser.GetProperty("email").GetString());
            Assert.True(currentUser.GetProperty("isPlatformAdmin").GetBoolean());

            using var logout = await client.PostAsync(
                "/api/v1/identity/logout",
                content: null,
                cancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

            using var revokedSession = await client.GetAsync("/api/v1/identity/me", cancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, revokedSession.StatusCode);
        }
        finally
        {
            await DeleteDatabaseAsync(factory.Services, cancellationToken);
        }
    }

    [Fact]
    public async Task Invitation_management_contract_preserves_platform_and_campaign_boundaries()
    {
        var connectionString = RequireIntegrationDatabase();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        await ResetDatabaseAsync(factory.Services, cancellationToken);

        try
        {
            await BootstrapAsync(client, cancellationToken);
            var accessToken = await LoginAsync(client, cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var createCampaign = await client.PostAsJsonAsync(
                "/api/v1/campaigns",
                new { name = "Contract Campaign" },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Created, createCampaign.StatusCode);
            var campaignId = (await createCampaign.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
                .GetProperty("id")
                .GetGuid();

            var platformInvitationId = await IssueInvitationAsync(
                client,
                "/api/v1/platform/invitations",
                "platform.contract@example.com",
                cancellationToken);

            using var platformList = await client.GetAsync(
                "/api/v1/platform/invitations",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, platformList.StatusCode);
            var platformInvitations = await platformList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Assert.Contains(
                platformInvitations.EnumerateArray(),
                invitation => invitation.GetProperty("id").GetGuid() == platformInvitationId);

            using var platformRevoke = await client.DeleteAsync(
                $"/api/v1/platform/invitations/{platformInvitationId}",
                cancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, platformRevoke.StatusCode);

            using var repeatedPlatformRevoke = await client.DeleteAsync(
                $"/api/v1/platform/invitations/{platformInvitationId}",
                cancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, repeatedPlatformRevoke.StatusCode);

            var campaignInvitationId = await IssueInvitationAsync(
                client,
                $"/api/v1/campaigns/{campaignId}/invitations",
                "campaign.contract@example.com",
                cancellationToken);

            using var campaignList = await client.GetAsync(
                $"/api/v1/campaigns/{campaignId}/invitations",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, campaignList.StatusCode);

            using var earlyResend = await client.PostAsJsonAsync(
                $"/api/v1/campaigns/{campaignId}/invitations/{campaignInvitationId}/resend",
                new { },
                cancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, earlyResend.StatusCode);

            using var campaignRevoke = await client.DeleteAsync(
                $"/api/v1/campaigns/{campaignId}/invitations/{campaignInvitationId}",
                cancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, campaignRevoke.StatusCode);

            var foreignCampaignId = Guid.NewGuid();
            using var foreignList = await client.GetAsync(
                $"/api/v1/campaigns/{foreignCampaignId}/invitations",
                cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, foreignList.StatusCode);

            using var foreignIssue = await client.PostAsJsonAsync(
                $"/api/v1/campaigns/{foreignCampaignId}/invitations",
                new { email = "forbidden.contract@example.com" },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, foreignIssue.StatusCode);
        }
        finally
        {
            await DeleteDatabaseAsync(factory.Services, cancellationToken);
        }
    }

    [Fact]
    public async Task Existing_user_can_be_selected_invited_and_see_the_campaign_after_acceptance()
    {
        var connectionString = RequireIntegrationDatabase();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();
        await ResetDatabaseAsync(factory.Services, cancellationToken);

        try
        {
            var admin = await BootstrapAsync(client, cancellationToken);
            var adminToken = await LoginAsync(client, cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            using (var invalidPlatformRecipient = await client.PostAsJsonAsync(
                "/api/v1/platform/invitations",
                new { recipientUserId = admin.GetProperty("id").GetGuid() },
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.BadRequest, invalidPlatformRecipient.StatusCode);
            }

            var platformInvitationId = await IssueInvitationAsync(
                client,
                "/api/v1/platform/invitations",
                "existing.player@example.com",
                cancellationToken);
            var platformToken = await ReadInvitationTokenAsync(
                factory.Services,
                platformInvitationId,
                cancellationToken);

            client.DefaultRequestHeaders.Authorization = null;
            using (var acceptAccount = await client.PostAsJsonAsync("/api/v1/invitations/accept", new
            {
                token = platformToken,
                displayName = "Existing Player",
                password = "A-valid-player-password-123!",
            }, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, acceptAccount.StatusCode);
            }

            var playerToken = await LoginAsync(
                client,
                "existing.player@example.com",
                "A-valid-player-password-123!",
                cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
            using var me = await client.GetAsync("/api/v1/identity/me", cancellationToken);
            var playerId = (await me.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
                .GetProperty("id")
                .GetGuid();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            using var createCampaign = await client.PostAsJsonAsync(
                "/api/v1/campaigns",
                new { name = "Integrated Campaign" },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Created, createCampaign.StatusCode);
            Assert.NotNull(createCampaign.Headers.Location);
            var campaign = await createCampaign.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var campaignId = campaign.GetProperty("id").GetGuid();
            Assert.NotEqual(Guid.Empty, admin.GetProperty("id").GetGuid());
            Assert.Equal("dm", campaign.GetProperty("role").GetString());
            Assert.Equal(JsonValueKind.Null, campaign.GetProperty("adventureModuleId").ValueKind);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
            using (var playerCampaignsBeforeAcceptance = await client.GetAsync(
                "/api/v1/campaigns",
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, playerCampaignsBeforeAcceptance.StatusCode);
                Assert.Empty((await playerCampaignsBeforeAcceptance.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
                    .EnumerateArray());
            }
            using (var forbiddenDetail = await client.GetAsync(
                $"/api/v1/campaigns/{campaignId}",
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Forbidden, forbiddenDetail.StatusCode);
            }
            using (var forbiddenSearch = await client.GetAsync(
                $"/api/v1/campaigns/{campaignId}/eligible-users",
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Forbidden, forbiddenSearch.StatusCode);
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            using var eligible = await client.GetAsync(
                $"/api/v1/campaigns/{campaignId}/eligible-users?query=player",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, eligible.StatusCode);
            var eligiblePage = await eligible.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var eligibleUser = Assert.Single(eligiblePage.GetProperty("items").EnumerateArray());
            Assert.Equal(playerId, eligibleUser.GetProperty("userId").GetGuid());
            Assert.Equal("Existing Player", eligibleUser.GetProperty("displayName").GetString());
            Assert.NotEqual("existing.player@example.com", eligibleUser.GetProperty("maskedEmail").GetString());

            using var issueCampaign = await client.PostAsJsonAsync(
                $"/api/v1/campaigns/{campaignId}/invitations",
                new { recipientUserId = playerId },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Accepted, issueCampaign.StatusCode);
            var campaignInvitationId = (await issueCampaign.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
                .GetProperty("id")
                .GetGuid();

            using (var duplicate = await client.PostAsJsonAsync(
                $"/api/v1/campaigns/{campaignId}/invitations",
                new { recipientUserId = playerId },
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            }

            var campaignToken = await ReadInvitationTokenAsync(
                factory.Services,
                campaignInvitationId,
                cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
            using (var acceptance = await client.PostAsJsonAsync("/api/v1/invitations/accept", new
            {
                token = campaignToken,
            }, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, acceptance.StatusCode);
            }

            using var playerCampaigns = await client.GetAsync("/api/v1/campaigns", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, playerCampaigns.StatusCode);
            var accessibleCampaign = Assert.Single(
                (await playerCampaigns.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray());
            Assert.Equal(campaignId, accessibleCampaign.GetProperty("id").GetGuid());
            Assert.Equal("player", accessibleCampaign.GetProperty("role").GetString());

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            using var eligibleAfterAcceptance = await client.GetAsync(
                $"/api/v1/campaigns/{campaignId}/eligible-users?query=player",
                cancellationToken);
            Assert.Empty((await eligibleAfterAcceptance.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
                .GetProperty("items")
                .EnumerateArray());

            var pendingInvitationId = await IssueInvitationAsync(
                client,
                $"/api/v1/campaigns/{campaignId}/invitations",
                "pending.after.deletion@example.com",
                cancellationToken);
            var pendingInvitationToken = await ReadInvitationTokenAsync(
                factory.Services,
                pendingInvitationId,
                cancellationToken);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
            using (var forbiddenDeletion = await client.DeleteAsync(
                $"/api/v1/campaigns/{campaignId}",
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Forbidden, forbiddenDeletion.StatusCode);
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            using (var deletion = await client.DeleteAsync(
                $"/api/v1/campaigns/{campaignId}",
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
            }

            using (var repeatedDeletion = await client.DeleteAsync(
                $"/api/v1/campaigns/{campaignId}",
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.NotFound, repeatedDeletion.StatusCode);
            }

            using (var deletedDetail = await client.GetAsync(
                $"/api/v1/campaigns/{campaignId}",
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.NotFound, deletedDetail.StatusCode);
            }

            using (var deletedInvitations = await client.GetAsync(
                $"/api/v1/campaigns/{campaignId}/invitations",
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Forbidden, deletedInvitations.StatusCode);
            }

            using (var dependentModule = await client.GetAsync(
                $"/api/v1/campaigns/{campaignId}/characters",
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.NotFound, dependentModule.StatusCode);
            }

            using (var dmCampaignsAfterDeletion = await client.GetAsync(
                "/api/v1/campaigns",
                cancellationToken))
            {
                Assert.Empty((await dmCampaignsAfterDeletion.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
                    .EnumerateArray());
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
            using (var playerCampaignsAfterDeletion = await client.GetAsync(
                "/api/v1/campaigns",
                cancellationToken))
            {
                Assert.Empty((await playerCampaignsAfterDeletion.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
                    .EnumerateArray());
            }

            client.DefaultRequestHeaders.Authorization = null;
            using var deletedCampaignAcceptance = await client.PostAsJsonAsync(
                "/api/v1/invitations/accept",
                new
                {
                    token = pendingInvitationToken,
                    displayName = "Pending Player",
                    password = "A-valid-player-password-123!",
                },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Gone, deletedCampaignAcceptance.StatusCode);
        }
        finally
        {
            await DeleteDatabaseAsync(factory.Services, cancellationToken);
        }
    }

    [Fact]
    public async Task Concurrent_bootstrap_creates_exactly_one_administrator()
    {
        var connectionString = RequireIntegrationDatabase();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateFactory(connectionString);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        await ResetDatabaseAsync(factory.Services, cancellationToken);

        try
        {
            firstClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "192.0.2.51");
            secondClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "192.0.2.52");
            var firstRequest = firstClient.PostAsJsonAsync("/api/v1/identity/bootstrap", new
            {
                token = BootstrapToken,
                email = "first.bootstrap@example.com",
                displayName = "First Admin",
                password = AdminPassword,
            }, cancellationToken);
            var secondRequest = secondClient.PostAsJsonAsync("/api/v1/identity/bootstrap", new
            {
                token = BootstrapToken,
                email = "second.bootstrap@example.com",
                displayName = "Second Admin",
                password = AdminPassword,
            }, cancellationToken);

            using var firstResponse = await firstRequest;
            using var secondResponse = await secondRequest;
            var statusCodes = new[] { firstResponse.StatusCode, secondResponse.StatusCode };
            Assert.Single(statusCodes, status => status == HttpStatusCode.Created);
            Assert.Single(statusCodes, status => status == HttpStatusCode.Conflict);

            await using var scope = factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            Assert.Equal(1, await database.Users.CountAsync(cancellationToken));
        }
        finally
        {
            await DeleteDatabaseAsync(factory.Services, cancellationToken);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OTEL_SDK_DISABLED", "true");
            builder.UseSetting("ConnectionStrings:Campaigns", connectionString);
            builder.UseSetting("Database:ApplyMigrations", "false");
            builder.UseSetting("EventGrid:Enabled", "false");
            builder.UseSetting("Identity:BootstrapToken", BootstrapToken);
            builder.UseSetting(
                "Identity:OutboxEncryptionKey",
                "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=");
            builder.UseSetting("Frontend:BaseUrl", "https://example.com/application/");
        });

    private static string RequireIntegrationDatabase()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DATABASE");
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(connectionString),
            "IDENTITY_TEST_DATABASE is required for PostgreSQL contract tests.");
        Assert.Contains("_test", connectionString, StringComparison.OrdinalIgnoreCase);
        return connectionString!;
    }

    private static async Task<JsonElement> BootstrapAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "192.0.2.40");
        using var response = await client.PostAsJsonAsync("/api/v1/identity/bootstrap", new
        {
            token = BootstrapToken,
            email = AdminEmail,
            displayName = "Contract Admin",
            password = AdminPassword,
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static async Task<string> LoginAsync(
        HttpClient client,
        CancellationToken cancellationToken)
        => await LoginAsync(client, AdminEmail, AdminPassword, cancellationToken);

    private static async Task<string> LoginAsync(
        HttpClient client,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/identity/login", new
        {
            email,
            password,
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return payload.GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> ReadInvitationTokenAsync(
        IServiceProvider services,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<InvitationTokenProtector>();
        var outbox = await database.InvitationOutbox.AsNoTracking().SingleAsync(
            message => message.InvitationId == invitationId,
            cancellationToken);
        return protector.Unprotect(outbox.EncryptedToken);
    }

    private static async Task<Guid> IssueInvitationAsync(
        HttpClient client,
        string path,
        string email,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(path, new { email }, cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return payload.GetProperty("id").GetGuid();
    }

    private static async Task ResetDatabaseAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await DeleteDatabaseAsync(services, cancellationToken);
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
        await services.ApplyAdventureCatalogMigrationsAsync(cancellationToken);
        await services.ApplyCampaignsMigrationsAsync(cancellationToken);
    }

    private static async Task DeleteDatabaseAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        await database.Database.EnsureDeletedAsync(cancellationToken);
    }
}
