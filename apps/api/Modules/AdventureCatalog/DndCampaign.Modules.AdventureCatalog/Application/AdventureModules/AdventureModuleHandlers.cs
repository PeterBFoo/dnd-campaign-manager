using System.Diagnostics;
using DndCampaign.Modules.AdventureCatalog.Application.Abstractions;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;

namespace DndCampaign.Modules.AdventureCatalog.Application.AdventureModules;

internal sealed record AdventureCatalogActor(Guid UserId, bool IsPlatformAdmin);

internal sealed record EditorialProvenanceInput(
    string? OriginKind,
    string? SourceReference,
    string? RightsBasis,
    string? Attribution);

internal sealed record EditorialProvenanceDto(
    string OriginKind,
    string? SourceReference,
    string RightsBasis,
    string? Attribution,
    DateTimeOffset VerifiedAt);

internal sealed record AdventureModuleDto(
    Guid Id,
    string Name,
    string? Description,
    string? CoverUrl,
    EditorialProvenanceDto TextProvenance,
    EditorialProvenanceDto? CoverProvenance,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Version);

internal sealed record ListAdventureModulesQuery(AdventureCatalogActor Actor);

internal sealed record GetAdventureModuleQuery(AdventureCatalogActor Actor, Guid ModuleId);

internal sealed record CreateAdventureModuleCommand(
    AdventureCatalogActor Actor,
    string? Name,
    string? Description,
    EditorialProvenanceInput TextProvenance,
    AdventureModuleCoverUpload? Cover,
    EditorialProvenanceInput? CoverProvenance);

internal sealed record UpdateAdventureModuleCommand(
    AdventureCatalogActor Actor,
    Guid ModuleId,
    string? Name,
    string? Description,
    EditorialProvenanceInput TextProvenance,
    AdventureModuleCoverUpload? Cover,
    EditorialProvenanceInput? CoverProvenance,
    bool RemoveCover,
    long ExpectedVersion);

internal sealed record DeleteAdventureModuleCommand(
    AdventureCatalogActor Actor,
    Guid ModuleId,
    long ExpectedVersion);

internal sealed record GetAdventureModuleCoverQuery(AdventureCatalogActor Actor, Guid ModuleId);

internal sealed class ListAdventureModulesHandler(
    IAdventureModuleRepository modules,
    IAdventureCatalogMetrics metrics)
{
    public async Task<AdventureCatalogResult<IReadOnlyList<AdventureModuleDto>>> HandleAsync(
        ListAdventureModulesQuery query,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            if (!AdventureModuleHandlerSupport.IsAuthorized(query.Actor))
            {
                outcome = "forbidden";
                return AdventureModuleHandlerSupport.Forbidden<IReadOnlyList<AdventureModuleDto>>();
            }

            var result = (await modules.ListAsync(cancellationToken))
                .Select(AdventureModuleHandlerSupport.ToDto)
                .ToArray();
            outcome = "success";
            return AdventureCatalogResult<IReadOnlyList<AdventureModuleDto>>.Success(result);
        }
        finally
        {
            metrics.OperationCompleted("list", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class GetAdventureModuleHandler(
    IAdventureModuleRepository modules,
    IAdventureCatalogMetrics metrics)
{
    public async Task<AdventureCatalogResult<AdventureModuleDto>> HandleAsync(
        GetAdventureModuleQuery query,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            if (!AdventureModuleHandlerSupport.IsAuthorized(query.Actor))
            {
                outcome = "forbidden";
                return AdventureModuleHandlerSupport.Forbidden<AdventureModuleDto>();
            }

            var module = await modules.FindAsync(query.ModuleId, cancellationToken);
            if (module is null)
            {
                outcome = "not_found";
                return AdventureModuleHandlerSupport.NotFound<AdventureModuleDto>();
            }

            outcome = "success";
            return AdventureCatalogResult<AdventureModuleDto>.Success(
                AdventureModuleHandlerSupport.ToDto(module));
        }
        finally
        {
            metrics.OperationCompleted("detail", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class CreateAdventureModuleHandler(
    IAdventureModuleRepository modules,
    IAdventureModuleCoverStore covers,
    IAdventureCatalogMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task<AdventureCatalogResult<AdventureModuleDto>> HandleAsync(
        CreateAdventureModuleCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        string? storedObjectKey = null;
        try
        {
            if (!AdventureModuleHandlerSupport.IsAuthorized(command.Actor))
            {
                outcome = "forbidden";
                return AdventureModuleHandlerSupport.Forbidden<AdventureModuleDto>();
            }

            var now = timeProvider.GetUtcNow();
            var validation = AdventureModuleHandlerSupport.ValidateInputs(
                command.Actor,
                command.Name,
                command.TextProvenance,
                command.Cover,
                command.CoverProvenance,
                now);
            if (!validation.IsSuccess)
            {
                outcome = "validation";
                return AdventureCatalogResult<AdventureModuleDto>.Failure(validation.Error!);
            }

            string nameKey;
            try
            {
                nameKey = AdventureModule.NormalizeNameKey(command.Name!);
            }
            catch (ArgumentException exception)
            {
                outcome = "validation";
                return AdventureModuleHandlerSupport.Validation<AdventureModuleDto>("name", exception.Message);
            }

            if (await modules.NameExistsAsync(nameKey, cancellationToken: cancellationToken))
            {
                outcome = "conflict";
                return AdventureModuleHandlerSupport.NameConflict<AdventureModuleDto>();
            }

            var moduleId = Guid.NewGuid();
            AdventureModuleCover? cover = null;
            if (command.Cover is not null)
            {
                try
                {
                    var stored = await covers.StoreAsync(moduleId, command.Cover, cancellationToken);
                    storedObjectKey = stored.ObjectKey;
                    cover = AdventureModuleCover.Create(stored.ObjectKey, stored.ContentType, stored.SizeBytes);
                }
                catch (AdventureModuleCoverValidationException exception)
                {
                    outcome = "validation";
                    return AdventureModuleHandlerSupport.Validation<AdventureModuleDto>("cover", exception.Message);
                }
            }

            AdventureModule module;
            try
            {
                module = AdventureModule.Create(
                    moduleId,
                    command.Name!,
                    command.Description,
                    validation.Value!.Text,
                    cover,
                    validation.Value.Cover,
                    command.Actor.UserId,
                    now);
            }
            catch (ArgumentException exception)
            {
                outcome = "validation";
                return AdventureModuleHandlerSupport.Validation<AdventureModuleDto>(
                    exception.ParamName ?? "module", exception.Message);
            }

            modules.Add(module);
            try
            {
                await modules.SaveChangesAsync(cancellationToken);
            }
            catch (AdventureModuleNameConflictException)
            {
                outcome = "conflict";
                return AdventureModuleHandlerSupport.NameConflict<AdventureModuleDto>();
            }

            storedObjectKey = null;
            outcome = "success";
            return AdventureCatalogResult<AdventureModuleDto>.Success(
                AdventureModuleHandlerSupport.ToDto(module));
        }
        finally
        {
            if (storedObjectKey is not null)
            {
                await covers.DeleteIfExistsAsync(storedObjectKey, CancellationToken.None);
            }
            metrics.OperationCompleted("create", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class UpdateAdventureModuleHandler(
    IAdventureModuleRepository modules,
    IAdventureModuleCoverStore covers,
    IAdventureCatalogMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task<AdventureCatalogResult<AdventureModuleDto>> HandleAsync(
        UpdateAdventureModuleCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        string? replacementObjectKey = null;
        try
        {
            if (!AdventureModuleHandlerSupport.IsAuthorized(command.Actor))
            {
                outcome = "forbidden";
                return AdventureModuleHandlerSupport.Forbidden<AdventureModuleDto>();
            }

            var module = await modules.FindAsync(command.ModuleId, cancellationToken);
            if (module is null)
            {
                outcome = "not_found";
                return AdventureModuleHandlerSupport.NotFound<AdventureModuleDto>();
            }
            if (module.Version != command.ExpectedVersion)
            {
                outcome = "conflict";
                return AdventureModuleHandlerSupport.VersionConflict<AdventureModuleDto>();
            }
            if (command.Cover is not null && command.RemoveCover)
            {
                outcome = "validation";
                return AdventureModuleHandlerSupport.Validation<AdventureModuleDto>(
                    "cover", "No se puede sustituir y retirar la portada a la vez.");
            }

            var now = timeProvider.GetUtcNow();
            var validation = AdventureModuleHandlerSupport.ValidateInputs(
                command.Actor,
                command.Name,
                command.TextProvenance,
                command.Cover,
                command.CoverProvenance,
                now);
            if (!validation.IsSuccess)
            {
                outcome = "validation";
                return AdventureCatalogResult<AdventureModuleDto>.Failure(validation.Error!);
            }

            string nameKey;
            try
            {
                nameKey = AdventureModule.NormalizeNameKey(command.Name!);
            }
            catch (ArgumentException exception)
            {
                outcome = "validation";
                return AdventureModuleHandlerSupport.Validation<AdventureModuleDto>("name", exception.Message);
            }
            if (await modules.NameExistsAsync(nameKey, module.Id, cancellationToken))
            {
                outcome = "conflict";
                return AdventureModuleHandlerSupport.NameConflict<AdventureModuleDto>();
            }

            AdventureModuleCover? replacement = null;
            if (command.Cover is not null)
            {
                try
                {
                    var stored = await covers.StoreAsync(module.Id, command.Cover, cancellationToken);
                    replacementObjectKey = stored.ObjectKey;
                    replacement = AdventureModuleCover.Create(
                        stored.ObjectKey, stored.ContentType, stored.SizeBytes);
                }
                catch (AdventureModuleCoverValidationException exception)
                {
                    outcome = "validation";
                    return AdventureModuleHandlerSupport.Validation<AdventureModuleDto>("cover", exception.Message);
                }
            }

            var previousCoverKey = module.Cover?.ObjectKey;
            try
            {
                module.Update(
                    command.Name!,
                    command.Description,
                    validation.Value!.Text,
                    replacement,
                    validation.Value.Cover,
                    command.RemoveCover,
                    command.Actor.UserId,
                    now);
                await modules.SaveChangesAsync(cancellationToken);
            }
            catch (AdventureModuleNameConflictException)
            {
                outcome = "conflict";
                return AdventureModuleHandlerSupport.NameConflict<AdventureModuleDto>();
            }
            catch (AdventureModuleConcurrencyException)
            {
                outcome = "conflict";
                return AdventureModuleHandlerSupport.VersionConflict<AdventureModuleDto>();
            }
            catch (ArgumentException exception)
            {
                outcome = "validation";
                return AdventureModuleHandlerSupport.Validation<AdventureModuleDto>(
                    exception.ParamName ?? "module", exception.Message);
            }

            replacementObjectKey = null;
            if (previousCoverKey is not null && (replacement is not null || command.RemoveCover))
            {
                await covers.DeleteIfExistsAsync(previousCoverKey, CancellationToken.None);
            }

            outcome = "success";
            return AdventureCatalogResult<AdventureModuleDto>.Success(
                AdventureModuleHandlerSupport.ToDto(module));
        }
        finally
        {
            if (replacementObjectKey is not null)
            {
                await covers.DeleteIfExistsAsync(replacementObjectKey, CancellationToken.None);
            }
            metrics.OperationCompleted("update", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class DeleteAdventureModuleHandler(
    IAdventureModuleRepository modules,
    IAdventureModuleCoverStore covers,
    IAdventureCatalogMetrics metrics)
{
    public async Task<AdventureCatalogResult<bool>> HandleAsync(
        DeleteAdventureModuleCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            if (!AdventureModuleHandlerSupport.IsAuthorized(command.Actor))
            {
                outcome = "forbidden";
                return AdventureModuleHandlerSupport.Forbidden<bool>();
            }

            var module = await modules.FindAsync(command.ModuleId, cancellationToken);
            if (module is null)
            {
                outcome = "not_found";
                return AdventureModuleHandlerSupport.NotFound<bool>();
            }
            if (module.Version != command.ExpectedVersion)
            {
                outcome = "conflict";
                return AdventureModuleHandlerSupport.VersionConflict<bool>();
            }

            var coverKey = module.Cover?.ObjectKey;
            modules.Remove(module);
            try
            {
                await modules.SaveChangesAsync(cancellationToken);
            }
            catch (AdventureModuleConcurrencyException)
            {
                outcome = "conflict";
                return AdventureModuleHandlerSupport.VersionConflict<bool>();
            }

            if (coverKey is not null)
            {
                await covers.DeleteIfExistsAsync(coverKey, CancellationToken.None);
            }
            outcome = "success";
            return AdventureCatalogResult<bool>.Success(true);
        }
        finally
        {
            metrics.OperationCompleted("delete", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class GetAdventureModuleCoverHandler(
    IAdventureModuleRepository modules,
    IAdventureModuleCoverStore covers,
    IAdventureCatalogMetrics metrics)
{
    public async Task<AdventureCatalogResult<AdventureModuleCoverContent>> HandleAsync(
        GetAdventureModuleCoverQuery query,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            if (!AdventureModuleHandlerSupport.IsAuthenticated(query.Actor))
            {
                outcome = "forbidden";
                return AdventureModuleHandlerSupport.Forbidden<AdventureModuleCoverContent>();
            }
            var module = await modules.FindAsync(query.ModuleId, cancellationToken);
            if (module?.Cover is null)
            {
                outcome = "not_found";
                return AdventureModuleHandlerSupport.NotFound<AdventureModuleCoverContent>();
            }
            var content = await covers.OpenReadAsync(module.Cover.ObjectKey, cancellationToken);
            if (content is null)
            {
                outcome = "not_found";
                return AdventureModuleHandlerSupport.NotFound<AdventureModuleCoverContent>();
            }
            outcome = "success";
            return AdventureCatalogResult<AdventureModuleCoverContent>.Success(content);
        }
        finally
        {
            metrics.OperationCompleted("cover_read", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal static class AdventureModuleHandlerSupport
{
    internal sealed record ValidatedProvenance(
        EditorialProvenance Text,
        EditorialProvenance? Cover);

    public static bool IsAuthorized(AdventureCatalogActor actor) =>
        actor.UserId != Guid.Empty && actor.IsPlatformAdmin;

    public static bool IsAuthenticated(AdventureCatalogActor actor) =>
        actor.UserId != Guid.Empty;

    public static AdventureCatalogResult<ValidatedProvenance> ValidateInputs(
        AdventureCatalogActor actor,
        string? name,
        EditorialProvenanceInput textInput,
        AdventureModuleCoverUpload? cover,
        EditorialProvenanceInput? coverInput,
        DateTimeOffset now)
    {
        try
        {
            _ = AdventureModule.NormalizeNameKey(name ?? string.Empty);
            var text = CreateProvenance(textInput, actor.UserId, now);
            if ((cover is null) != (coverInput is null))
            {
                return Validation<ValidatedProvenance>(
                    "coverProvenance", "La portada y su procedencia deben proporcionarse juntas.");
            }
            var coverProvenance = coverInput is null
                ? null
                : CreateProvenance(coverInput, actor.UserId, now);
            return AdventureCatalogResult<ValidatedProvenance>.Success(
                new ValidatedProvenance(text, coverProvenance));
        }
        catch (ArgumentException exception)
        {
            return Validation<ValidatedProvenance>(exception.ParamName ?? "provenance", exception.Message);
        }
    }

    public static AdventureModuleDto ToDto(AdventureModule module) => new(
        module.Id,
        module.Name,
        module.Description,
        module.Cover is null ? null : $"/api/v1/admin/adventure-modules/{module.Id}/cover",
        ToProvenanceDto(module.TextProvenance),
        module.CoverProvenance is null ? null : ToProvenanceDto(module.CoverProvenance),
        module.CreatedAt,
        module.UpdatedAt,
        module.Version);

    public static AdventureCatalogResult<T> Forbidden<T>() => AdventureCatalogResult<T>.Failure(
        new AdventureCatalogError(
            "adventure_catalog.forbidden",
            AdventureCatalogErrorType.Forbidden,
            "No tienes permiso para administrar módulos de aventura."));

    public static AdventureCatalogResult<T> NotFound<T>() => AdventureCatalogResult<T>.Failure(
        new AdventureCatalogError(
            "adventure_catalog.not_found",
            AdventureCatalogErrorType.NotFound,
            "No se ha encontrado el módulo de aventura."));

    public static AdventureCatalogResult<T> NameConflict<T>() => AdventureCatalogResult<T>.Failure(
        new AdventureCatalogError(
            "adventure_catalog.duplicate_name",
            AdventureCatalogErrorType.Conflict,
            "Ya existe un módulo con un nombre equivalente."));

    public static AdventureCatalogResult<T> VersionConflict<T>() => AdventureCatalogResult<T>.Failure(
        new AdventureCatalogError(
            "adventure_catalog.stale_version",
            AdventureCatalogErrorType.Conflict,
            "El módulo ha cambiado. Recarga la versión vigente antes de continuar."));

    public static AdventureCatalogResult<T> Validation<T>(string field, string description) =>
        AdventureCatalogResult<T>.Failure(new AdventureCatalogError(
            "adventure_catalog.validation",
            AdventureCatalogErrorType.Validation,
            "El módulo de aventura no es válido.",
            new Dictionary<string, string[]> { [field] = [description] }));

    internal static EditorialProvenance CreateProvenance(
        EditorialProvenanceInput input,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (!Enum.TryParse<EditorialOriginKind>(input.OriginKind, true, out var kind)
            || !Enum.IsDefined(kind))
        {
            throw new ArgumentException("El tipo de procedencia no es válido.", "originKind");
        }
        return EditorialProvenance.Create(
            kind,
            input.SourceReference,
            input.RightsBasis ?? string.Empty,
            input.Attribution,
            now,
            actorUserId);
    }

    internal static EditorialProvenanceDto ToProvenanceDto(EditorialProvenance provenance) => new(
        provenance.OriginKind.ToString(),
        provenance.SourceReference,
        provenance.RightsBasis,
        provenance.Attribution,
        provenance.VerifiedAt);
}
