using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;

namespace DndCampaign.Api.Application.Invitations;

public sealed class InvitationIssuanceCore(
    IInvitationStore invitations,
    IInvitationOutboxStore outbox,
    ITransactionalBoundary transactions,
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
        IssuedInvitation created = CreateInvitation(kind, normalizedEmail, campaignId, issuedByUserId, now);

        await transactions.ExecuteSerializableAsync(async ct =>
        {
            if (await invitations.HasPendingAsync(kind, campaignId, normalizedEmail, now, ct))
            {
                throw new InvitationConflictException();
            }

            await invitations.AddAsync(created.Invitation, ct);
            await outbox.EnqueueAsync(
                created.Invitation.Id,
                protector.Protect(created.Token),
                now,
                ct);
        }, cancellationToken);

        IdentityTelemetry.InvitationsIssued.Add(
            1,
            new KeyValuePair<string, object?>("invitation.kind", kind.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("invitation.operation", "initial"));
        return InvitationInternalOperations.ToSummary(
            created.Invitation,
            lastSentAt: null,
            InvitationDeliveryStatus.Pending);
    }

    internal async Task<InvitationSummary> ResendAsync(
        Invitation invitation,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!invitation.IsPending(now))
        {
            throw new InvitationStateException("Solo se puede reenviar una invitación pendiente.");
        }

        IssuedInvitation replacement = CreateInvitation(
            invitation.Kind,
            invitation.RecipientEmail,
            invitation.CampaignId,
            invitation.IssuedByUserId,
            now);

        await transactions.ExecuteSerializableAsync(async ct =>
        {
            var recentIssues = await invitations.ListRecentIssueTimesAsync(
                invitation.Kind,
                invitation.CampaignId,
                invitation.RecipientEmail,
                now.AddHours(-24),
                ct);
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
            await invitations.SaveAsync(invitation, ct);
            await invitations.AddAsync(replacement.Invitation, ct);
            await outbox.EnqueueAsync(
                replacement.Invitation.Id,
                protector.Protect(replacement.Token),
                now,
                ct);
        }, cancellationToken);

        IdentityTelemetry.InvitationsIssued.Add(
            1,
            new KeyValuePair<string, object?>(
                "invitation.kind",
                replacement.Invitation.Kind.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("invitation.operation", "resend"));
        return InvitationInternalOperations.ToSummary(
            replacement.Invitation,
            lastSentAt: null,
            InvitationDeliveryStatus.Pending);
    }

    private static IssuedInvitation CreateInvitation(
        InvitationKind kind,
        string recipientEmail,
        Guid? campaignId,
        Guid issuedByUserId,
        DateTimeOffset now) =>
        kind switch
        {
            InvitationKind.Platform => Invitation.IssuePlatform(recipientEmail, issuedByUserId, now),
            InvitationKind.Campaign when campaignId.HasValue =>
                Invitation.IssueCampaign(recipientEmail, campaignId.Value, issuedByUserId, now),
            _ => throw new InvalidOperationException("The invitation kind is not supported."),
        };
}
