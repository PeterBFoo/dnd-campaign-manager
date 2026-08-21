using System.Data;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Persistence;
using DndCampaign.Api.Infrastructure.Observability;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Application.Invitations;

internal sealed class InvitationIssuanceCore(
    CampaignDbContext database,
    InvitationTokenProtector protector,
    TimeProvider timeProvider)
{
    internal async Task<InvitationSummary> IssueAsync(
        InvitationKind kind,
        string recipientEmail,
        Guid? campaignId,
        Guid issuedByUserId,
        CancellationToken cancellationToken)
    {
        string normalizedEmail;
        try
        {
            normalizedEmail = UserAccount.NormalizeEmail(recipientEmail);
        }
        catch (ArgumentException exception)
        {
            throw new InvitationEmailValidationException(exception.Message);
        }
        var now = timeProvider.GetUtcNow();
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var alreadyPending = await database.Invitations.AnyAsync(
            invitation =>
                invitation.Kind == kind
                && invitation.CampaignId == campaignId
                && invitation.RecipientEmail == normalizedEmail
                && invitation.Status == InvitationStatus.Pending
                && invitation.ExpiresAt > now,
            cancellationToken);
        if (alreadyPending)
        {
            throw new InvitationConflictException();
        }

        var created = CreateInvitation(kind, normalizedEmail, campaignId, issuedByUserId, now);
        database.Invitations.Add(created.Record);
        database.InvitationOutbox.Add(created.Outbox);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        IdentityTelemetry.InvitationsIssued.Add(
            1,
            new KeyValuePair<string, object?>("invitation.kind", kind.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("invitation.operation", "initial"));
        return InvitationInternalOperations.ToSummary(created.Record, "pending");
    }

    internal async Task<InvitationSummary> ResendAsync(
        InvitationRecord invitation,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!invitation.IsPending(now))
        {
            throw new InvitationStateException("Solo se puede reenviar una invitación pendiente.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var recentIssues = await database.Invitations
            .Where(candidate =>
                candidate.Kind == invitation.Kind
                && candidate.CampaignId == invitation.CampaignId
                && candidate.RecipientEmail == invitation.RecipientEmail
                && candidate.IssuedAt >= now.AddHours(-24))
            .Select(candidate => candidate.IssuedAt)
            .ToListAsync(cancellationToken);
        var mostRecentIssue = recentIssues.Count > 0 ? recentIssues.Max() : invitation.IssuedAt;
        var nextAllowedAt = mostRecentIssue.AddMinutes(15);
        if (now < nextAllowedAt)
        {
            throw new InvitationRateLimitException(nextAllowedAt);
        }

        if (recentIssues.Count >= 5)
        {
            throw new InvitationRateLimitException(recentIssues.Min().AddHours(24));
        }

        invitation.Revoke(now);
        var replacement = CreateInvitation(
            invitation.Kind,
            invitation.RecipientEmail,
            invitation.CampaignId,
            invitation.IssuedByUserId,
            now);
        database.Invitations.Add(replacement.Record);
        database.InvitationOutbox.Add(replacement.Outbox);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        IdentityTelemetry.InvitationsIssued.Add(
            1,
            new KeyValuePair<string, object?>(
                "invitation.kind",
                replacement.Record.Kind.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("invitation.operation", "resend"));
        return InvitationInternalOperations.ToSummary(replacement.Record, "pending");
    }

    private (InvitationRecord Record, InvitationOutboxMessage Outbox) CreateInvitation(
        InvitationKind kind,
        string recipientEmail,
        Guid? campaignId,
        Guid issuedByUserId,
        DateTimeOffset now)
    {
        var issued = kind switch
        {
            InvitationKind.Platform => Invitation.IssuePlatform(recipientEmail, now),
            InvitationKind.Campaign when campaignId.HasValue =>
                Invitation.IssueCampaign(recipientEmail, campaignId.Value, now),
            _ => throw new InvalidOperationException("The invitation kind is not supported."),
        };
        var record = InvitationRecord.FromIssued(issued, issuedByUserId);
        var outbox = InvitationOutboxMessage.Create(
            record.Id,
            protector.Protect(issued.Token),
            now);
        return (record, outbox);
    }
}
