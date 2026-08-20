namespace DndCampaign.Api.Application.Invitations;

public enum AcceptInvitationStatus
{
    NotFound,
    AlreadyAccepted,
    Unauthorized,
    Forbidden,
    InvalidCredentials,
    Accepted,
    Expired
}