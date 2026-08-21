namespace DndCampaign.Api.Application.Invitations;

public sealed class InvitationConflictException : Exception;

public sealed class InvitationStateException(string message) : Exception(message);

public sealed class InvitationRateLimitException(DateTimeOffset retryAt) : Exception
{
    public DateTimeOffset RetryAt { get; } = retryAt;
}

public sealed class InvitationEmailValidationException(string message) : Exception(message);
