namespace DndCampaign.Modules.Combat.Application.Abstractions;

internal enum CombatErrorType
{
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

internal sealed record CombatError(
    CombatErrorType Type,
    string Code,
    string Description,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

internal sealed class CombatResult<T>
{
    private CombatResult(T? value, CombatError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public CombatError? Error { get; }

    public bool IsSuccess => Error is null;

    public static CombatResult<T> Success(T value) => new(value, null);

    public static CombatResult<T> Failure(CombatError error) => new(default, error);
}
