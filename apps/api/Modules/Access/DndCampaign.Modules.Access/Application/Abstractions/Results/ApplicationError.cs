namespace DndCampaign.Modules.Access.Application.Abstractions.Results;

internal enum ApplicationErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Gone,
    RateLimited,
}

internal sealed record ApplicationError(
    string Code,
    ApplicationErrorType Type,
    string Description,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    DateTimeOffset? RetryAt = null);
