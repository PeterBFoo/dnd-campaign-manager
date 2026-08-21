using DndCampaign.Api.Application.Identity;

namespace DndCampaign.Api.Application.Invitations;

public sealed record AcceptInvitationResult(
    AcceptInvitationStatus Status,
    IReadOnlyCollection<IdentityAccountValidationErrors> Errors,
    InvitationAcceptanceOutcome? Outcome)
{
    public static AcceptInvitationResult Failure(AcceptInvitationStatus status) =>
        new(status, [], null);

    public static AcceptInvitationResult InvalidCredentials(
        IReadOnlyCollection<IdentityAccountValidationErrors> errors) =>
        new(AcceptInvitationStatus.InvalidCredentials, errors, null);

    public static AcceptInvitationResult Success(InvitationAcceptanceOutcome outcome) =>
        new(AcceptInvitationStatus.Accepted, [], outcome);
}
