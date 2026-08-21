namespace DndCampaign.Api.Application.Identity;

public sealed record BootstrapAccountCommand(string? Token, string? Email, string? DisplayName, string? Password);

public sealed record LoginCommand(string? Email, string? Password);

public sealed record LogoutCommand(Guid SessionId);

public sealed record UserProfile(Guid Id, string Email, string DisplayName, bool IsPlatformAdmin);

public sealed record LoginOutcome(string AccessToken, DateTimeOffset ExpiresAt, UserProfile User);
