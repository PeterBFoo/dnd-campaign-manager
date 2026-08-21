using DndCampaign.Api.Application;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using DndCampaign.Api.Composition;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DndCampaign.Api.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class ApplicationServicesIntegrationTests
{
    [Fact]
    public async Task IdentityService_bootstrap_login_and_logout_round_trip()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var identityService = provider.GetRequiredService<IIdentityService>();
            var options = provider.GetRequiredService<IdentitySecurityOptions>();

            var status = await identityService.GetBootstrapStatus(cancellationToken);
            Assert.Equal(BootstrapStatus.Required, status);

            var (bootstrapStatus, _, user) = await identityService.BootstrapAsync(
                new BootstrapAccountCommand(
                    options.BootstrapToken,
                    "admin@example.com",
                    "Platform Admin",
                    "A-valid-admin-password-123!"),
                cancellationToken);
            Assert.Equal(BootstrapCreationStatus.Created, bootstrapStatus);
            Assert.NotNull(user);

            var login = await identityService.LoginAsync(
                new LoginCommand("admin@example.com", "A-valid-admin-password-123!"),
                cancellationToken);
            Assert.NotNull(login);

            await using (var scope = provider.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
                var session = await database.UserSessions.SingleAsync(cancellationToken);
                await identityService.LogoutAsync(new LogoutCommand(session.Id), cancellationToken);
                await database.Entry(session).ReloadAsync(cancellationToken);
                Assert.NotNull(session.RevokedAt);
            }
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task PlatformInvitationService_issue_conflict_resend_rate_limit_and_revoke()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var user = await BootstrapAdminAsync(provider, cancellationToken);
            var platformService = provider.GetRequiredService<IPlatformInvitationService>();

            var issued = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand("player@example.com", user.Id),
                cancellationToken);
            Assert.Equal("pending", issued.Status);

            await Assert.ThrowsAsync<InvitationConflictException>(() =>
                platformService.IssueAsync(
                    new IssuePlatformInvitationCommand("player@example.com", user.Id),
                    cancellationToken));

            await Assert.ThrowsAsync<InvitationRateLimitException>(() =>
                platformService.ResendAsync(new ResendInvitationCommand(issued.Id), cancellationToken));

            var revokeStatus = await platformService.RevokeAsync(
                new RevokeInvitationCommand(issued.Id),
                cancellationToken);
            Assert.Equal(RevokeInvitationStatus.Revoked, revokeStatus);

            var conflictingRevoke = await platformService.RevokeAsync(
                new RevokeInvitationCommand(issued.Id),
                cancellationToken);
            Assert.Equal(RevokeInvitationStatus.Conflict, conflictingRevoke);

            var missingRevoke = await platformService.RevokeAsync(
                new RevokeInvitationCommand(Guid.NewGuid()),
                cancellationToken);
            Assert.Equal(RevokeInvitationStatus.NotFound, missingRevoke);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task PlatformInvitationService_issue_persists_invitation_and_outbox_together()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var user = await BootstrapAdminAsync(provider, cancellationToken);
            var platformService = provider.GetRequiredService<IPlatformInvitationService>();
            var issued = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand("player@example.com", user.Id),
                cancellationToken);

            var database = provider.GetRequiredService<CampaignDbContext>();
            Assert.Equal(1, await database.Invitations.CountAsync(cancellationToken));
            Assert.Equal(1, await database.InvitationOutbox.CountAsync(cancellationToken));
            Assert.Equal(
                issued.Id,
                (await database.InvitationOutbox.SingleAsync(cancellationToken)).InvitationId);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task PlatformInvitationService_issue_rolls_back_both_when_enqueue_fails()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!, throwOnEnqueue: true);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var user = await BootstrapAdminAsync(provider, cancellationToken);
            var platformService = provider.GetRequiredService<IPlatformInvitationService>();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                platformService.IssueAsync(
                    new IssuePlatformInvitationCommand("player@example.com", user.Id),
                    cancellationToken));

            var database = provider.GetRequiredService<CampaignDbContext>();
            Assert.Equal(0, await database.Invitations.CountAsync(cancellationToken));
            Assert.Equal(0, await database.InvitationOutbox.CountAsync(cancellationToken));
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task PlatformInvitationService_resend_persists_replacement_against_real_stores()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var user = await BootstrapAdminAsync(provider, cancellationToken);
            var platformService = provider.GetRequiredService<IPlatformInvitationService>();
            var issued = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand("player@example.com", user.Id),
                cancellationToken);

            var database = provider.GetRequiredService<CampaignDbContext>();
            var original = await database.Invitations.SingleAsync(
                invitation => invitation.Id == issued.Id,
                cancellationToken);
            database.Entry(original).Property(nameof(InvitationRecord.IssuedAt)).CurrentValue =
                DateTimeOffset.UtcNow.AddMinutes(-16);
            await database.SaveChangesAsync(cancellationToken);

            var (status, summary) = await platformService.ResendAsync(
                new ResendInvitationCommand(issued.Id),
                cancellationToken);

            Assert.Equal(ResendInvitationStatus.Resent, status);
            Assert.NotNull(summary);
            Assert.NotEqual(issued.Id, summary.Id);

            await database.Entry(original).ReloadAsync(cancellationToken);
            Assert.Equal(InvitationStatus.Revoked, original.Status);
            Assert.Equal(2, await database.Invitations.CountAsync(cancellationToken));
            Assert.Equal(2, await database.InvitationOutbox.CountAsync(cancellationToken));
            Assert.Equal(
                InvitationStatus.Pending,
                (await database.Invitations.SingleAsync(
                    invitation => invitation.Id == summary.Id,
                    cancellationToken)).Status);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task PlatformInvitationService_revoke_of_expired_invitation_does_not_persist_expired()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var user = await BootstrapAdminAsync(provider, cancellationToken);
            var platformService = provider.GetRequiredService<IPlatformInvitationService>();
            var issued = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand("player@example.com", user.Id),
                cancellationToken);
            await ExpireInvitationAsync(provider, issued.Id, cancellationToken);

            var status = await platformService.RevokeAsync(
                new RevokeInvitationCommand(issued.Id),
                cancellationToken);

            Assert.Equal(RevokeInvitationStatus.Conflict, status);
            var database = provider.GetRequiredService<CampaignDbContext>();
            var row = await database.Invitations.AsNoTracking().SingleAsync(
                invitation => invitation.Id == issued.Id,
                cancellationToken);
            Assert.Equal(InvitationStatus.Pending, row.Status);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task PlatformInvitationService_list_expires_all_pending_invitations_together()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var user = await BootstrapAdminAsync(provider, cancellationToken);
            var platformService = provider.GetRequiredService<IPlatformInvitationService>();
            var first = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand("first@example.com", user.Id),
                cancellationToken);
            var second = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand("second@example.com", user.Id),
                cancellationToken);
            await ExpireInvitationAsync(provider, first.Id, cancellationToken);
            await ExpireInvitationAsync(provider, second.Id, cancellationToken);

            var items = await platformService.ListAsync(new ListPlatformInvitationsCommand(), cancellationToken);

            Assert.Equal(2, items.Count);
            Assert.All(items, item => Assert.Equal("expired", item.Status));
            var database = provider.GetRequiredService<CampaignDbContext>();
            Assert.Equal(
                2,
                await database.Invitations.CountAsync(
                    invitation => invitation.Status == InvitationStatus.Expired,
                    cancellationToken));
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task InvitationAcceptanceService_preview_and_accept_flow()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var user = await BootstrapAdminAsync(provider, cancellationToken);
            var platformService = provider.GetRequiredService<IPlatformInvitationService>();
            var acceptanceService = provider.GetRequiredService<IInvitationAcceptanceService>();

            var invalidPreview = await acceptanceService.PreviewAsync(
                new PreviewInvitationCommand("short"),
                cancellationToken);
            Assert.Equal("invalid", invalidPreview.State);

            var issued = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand("player@example.com", user.Id),
                cancellationToken);

            var token = await ReadInvitationTokenAsync(provider, issued.Id, cancellationToken);

            var preview = await acceptanceService.PreviewAsync(
                new PreviewInvitationCommand(token),
                cancellationToken);
            Assert.Equal("valid", preview.State);
            Assert.Equal("platform", preview.Kind);

            var acceptResult = await acceptanceService.AcceptAsync(
                new AcceptInvitationCommand(token, "Invited Player", "A-valid-player-password-123!"),
                new AuthenticatedActor(false, null),
                cancellationToken);
            Assert.Equal(AcceptInvitationStatus.Accepted, acceptResult.Status);
            Assert.NotNull(acceptResult.Outcome?.AccessToken);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task InvitationAcceptanceService_preview_reports_expired_accepted_and_revoked_states()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var admin = await BootstrapAdminAsync(provider, cancellationToken);
            var platformService = provider.GetRequiredService<IPlatformInvitationService>();
            var acceptanceService = provider.GetRequiredService<IInvitationAcceptanceService>();

            var expiredInvitation = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand("expired@example.com", admin.Id),
                cancellationToken);
            var expiredToken = await ReadInvitationTokenAsync(provider, expiredInvitation.Id, cancellationToken);
            await ExpireInvitationAsync(provider, expiredInvitation.Id, cancellationToken);

            var expiredPreview = await acceptanceService.PreviewAsync(
                new PreviewInvitationCommand(expiredToken),
                cancellationToken);
            Assert.Equal("expired", expiredPreview.State);
            Assert.False(expiredPreview.RequiresAuthentication);

            var acceptedInvitation = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand("accepted@example.com", admin.Id),
                cancellationToken);
            var acceptedToken = await ReadInvitationTokenAsync(provider, acceptedInvitation.Id, cancellationToken);
            var acceptResult = await acceptanceService.AcceptAsync(
                new AcceptInvitationCommand(acceptedToken, "Accepted Player", "A-valid-player-password-123!"),
                new AuthenticatedActor(false, null),
                cancellationToken);
            Assert.Equal(AcceptInvitationStatus.Accepted, acceptResult.Status);

            var acceptedPreview = await acceptanceService.PreviewAsync(
                new PreviewInvitationCommand(acceptedToken),
                cancellationToken);
            Assert.Equal("accepted", acceptedPreview.State);

            var revokedInvitation = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand("revoked@example.com", admin.Id),
                cancellationToken);
            var revokedToken = await ReadInvitationTokenAsync(provider, revokedInvitation.Id, cancellationToken);
            Assert.Equal(
                RevokeInvitationStatus.Revoked,
                await platformService.RevokeAsync(
                    new RevokeInvitationCommand(revokedInvitation.Id),
                    cancellationToken));

            var revokedPreview = await acceptanceService.PreviewAsync(
                new PreviewInvitationCommand(revokedToken),
                cancellationToken);
            Assert.Equal("revoked", revokedPreview.State);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task InvitationAcceptanceService_rejects_accept_for_existing_user_without_matching_actor()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var admin = await BootstrapAdminAsync(provider, cancellationToken);
            var platformService = provider.GetRequiredService<IPlatformInvitationService>();
            var acceptanceService = provider.GetRequiredService<IInvitationAcceptanceService>();

            var issued = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand(admin.Email, admin.Id),
                cancellationToken);
            var token = await ReadInvitationTokenAsync(provider, issued.Id, cancellationToken);

            var anonymous = await acceptanceService.AcceptAsync(
                new AcceptInvitationCommand(token, null, null),
                new AuthenticatedActor(false, null),
                cancellationToken);
            Assert.Equal(AcceptInvitationStatus.Unauthorized, anonymous.Status);

            var otherUser = await acceptanceService.AcceptAsync(
                new AcceptInvitationCommand(token, null, null),
                new AuthenticatedActor(true, Guid.NewGuid()),
                cancellationToken);
            Assert.Equal(AcceptInvitationStatus.Forbidden, otherUser.Status);

            var owner = await acceptanceService.AcceptAsync(
                new AcceptInvitationCommand(token, null, null),
                new AuthenticatedActor(true, admin.Id),
                cancellationToken);
            Assert.Equal(AcceptInvitationStatus.Accepted, owner.Status);
            Assert.Null(owner.Outcome?.AccessToken);

            var replay = await acceptanceService.AcceptAsync(
                new AcceptInvitationCommand(token, null, null),
                new AuthenticatedActor(true, admin.Id),
                cancellationToken);
            Assert.Equal(AcceptInvitationStatus.AlreadyAccepted, replay.Status);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task InvitationAcceptanceService_accepting_campaign_invitation_joins_the_campaign_as_player()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var dm = await BootstrapAdminAsync(provider, cancellationToken);
            var campaignService = provider.GetRequiredService<ICampaignInvitationService>();
            var acceptanceService = provider.GetRequiredService<IInvitationAcceptanceService>();
            var database = provider.GetRequiredService<CampaignDbContext>();
            var campaignId = Guid.NewGuid();

            database.CampaignMemberships.Add(
                CampaignMembership.CreateDm(campaignId, dm.Id, DateTimeOffset.UtcNow));
            await database.SaveChangesAsync(cancellationToken);

            var (_, summary) = await campaignService.IssueAsync(
                new IssueCampaignInvitationCommand("campaign-player@example.com", campaignId, dm.Id),
                cancellationToken);
            var token = await ReadInvitationTokenAsync(provider, summary!.Id, cancellationToken);

            var accepted = await acceptanceService.AcceptAsync(
                new AcceptInvitationCommand(token, "Campaign Player", "A-valid-player-password-123!"),
                new AuthenticatedActor(false, null),
                cancellationToken);
            Assert.Equal(AcceptInvitationStatus.Accepted, accepted.Status);
            Assert.Equal("campaign", accepted.Outcome!.Kind);
            Assert.NotNull(accepted.Outcome.AccessToken);

            var membership = await database.CampaignMemberships.SingleAsync(
                candidate =>
                    candidate.CampaignId == campaignId
                    && candidate.UserId == accepted.Outcome.User.Id,
                cancellationToken);
            Assert.Equal(CampaignRole.Player, membership.Role);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task PlatformInvitationService_list_marks_expired_invitations()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var user = await BootstrapAdminAsync(provider, cancellationToken);
            var platformService = provider.GetRequiredService<IPlatformInvitationService>();

            var issued = await platformService.IssueAsync(
                new IssuePlatformInvitationCommand("player@example.com", user.Id),
                cancellationToken);

            await ExpireInvitationAsync(provider, issued.Id, cancellationToken);

            var listed = await platformService.ListAsync(new ListPlatformInvitationsCommand(), cancellationToken);
            Assert.Contains(listed, summary => summary.Id == issued.Id && summary.Status == "expired");
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task CampaignInvitationService_issue_list_and_revoke_for_dm()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var user = await BootstrapAdminAsync(provider, cancellationToken);
            var campaignService = provider.GetRequiredService<ICampaignInvitationService>();
            var campaignId = Guid.NewGuid();

            await using (var scope = provider.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
                database.CampaignMemberships.Add(
                    CampaignMembership.CreateDm(campaignId, user.Id, DateTimeOffset.UtcNow));
                await database.SaveChangesAsync(cancellationToken);
            }

            var (forbiddenList, _) = await campaignService.ListAsync(
                new ListCampaignInvitationsCommand(campaignId, Guid.NewGuid()),
                cancellationToken);
            Assert.Equal(CampaignAccessStatus.Forbidden, forbiddenList);

            var (allowedList, items) = await campaignService.ListAsync(
                new ListCampaignInvitationsCommand(campaignId, user.Id),
                cancellationToken);
            Assert.Equal(CampaignAccessStatus.Allowed, allowedList);
            Assert.Empty(items!);

            var (issueAccess, summary) = await campaignService.IssueAsync(
                new IssueCampaignInvitationCommand("player@example.com", campaignId, user.Id),
                cancellationToken);
            Assert.Equal(CampaignAccessStatus.Allowed, issueAccess);
            Assert.NotNull(summary);

            var revokeStatus = await campaignService.RevokeAsync(
                campaignId,
                user.Id,
                new RevokeInvitationCommand(summary!.Id),
                cancellationToken);
            Assert.Equal(CampaignAccessStatus.Allowed, revokeStatus.Access);
            Assert.Equal(RevokeInvitationStatus.Revoked, revokeStatus.Status);

            var conflictingRevoke = await campaignService.RevokeAsync(
                campaignId,
                user.Id,
                new RevokeInvitationCommand(summary.Id),
                cancellationToken);
            Assert.Equal(RevokeInvitationStatus.Conflict, conflictingRevoke.Status);

            var missingRevoke = await campaignService.RevokeAsync(
                campaignId,
                user.Id,
                new RevokeInvitationCommand(Guid.NewGuid()),
                cancellationToken);
            Assert.Equal(RevokeInvitationStatus.NotFound, missingRevoke.Status);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task CampaignInvitationService_issue_conflict_resend_rate_limit_and_expired_list()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            var dm = await BootstrapAdminAsync(provider, cancellationToken);
            var campaignService = provider.GetRequiredService<ICampaignInvitationService>();
            var campaignId = Guid.NewGuid();
            var outsiderId = Guid.NewGuid();

            await using (var scope = provider.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
                database.CampaignMemberships.Add(
                    CampaignMembership.CreateDm(campaignId, dm.Id, DateTimeOffset.UtcNow));
                await database.SaveChangesAsync(cancellationToken);
            }

            var (_, summary) = await campaignService.IssueAsync(
                new IssueCampaignInvitationCommand("player@example.com", campaignId, dm.Id),
                cancellationToken);
            Assert.NotNull(summary);

            await Assert.ThrowsAsync<InvitationConflictException>(() =>
                campaignService.IssueAsync(
                    new IssueCampaignInvitationCommand("player@example.com", campaignId, dm.Id),
                    cancellationToken));

            await Assert.ThrowsAsync<InvitationRateLimitException>(() =>
                campaignService.ResendAsync(
                    new ResendCampaignInvitationCommand(campaignId, dm.Id, summary!.Id),
                    cancellationToken));

            var forbiddenResend = await campaignService.ResendAsync(
                new ResendCampaignInvitationCommand(campaignId, outsiderId, summary!.Id),
                cancellationToken);
            Assert.Equal(CampaignAccessStatus.Forbidden, forbiddenResend.Access);

            var missingResend = await campaignService.ResendAsync(
                new ResendCampaignInvitationCommand(campaignId, dm.Id, Guid.NewGuid()),
                cancellationToken);
            Assert.Equal(CampaignAccessStatus.Allowed, missingResend.Access);
            Assert.Equal(ResendInvitationStatus.NotFound, missingResend.Status);

            await ExpireInvitationAsync(provider, summary.Id, cancellationToken);

            var (_, items) = await campaignService.ListAsync(
                new ListCampaignInvitationsCommand(campaignId, dm.Id),
                cancellationToken);
            Assert.Contains(items!, item => item.Id == summary.Id && item.Status == "expired");

            await Assert.ThrowsAsync<InvitationStateException>(() =>
                campaignService.ResendAsync(
                    new ResendCampaignInvitationCommand(campaignId, dm.Id, summary.Id),
                    cancellationToken));
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    private static async Task<string> ReadInvitationTokenAsync(
        IServiceProvider provider,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var database = provider.GetRequiredService<CampaignDbContext>();
        var protector = provider.GetRequiredService<InvitationTokenProtector>();
        var outbox = await database.InvitationOutbox
            .Where(message => message.InvitationId == invitationId)
            .OrderByDescending(message => message.CreatedAt)
            .FirstAsync(cancellationToken);
        return protector.Unprotect(outbox.EncryptedToken);
    }

    /// <summary>
    /// Backdates the expiry using the same scoped <see cref="CampaignDbContext"/> the services resolve,
    /// so the tracked entity and the row stay in sync.
    /// </summary>
    private static async Task ExpireInvitationAsync(
        IServiceProvider provider,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var database = provider.GetRequiredService<CampaignDbContext>();
        var invitation = await database.Invitations.SingleAsync(
            candidate => candidate.Id == invitationId,
            cancellationToken);
        database.Entry(invitation).Property(nameof(InvitationRecord.ExpiresAt))
            .CurrentValue = DateTimeOffset.UtcNow.AddDays(-1);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static async Task<UserProfile> BootstrapAdminAsync(
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        var identityService = provider.GetRequiredService<IIdentityService>();
        var options = provider.GetRequiredService<IdentitySecurityOptions>();
        var (_, _, user) = await identityService.BootstrapAsync(
            new BootstrapAccountCommand(
                options.BootstrapToken,
                "admin@example.com",
                "Platform Admin",
                "A-valid-admin-password-123!"),
            cancellationToken);
        return user!;
    }

    private static ServiceProvider BuildServiceProvider(
        string connectionString,
        bool throwOnEnqueue = false)
    {
        var services = new ServiceCollection();
        services.AddDbContext<CampaignDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IIdentityStore, IdentityStore>();
        services.AddScoped<IInvitationStore, InvitationStore>();
        services.AddScoped<InvitationOutboxStore>();
        if (throwOnEnqueue)
        {
            services.AddScoped<IInvitationOutboxStore>(provider =>
                new ThrowingEnqueueOutboxStore(provider.GetRequiredService<InvitationOutboxStore>()));
        }
        else
        {
            services.AddScoped<IInvitationOutboxStore>(provider =>
                provider.GetRequiredService<InvitationOutboxStore>());
        }
        services.AddScoped<ITransactionalBoundary, SerializableTransactionalBoundary>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(IdentitySecurityTestsHelper.CreateOptions());
        services.AddSingleton<InvitationTokenProtector>();
        services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        services.AddScoped<InvitationIssuanceCore>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPlatformInvitationService, PlatformInvitationService>();
        services.AddScoped<IInvitationAcceptanceService, InvitationAcceptanceService>();
        services.AddScoped<ICampaignInvitationService, CampaignInvitationService>();
        return services.BuildServiceProvider();
    }

    private sealed class ThrowingEnqueueOutboxStore(IInvitationOutboxStore inner) : IInvitationOutboxStore
    {
        public Task EnqueueAsync(
            Guid invitationId,
            string encryptedToken,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("forced enqueue failure");

        public Task<ClaimedOutboxWork?> TryClaimNextAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            inner.TryClaimNextAsync(now, cancellationToken);

        public Task MarkProcessedAsync(
            Guid outboxId,
            string providerMessageId,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            inner.MarkProcessedAsync(outboxId, providerMessageId, now, cancellationToken);

        public Task MarkDiscardedAsync(
            Guid outboxId,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            inner.MarkDiscardedAsync(outboxId, now, cancellationToken);

        public Task MarkFailedAsync(
            Guid outboxId,
            string errorCode,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            inner.MarkFailedAsync(outboxId, errorCode, now, cancellationToken);

        public Task<IReadOnlyDictionary<Guid, InvitationDeliveryStatus>> GetDeliveryStatusesAsync(
            IReadOnlyCollection<Guid> invitationIds,
            CancellationToken cancellationToken) =>
            inner.GetDeliveryStatusesAsync(invitationIds, cancellationToken);
    }
}

internal static class IdentitySecurityTestsHelper
{
    internal static IdentitySecurityOptions CreateOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:BootstrapToken"] = "integration-bootstrap-token-with-32-characters",
                ["Identity:OutboxEncryptionKey"] = "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=",
                ["Frontend:BaseUrl"] = "https://example.com/application/",
            })
            .Build();
        return IdentitySecurityOptionsFactory.FromConfiguration(
            configuration,
            new IntegrationTestHostEnvironment());
    }
}

internal sealed class IntegrationTestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;

    public string ApplicationName { get; set; } = "DndCampaign.Api.Tests";

    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
