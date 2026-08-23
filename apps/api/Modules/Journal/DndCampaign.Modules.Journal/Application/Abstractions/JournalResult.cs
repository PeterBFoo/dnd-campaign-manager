namespace DndCampaign.Modules.Journal.Application.Abstractions;

internal enum JournalErrorType
{
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

internal sealed record JournalError(
    JournalErrorType Type,
    string Code,
    string Description,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

internal sealed class JournalResult<T>
{
    private JournalResult(T? value, JournalError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public JournalError? Error { get; }

    public bool IsSuccess => Error is null;

    public static JournalResult<T> Success(T value) => new(value, null);

    public static JournalResult<T> Failure(JournalError error) => new(default, error);
}
