using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DndCampaign.Api.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class InvitationFlowIntegrationTests
{
    [Fact]
    public async Task Bootstrap_login_issue_and_accept_invitation_complete_the_flow()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DATABASE");
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(connectionString),
            "IDENTITY_TEST_DATABASE is required for the PostgreSQL integration flow.");
        Assert.Contains("_test", connectionString, StringComparison.OrdinalIgnoreCase);
        var cancellationToken = TestContext.Current.CancellationToken;

        const string bootstrapToken = "integration-bootstrap-token-with-32-characters";
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OTEL_SDK_DISABLED", "true");
            builder.UseSetting("ConnectionStrings:Campaigns", connectionString);
            builder.UseSetting("Database:ApplyMigrations", "false");
            builder.UseSetting("Email:OutboxWorkerEnabled", "false");
            builder.UseSetting("Identity:BootstrapToken", bootstrapToken);
            builder.UseSetting(
                "Identity:OutboxEncryptionKey",
                "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=");
            builder.UseSetting("Frontend:BaseUrl", "https://example.com/application/");
        });
        using var client = factory.CreateClient();
        await ResetDatabaseAsync(factory.Services, delete: true, cancellationToken);

        try
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");
            for (var attempt = 0; attempt < 10; attempt++)
            {
                using var invalidBootstrap = await client.PostAsJsonAsync("/api/v1/identity/bootstrap", new
                {
                    token = "invalid-bootstrap-token",
                    email = "admin@example.com",
                    displayName = "Platform Admin",
                    password = "A-valid-admin-password-123!",
                }, cancellationToken);
                Assert.Equal(HttpStatusCode.Unauthorized, invalidBootstrap.StatusCode);
            }

            using var throttledBootstrap = await client.PostAsJsonAsync("/api/v1/identity/bootstrap", new
            {
                token = "invalid-bootstrap-token",
                email = "admin@example.com",
                displayName = "Platform Admin",
                password = "A-valid-admin-password-123!",
            }, cancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, throttledBootstrap.StatusCode);
            Assert.True(throttledBootstrap.Headers.RetryAfter?.Delta > TimeSpan.Zero);

            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.20");
            using var bootstrap = await client.PostAsJsonAsync("/api/v1/identity/bootstrap", new
            {
                token = bootstrapToken,
                email = "admin@example.com",
                displayName = "Platform Admin",
                password = "A-valid-admin-password-123!",
            }, cancellationToken);
            Assert.Equal(HttpStatusCode.Created, bootstrap.StatusCode);

            using var anonymousList = await client.GetAsync(
                "/api/v1/platform/invitations",
                cancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);

            var adminToken = await LoginAsync(client, "admin@example.com", "A-valid-admin-password-123!", cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            using var issue = await client.PostAsJsonAsync("/api/v1/platform/invitations", new
            {
                email = "player@example.com",
            }, cancellationToken);
            Assert.Equal(HttpStatusCode.Accepted, issue.StatusCode);
            var issuedInvitation = await issue.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var invitationId = issuedInvitation.GetProperty("id").GetGuid();
            using var earlyResend = await client.PostAsJsonAsync(
                $"/api/v1/platform/invitations/{invitationId}/resend",
                new { },
                cancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, earlyResend.StatusCode);

            string invitationToken;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
                var protector = scope.ServiceProvider.GetRequiredService<InvitationTokenProtector>();
                var outbox = await database.InvitationOutbox.SingleAsync(cancellationToken);
                invitationToken = protector.Unprotect(outbox.EncryptedToken);
            }

            client.DefaultRequestHeaders.Authorization = null;
            using var preview = await client.PostAsJsonAsync("/api/v1/invitations/preview", new
            {
                token = invitationToken,
            }, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            var previewJson = await preview.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Assert.Equal("valid", previewJson.GetProperty("state").GetString());
            Assert.False(previewJson.GetProperty("requiresAuthentication").GetBoolean());

            using var acceptance = await client.PostAsJsonAsync("/api/v1/invitations/accept", new
            {
                token = invitationToken,
                displayName = "Invited Player",
                password = "A-valid-player-password-123!",
            }, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, acceptance.StatusCode);

            var playerToken = await LoginAsync(
                client,
                "player@example.com",
                "A-valid-player-password-123!",
                cancellationToken);
            Assert.False(string.IsNullOrWhiteSpace(playerToken));

            using var reused = await client.PostAsJsonAsync("/api/v1/invitations/accept", new
            {
                token = invitationToken,
            }, cancellationToken);
            Assert.Equal(HttpStatusCode.Gone, reused.StatusCode);
        }
        finally
        {
            await ResetDatabaseAsync(factory.Services, delete: true, cancellationToken);
        }
    }

    private static async Task<string> LoginAsync(
        HttpClient client,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var login = await client.PostAsJsonAsync(
            "/api/v1/identity/login",
            new { email, password },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var payload = await login.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return payload.GetProperty("accessToken").GetString()!;
    }

    private static async Task ResetDatabaseAsync(
        IServiceProvider services,
        bool delete,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
        if (delete)
        {
            await database.Database.EnsureDeletedAsync(cancellationToken);
        }

        await database.Database.MigrateAsync(cancellationToken);
    }
}
