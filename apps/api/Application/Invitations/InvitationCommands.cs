using DndCampaign.Api.Application.Identity;

namespace DndCampaign.Api.Application.Invitations;

public sealed record PreviewInvitationCommand(string? Token);

public sealed record AcceptInvitationCommand(string? Token, string? DisplayName, string? Password);

public sealed record AuthenticatedActor(bool IsAuthenticated, Guid? UserId);

public sealed record InvitationAcceptanceOutcome(
    UserProfile User,
    string? AccessToken,
    DateTimeOffset? ExpiresAt,
    string Kind);

public sealed record ListPlatformInvitationsCommand;

public sealed record ListCampaignInvitationsCommand(Guid CampaignId, Guid RequesterUserId);

public sealed record IssuePlatformInvitationCommand(string Email, Guid IssuedByUserId);

public sealed record IssueCampaignInvitationCommand(string Email, Guid CampaignId, Guid IssuedByUserId);

public sealed record ResendInvitationCommand(Guid InvitationId);

public sealed record ResendCampaignInvitationCommand(
    Guid CampaignId,
    Guid RequesterUserId,
    Guid InvitationId);

public sealed record RevokeInvitationCommand(Guid InvitationId);

public sealed record InvitationSummary(
    Guid Id,
    string Kind,
    string RecipientEmail,
    Guid? CampaignId,
    string Status,
    InvitationDeliveryStatus DeliveryStatus,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastSentAt);

public sealed record InvitationPreviewOutcome(
    string State,
    string? Kind,
    string? RecipientEmail,
    DateTimeOffset? ExpiresAt,
    bool RequiresAuthentication);

public enum ResendInvitationStatus
{
    Resent,
    NotFound,
}

public enum RevokeInvitationStatus
{
    Revoked,
    NotFound,
    Conflict,
}

public enum CampaignAccessStatus
{
    Allowed,
    Forbidden,
}
