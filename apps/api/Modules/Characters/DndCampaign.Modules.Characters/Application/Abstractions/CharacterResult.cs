namespace DndCampaign.Modules.Characters.Application.Abstractions;

internal enum CharacterErrorType
{
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

internal sealed record CharacterError(
    string Code,
    CharacterErrorType Type,
    string Description,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

internal sealed class CharacterResult<T>
{
    private CharacterResult(T? value, CharacterError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public CharacterError? Error { get; }

    public static CharacterResult<T> Success(T value) => new(value, null);

    public static CharacterResult<T> Failure(CharacterError error) => new(default, error);
}
