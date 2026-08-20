namespace DndCampaign.Api.Application.Identity;

public enum BootstrapStatus
{
    Completed,
    Required,
}

public enum BootstrapCreationStatus
{
    InvalidBootstrapToken,
    InvalidCredentials,
    InitialRegistrationClosed,
    Created
}

public enum IdentityAccountValidationErrors
{
    Email,
    DisplayName,
    PasswordTooShortOrTooLong,
    PasswordRequiresLowerCase,
    PasswordRequiresUpperCase,
    PasswordRequiresNumber,
    PasswordRequiresSymbol,
}
