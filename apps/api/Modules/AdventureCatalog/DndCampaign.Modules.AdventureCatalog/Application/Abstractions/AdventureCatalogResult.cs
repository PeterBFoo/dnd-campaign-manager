namespace DndCampaign.Modules.AdventureCatalog.Application.Abstractions;

internal enum AdventureCatalogErrorType
{
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

internal sealed record AdventureCatalogError(
    string Code,
    AdventureCatalogErrorType Type,
    string Description,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

internal sealed class AdventureCatalogResult<T>
{
    private AdventureCatalogResult(T? value, AdventureCatalogError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public AdventureCatalogError? Error { get; }

    public static AdventureCatalogResult<T> Success(T value) => new(value, null);

    public static AdventureCatalogResult<T> Failure(AdventureCatalogError error) => new(default, error);
}
