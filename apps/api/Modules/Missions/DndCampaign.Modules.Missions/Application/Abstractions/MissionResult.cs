namespace DndCampaign.Modules.Missions.Application.Abstractions;

internal enum MissionErrorType
{
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

internal sealed record MissionError(
    MissionErrorType Type,
    string Code,
    string Description,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

internal sealed class MissionResult<T>
{
    private MissionResult(T? value, MissionError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public MissionError? Error { get; }

    public bool IsSuccess => Error is null;

    public static MissionResult<T> Success(T value) => new(value, null);

    public static MissionResult<T> Failure(MissionError error) => new(default, error);
}
