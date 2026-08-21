namespace DndCampaign.Modules.Access.Application.Abstractions.Results;

internal sealed class Result<TValue>
{
    private Result(TValue value)
    {
        Value = value;
    }

    private Result(ApplicationError error)
    {
        Error = error;
    }

    public TValue? Value { get; }

    public ApplicationError? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result<TValue> Success(TValue value) => new(value);

    public static Result<TValue> Failure(ApplicationError error) => new(error);
}
