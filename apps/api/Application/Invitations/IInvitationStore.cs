using DndCampaign.Api.Domain.Invitations;

namespace DndCampaign.Api.Application.Invitations;

public enum InvitationDeliveryStatus
{
    Pending,
    Sent,
    Discarded,
    Failed,
}

public sealed record InvitationListItem(
    Invitation Invitation,
    DateTimeOffset? LastSentAt);

public interface IInvitationStore
{
    Task<bool> HasPendingAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<Invitation?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    Task<Invitation?> FindByIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken);

    Task<Invitation?> FindByIdAsync(
        Guid invitationId,
        InvitationKind kind,
        Guid? campaignId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DateTimeOffset>> ListRecentIssueTimesAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset since,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InvitationListItem>> ListAsync(
        InvitationKind kind,
        Guid? campaignId,
        CancellationToken cancellationToken);

    Task AddAsync(Invitation invitation, CancellationToken cancellationToken);

    Task SaveAsync(Invitation invitation, CancellationToken cancellationToken);

    Task SaveAllAsync(IReadOnlyCollection<Invitation> invitations, CancellationToken cancellationToken);

    Task MarkSentAsync(Guid invitationId, DateTimeOffset now, CancellationToken cancellationToken);
}
