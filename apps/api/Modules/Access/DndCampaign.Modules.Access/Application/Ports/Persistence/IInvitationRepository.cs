using DndCampaign.Modules.Access.Domain.Invitations;

namespace DndCampaign.Modules.Access.Application.Ports.Persistence;

internal interface IInvitationRepository
{
    Task<Invitation?> FindByIdAsync(Guid invitationId, CancellationToken cancellationToken = default);

    Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<bool> HasPendingAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DateTimeOffset>> ListIssueTimesAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    void Add(Invitation invitation);
}

internal interface IInvitationOutboxRepository
{
    void Add(Guid invitationId, string protectedToken, DateTimeOffset createdAt);
}

internal interface IInvitationReadStore
{
    Task<InvitationPreviewDto?> PreviewAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvitationListItemDto>> ListAsync(
        InvitationKind kind,
        Guid? campaignId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

internal sealed record InvitationPreviewDto(
    string State,
    string Kind,
    string MaskedRecipientEmail,
    DateTimeOffset ExpiresAt,
    bool RequiresAuthentication);

internal sealed record InvitationListItemDto(
    Guid Id,
    string Kind,
    string RecipientEmail,
    Guid? CampaignId,
    string Status,
    string DeliveryStatus,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastSentAt);
