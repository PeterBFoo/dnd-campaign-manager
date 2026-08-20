using DndCampaign.Api.Api;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;

public sealed record AcceptInvitationResult(
    AcceptInvitationStatus Status,
    IReadOnlyCollection<IdentityAccountValidationErrors> Errors,
    InvitationAcceptanceResponse? Response)
{
    public static AcceptInvitationResult Failure(
        AcceptInvitationStatus status)
    {
        return new AcceptInvitationResult(
            status,
            [],
            null);
    }

    public static AcceptInvitationResult InvalidCredentials(
        IReadOnlyCollection<IdentityAccountValidationErrors> errors)
    {
        return new AcceptInvitationResult(
            AcceptInvitationStatus.InvalidCredentials,
            errors,
            null);
    }

    public static AcceptInvitationResult Success(
        InvitationAcceptanceResponse response)
    {
        return new AcceptInvitationResult(
            AcceptInvitationStatus.Accepted,
            [],
            response);
    }
}