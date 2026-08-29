namespace DndCampaign.Modules.Campaigns.Application.Abstractions;

internal enum CampaignErrorType
{
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

internal sealed record CampaignError(
    string Code,
    CampaignErrorType Type,
    string Description,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

internal sealed class CampaignResult<T>
{
    private CampaignResult(T? value, CampaignError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public CampaignError? Error { get; }

    public static CampaignResult<T> Success(T value) => new(value, null);

    public static CampaignResult<T> Failure(CampaignError error) => new(default, error);
}
