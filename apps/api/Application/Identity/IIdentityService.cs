namespace DndCampaign.Api.Application.Identity;

public interface IIdentityService
{
    Task<BootstrapStatus> GetBootstrapStatus(CancellationToken cancellationToken);

    Task<(BootstrapCreationStatus status, IEnumerable<IdentityAccountValidationErrors> errors, UserProfile? user)>
        BootstrapAsync(BootstrapAccountCommand command, CancellationToken cancellationToken);

    Task<LoginOutcome?> LoginAsync(LoginCommand command, CancellationToken cancellationToken);

    Task LogoutAsync(LogoutCommand command, CancellationToken cancellationToken);
}
