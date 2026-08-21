namespace DndCampaign.Api.Api.Contracts;

public sealed record BootstrapRequest(string? Token, string? Email, string? DisplayName, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record UserResponse(Guid Id, string Email, string DisplayName, bool IsPlatformAdmin);

public sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);

public sealed record BootstrapStatusResponse(string State);
