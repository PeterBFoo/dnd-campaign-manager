namespace DndCampaign.Api.Api.Contracts;

public sealed record IssueInvitationRequest(string? Email);

public sealed record InvitationResponse(
    Guid Id,
    string Kind,
    string RecipientEmail,
    Guid? CampaignId,
    string Status,
    string DeliveryStatus,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastSentAt);
