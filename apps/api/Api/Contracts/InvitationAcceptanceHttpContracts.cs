namespace DndCampaign.Api.Api.Contracts;

public sealed record InvitationTokenRequest(string? Token);

public sealed record AcceptInvitationRequest(string? Token, string? DisplayName, string? Password);

public sealed record InvitationPreviewResponse(
    string State,
    string? Kind,
    string? RecipientEmail,
    DateTimeOffset? ExpiresAt,
    bool RequiresAuthentication);

public sealed record InvitationAcceptanceResponse(
    UserResponse User,
    string? AccessToken,
    DateTimeOffset? ExpiresAt,
    string Kind);
