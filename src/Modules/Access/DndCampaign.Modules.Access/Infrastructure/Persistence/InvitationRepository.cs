using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Domain.Invitations;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class InvitationRepository(AccessDbContext database)
    : IInvitationRepository, IInvitationReadStore
{
    public Task<Invitation?> FindByIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default) =>
        database.Invitations.SingleOrDefaultAsync(
            invitation => invitation.Id == invitationId,
            cancellationToken);

    public Task<Invitation?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        database.Invitations.SingleOrDefaultAsync(
            invitation => invitation.TokenHash == tokenHash,
            cancellationToken);

    public Task<bool> HasPendingAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        database.Invitations.AnyAsync(invitation =>
            invitation.Kind == kind
            && invitation.CampaignId == campaignId
            && invitation.RecipientEmail == recipientEmail
            && invitation.Status == InvitationStatus.Pending
            && invitation.ExpiresAt > now,
            cancellationToken);

    public async Task<IReadOnlyList<DateTimeOffset>> ListIssueTimesAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        await database.Invitations
            .Where(invitation =>
                invitation.Kind == kind
                && invitation.CampaignId == campaignId
                && invitation.RecipientEmail == recipientEmail
                && invitation.IssuedAt >= since)
            .Select(invitation => invitation.IssuedAt)
            .ToListAsync(cancellationToken);

    public void Add(Invitation invitation) => database.Invitations.Add(invitation);

    public async Task<InvitationPreviewDto?> PreviewAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var invitation = await database.Invitations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);
        if (invitation is null)
        {
            return null;
        }

        var state = invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt <= now
            ? "expired"
            : invitation.Status.ToString().ToLowerInvariant();
        var requiresAuthentication = state == "pending" && await database.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == invitation.RecipientEmail, cancellationToken);
        return new InvitationPreviewDto(
            state == "pending" ? "valid" : state,
            invitation.Kind.ToString().ToLowerInvariant(),
            MaskEmail(invitation.RecipientEmail),
            invitation.ExpiresAt,
            requiresAuthentication);
    }

    public async Task<IReadOnlyList<InvitationListItemDto>> ListAsync(
        InvitationKind kind,
        Guid? campaignId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var invitations = await database.Invitations
            .AsNoTracking()
            .Where(invitation => invitation.Kind == kind && invitation.CampaignId == campaignId)
            .OrderByDescending(invitation => invitation.IssuedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        var ids = invitations.Select(invitation => invitation.Id).ToArray();
        var deliveries = await database.InvitationOutbox
            .AsNoTracking()
            .Where(message => ids.Contains(message.InvitationId))
            .ToListAsync(cancellationToken);
        return invitations.Select(invitation =>
        {
            var delivery = deliveries
                .Where(message => message.InvitationId == invitation.Id)
                .OrderByDescending(message => message.CreatedAt)
                .FirstOrDefault();
            var deliveryStatus = delivery switch
            {
                { ProcessedAt: not null, ProviderMessageId: not "discarded" } => "sent",
                { ProcessedAt: not null, ProviderMessageId: "discarded" } => "discarded",
                { Attempts: >= 5 } => "failed",
                _ => "pending",
            };
            var status = invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt <= now
                ? "expired"
                : invitation.Status.ToString().ToLowerInvariant();
            return new InvitationListItemDto(
                invitation.Id,
                invitation.Kind.ToString().ToLowerInvariant(),
                invitation.RecipientEmail,
                invitation.CampaignId,
                status,
                deliveryStatus,
                invitation.IssuedAt,
                invitation.ExpiresAt,
                invitation.LastSentAt);
        }).ToArray();
    }

    private static string MaskEmail(string email)
    {
        var separator = email.IndexOf('@', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return "***";
        }

        var local = email[..separator];
        var visible = local[..Math.Min(2, local.Length)];
        return $"{visible}***{email[separator..]}";
    }
}
