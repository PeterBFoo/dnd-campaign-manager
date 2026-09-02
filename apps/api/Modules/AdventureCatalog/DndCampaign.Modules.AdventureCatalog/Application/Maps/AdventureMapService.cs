using DndCampaign.Modules.AdventureCatalog.Application.Abstractions;
using DndCampaign.Modules.AdventureCatalog.Application.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Contracts.Campaigns;
using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;
using DndCampaign.Modules.AdventureCatalog.Domain.Maps;

namespace DndCampaign.Modules.AdventureCatalog.Application.Maps;

internal sealed record AdventureMapChapterDto(Guid Id, string Name, int Position);
internal sealed record AdventureMapDto(Guid Id, Guid ModuleId, string Name, string? Description, bool HasImage, string? ImageUrl, int? Width, int? Height, EditorialProvenanceDto? ImageProvenance, IReadOnlyList<AdventureMapChapterDto> Chapters, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version);
internal sealed record AdventureMapImageUploadInput(AdventureMapImageUpload Upload, EditorialProvenanceInput Provenance, long ExpectedVersion);

internal sealed class AdventureMapService(
    IAdventureMapRepository maps,
    IAdventureLocationRepository locations,
    IAdventureMapImageStore images,
    ICampaignAdventureContext campaigns,
    IAdventureCatalogMetrics metrics,
    TimeProvider time)
{
    public async Task<AdventureCatalogResult<IReadOnlyList<AdventureMapDto>>> ListAdminAsync(AdventureCatalogActor actor, Guid moduleId, CancellationToken ct) =>
        !IsAdmin(actor) ? Forbidden<IReadOnlyList<AdventureMapDto>>() : await ListCoreAsync(moduleId, null, ct);

    public async Task<AdventureCatalogResult<IReadOnlyList<AdventureMapDto>>> ListCampaignAsync(Guid userId, Guid campaignId, CancellationToken ct)
    {
        var access = await campaigns.ResolveAsync(campaignId, userId, ct);
        if (!access.Exists || !access.IsDm || access.AdventureModuleId is null) return Forbidden<IReadOnlyList<AdventureMapDto>>();
        return await ListCoreAsync(access.AdventureModuleId.Value, campaignId, ct);
    }

    public async Task<AdventureCatalogResult<AdventureMapDto>> GetAdminAsync(AdventureCatalogActor actor, Guid moduleId, Guid mapId, CancellationToken ct) =>
        !IsAdmin(actor) ? Forbidden<AdventureMapDto>() : await GetCoreAsync(moduleId, mapId, null, ct);

    public async Task<AdventureCatalogResult<AdventureMapDto>> GetCampaignAsync(Guid userId, Guid campaignId, Guid mapId, CancellationToken ct)
    {
        var access = await campaigns.ResolveAsync(campaignId, userId, ct);
        if (!access.Exists || !access.IsDm || access.AdventureModuleId is null) return Forbidden<AdventureMapDto>();
        return await GetCoreAsync(access.AdventureModuleId.Value, mapId, campaignId, ct);
    }

    public async Task<AdventureCatalogResult<AdventureMapDto>> CreateAsync(AdventureCatalogActor actor, Guid moduleId, string? name, string? description, CancellationToken ct)
    {
        if (!IsAdmin(actor)) return Forbidden<AdventureMapDto>();
        if (!await maps.ModuleExistsAsync(moduleId, ct)) return NotFound<AdventureMapDto>();
        try
        {
            var map = AdventureMap.Create(Guid.NewGuid(), moduleId, name!, description, actor.UserId, time.GetUtcNow());
            maps.Add(map); await maps.SaveChangesAsync(ct); metrics.OperationCompleted("map_create", "success", 0);
            return AdventureCatalogResult<AdventureMapDto>.Success(ToDto(map, [], null));
        }
        catch (ArgumentException ex) { return Validation<AdventureMapDto>(ex.ParamName ?? "map", ex.Message); }
    }

    public async Task<AdventureCatalogResult<AdventureMapDto>> UpdateAsync(AdventureCatalogActor actor, Guid moduleId, Guid mapId, string? name, string? description, long expectedVersion, CancellationToken ct)
    {
        if (!IsAdmin(actor)) return Forbidden<AdventureMapDto>();
        var map = await maps.FindAsync(moduleId, mapId, cancellationToken: ct);
        if (map is null) return NotFound<AdventureMapDto>();
        if (map.Version != expectedVersion) return Conflict<AdventureMapDto>();
        try { map.Update(name!, description, actor.UserId, time.GetUtcNow()); await maps.SaveChangesAsync(ct); return await GetCoreAsync(moduleId, mapId, null, ct); }
        catch (ArgumentException ex) { return Validation<AdventureMapDto>(ex.ParamName ?? "map", ex.Message); }
        catch (AdventureMapConcurrencyException) { return Conflict<AdventureMapDto>(); }
    }

    public async Task<AdventureCatalogResult<bool>> DeleteAsync(AdventureCatalogActor actor, Guid moduleId, Guid mapId, long expectedVersion, CancellationToken ct)
    {
        if (!IsAdmin(actor)) return Forbidden<bool>();
        var map = await maps.FindAsync(moduleId, mapId, cancellationToken: ct);
        if (map is null) return NotFound<bool>();
        if (map.Version != expectedVersion) return Conflict<bool>();
        var key = map.Image?.ObjectKey;
        await locations.ClearMapDependenciesAsync(moduleId, mapId, actor.UserId, time.GetUtcNow(), ct);
        maps.Remove(map);
        try { await maps.SaveChangesAsync(ct); if (key is not null) await images.DeleteIfExistsAsync(key, CancellationToken.None); return AdventureCatalogResult<bool>.Success(true); }
        catch (AdventureMapConcurrencyException) { return Conflict<bool>(); }
    }

    public async Task<AdventureCatalogResult<AdventureMapDto>> PutImageAsync(AdventureCatalogActor actor, Guid moduleId, Guid mapId, AdventureMapImageUploadInput input, CancellationToken ct)
    {
        if (!IsAdmin(actor)) return Forbidden<AdventureMapDto>();
        var map = await maps.FindAsync(moduleId, mapId, cancellationToken: ct);
        if (map is null) return NotFound<AdventureMapDto>();
        if (map.Version != input.ExpectedVersion) return Conflict<AdventureMapDto>();
        EditorialProvenance provenance;
        try { provenance = ParseProvenance(input.Provenance, actor.UserId); }
        catch (ArgumentException ex) { return Validation<AdventureMapDto>("provenance", ex.Message); }
        StoredAdventureMapImage stored;
        try { stored = await images.StoreAsync(moduleId, mapId, input.Upload, ct); }
        catch (AdventureMapImageValidationException ex) { return Validation<AdventureMapDto>("image", ex.Message); }
        var previous = map.Image?.ObjectKey;
        try
        {
            map.SetImage(AdventureMapImage.Create(stored.ObjectKey, stored.ContentType, stored.SizeBytes, stored.Width, stored.Height), provenance, actor.UserId, time.GetUtcNow());
            await maps.SaveChangesAsync(ct);
        }
        catch (AdventureMapConcurrencyException) { await images.DeleteIfExistsAsync(stored.ObjectKey, CancellationToken.None); return Conflict<AdventureMapDto>(); }
        catch { await images.DeleteIfExistsAsync(stored.ObjectKey, CancellationToken.None); throw; }
        if (previous is not null) await images.DeleteIfExistsAsync(previous, CancellationToken.None);
        return await GetCoreAsync(moduleId, mapId, null, ct);
    }

    public async Task<AdventureCatalogResult<AdventureMapDto>> RemoveImageAsync(AdventureCatalogActor actor, Guid moduleId, Guid mapId, long expectedVersion, CancellationToken ct)
    {
        if (!IsAdmin(actor)) return Forbidden<AdventureMapDto>();
        var map = await maps.FindAsync(moduleId, mapId, cancellationToken: ct);
        if (map is null) return NotFound<AdventureMapDto>();
        if (map.Version != expectedVersion) return Conflict<AdventureMapDto>();
        var key = map.Image?.ObjectKey;
        if (key is not null) { map.RemoveImage(actor.UserId, time.GetUtcNow()); await maps.SaveChangesAsync(ct); await images.DeleteIfExistsAsync(key, CancellationToken.None); }
        return await GetCoreAsync(moduleId, mapId, null, ct);
    }

    public async Task<AdventureCatalogResult<AdventureMapDto>> SetChapterAsync(AdventureCatalogActor actor, Guid moduleId, Guid mapId, Guid chapterId, long expectedVersion, bool add, CancellationToken ct)
    {
        if (!IsAdmin(actor)) return Forbidden<AdventureMapDto>();
        var map = await maps.FindAsync(moduleId, mapId, cancellationToken: ct);
        if (map is null || !await maps.ChapterExistsAsync(moduleId, chapterId, ct)) return NotFound<AdventureMapDto>();
        if (map.Version != expectedVersion) return Conflict<AdventureMapDto>();
        var changed = add ? map.AddChapter(chapterId, actor.UserId, time.GetUtcNow()) : map.RemoveChapter(chapterId, actor.UserId, time.GetUtcNow());
        if (changed) await maps.SaveChangesAsync(ct);
        return await GetCoreAsync(moduleId, mapId, null, ct);
    }

    public async Task<AdventureCatalogResult<AdventureMapImageContent>> OpenImageAdminAsync(AdventureCatalogActor actor, Guid moduleId, Guid mapId, CancellationToken ct) =>
        !IsAdmin(actor) ? Forbidden<AdventureMapImageContent>() : await OpenImageCoreAsync(moduleId, mapId, ct);

    public async Task<AdventureCatalogResult<AdventureMapImageContent>> OpenImageCampaignAsync(Guid userId, Guid campaignId, Guid mapId, CancellationToken ct)
    {
        var access = await campaigns.ResolveAsync(campaignId, userId, ct);
        return !access.Exists || !access.IsDm || access.AdventureModuleId is null
            ? Forbidden<AdventureMapImageContent>() : await OpenImageCoreAsync(access.AdventureModuleId.Value, mapId, ct);
    }

    public async Task<AdventureCatalogResult<IReadOnlyList<AdventureMapChapterDto>>> ChaptersAsync(AdventureCatalogActor actor, Guid moduleId, CancellationToken ct)
    {
        if (!IsAdmin(actor)) return Forbidden<IReadOnlyList<AdventureMapChapterDto>>();
        var chapters = await maps.ListChaptersAsync(moduleId, ct);
        return AdventureCatalogResult<IReadOnlyList<AdventureMapChapterDto>>.Success(chapters.Select(ToChapter).ToArray());
    }

    private async Task<AdventureCatalogResult<IReadOnlyList<AdventureMapDto>>> ListCoreAsync(Guid moduleId, Guid? campaignId, CancellationToken ct)
    {
        if (!await maps.ModuleExistsAsync(moduleId, ct)) return NotFound<IReadOnlyList<AdventureMapDto>>();
        var chapters = (await maps.ListChaptersAsync(moduleId, ct)).ToDictionary(item => item.Id);
        var result = (await maps.ListAsync(moduleId, ct)).Select(map => ToDto(map, Resolve(map, chapters), campaignId)).ToArray();
        return AdventureCatalogResult<IReadOnlyList<AdventureMapDto>>.Success(result);
    }

    private async Task<AdventureCatalogResult<AdventureMapDto>> GetCoreAsync(Guid moduleId, Guid mapId, Guid? campaignId, CancellationToken ct)
    {
        var map = await maps.FindAsync(moduleId, mapId, false, ct); if (map is null) return NotFound<AdventureMapDto>();
        var chapters = (await maps.ListChaptersAsync(moduleId, ct)).ToDictionary(item => item.Id);
        return AdventureCatalogResult<AdventureMapDto>.Success(ToDto(map, Resolve(map, chapters), campaignId));
    }

    private async Task<AdventureCatalogResult<AdventureMapImageContent>> OpenImageCoreAsync(Guid moduleId, Guid mapId, CancellationToken ct)
    {
        var map = await maps.FindAsync(moduleId, mapId, false, ct); if (map?.Image is null) return NotFound<AdventureMapImageContent>();
        var content = await images.OpenReadAsync(map.Image.ObjectKey, ct); return content is null ? NotFound<AdventureMapImageContent>() : AdventureCatalogResult<AdventureMapImageContent>.Success(content);
    }

    private EditorialProvenance ParseProvenance(EditorialProvenanceInput input, Guid actorId)
    {
        if (!Enum.TryParse<EditorialOriginKind>(input.OriginKind, true, out var kind)) throw new ArgumentException("La procedencia es obligatoria.");
        return EditorialProvenance.Create(kind, input.SourceReference, input.RightsBasis ?? string.Empty, input.Attribution, time.GetUtcNow(), actorId);
    }

    private static IReadOnlyList<AdventureMapChapterDto> Resolve(AdventureMap map, IReadOnlyDictionary<Guid, AdventureChapter> chapters) => map.Chapters.Where(link => chapters.ContainsKey(link.ChapterId)).Select(link => ToChapter(chapters[link.ChapterId])).OrderBy(item => item.Position).ToArray();
    private static AdventureMapChapterDto ToChapter(AdventureChapter chapter) => new(chapter.Id, chapter.Name, chapter.Position);
    private static AdventureMapDto ToDto(AdventureMap map, IReadOnlyList<AdventureMapChapterDto> chapters, Guid? campaignId) => new(map.Id, map.ModuleId, map.Name, map.Description, map.Image is not null, map.Image is null ? null : campaignId is null ? $"/api/v1/admin/adventure-modules/{map.ModuleId}/maps/{map.Id}/image" : $"/api/v1/campaigns/{campaignId}/adventure/maps/{map.Id}/image", map.Image?.Width, map.Image?.Height, campaignId is not null || map.ImageProvenance is null ? null : new(map.ImageProvenance.OriginKind.ToString(), map.ImageProvenance.SourceReference, map.ImageProvenance.RightsBasis, map.ImageProvenance.Attribution, map.ImageProvenance.VerifiedAt), chapters, map.CreatedAt, map.UpdatedAt, map.Version);
    private static bool IsAdmin(AdventureCatalogActor actor) => actor.UserId != Guid.Empty && actor.IsPlatformAdmin;
    private static AdventureCatalogResult<T> Forbidden<T>() => AdventureCatalogResult<T>.Failure(new("forbidden", AdventureCatalogErrorType.Forbidden, "No autorizado."));
    private static AdventureCatalogResult<T> NotFound<T>() => AdventureCatalogResult<T>.Failure(new("not_found", AdventureCatalogErrorType.NotFound, "No encontrado."));
    private static AdventureCatalogResult<T> Conflict<T>() => AdventureCatalogResult<T>.Failure(new("version_conflict", AdventureCatalogErrorType.Conflict, "La versión ha cambiado."));
    private static AdventureCatalogResult<T> Validation<T>(string field, string message) => AdventureCatalogResult<T>.Failure(new("validation", AdventureCatalogErrorType.Validation, message, new Dictionary<string, string[]> { [field] = [message] }));
}
