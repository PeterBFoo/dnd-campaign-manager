using System.Diagnostics;
using DndCampaign.Modules.AdventureCatalog.Application.Abstractions;
using DndCampaign.Modules.AdventureCatalog.Application.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Contracts.Campaigns;
using DndCampaign.Modules.AdventureCatalog.Domain.Locations;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;
using DndCampaign.Modules.AdventureCatalog.Domain.Maps;

namespace DndCampaign.Modules.AdventureCatalog.Application.Locations;

internal sealed record LocationMapDto(Guid Id, string Name, bool HasImage, string? ImageUrl, int? Width, int? Height);
internal sealed record LocationChapterDto(Guid Id, string Name, int Position);
internal sealed record PointOfInterestDto(Guid Id, string Name, string? Description, decimal? X, decimal? Y, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt, long? Version);
internal sealed record LocationPlacementDto(Guid MapId, decimal X, decimal Y);
internal sealed record AdventureLocationDto(Guid Id, Guid ModuleId, string Name, string? Description, Guid? DetailMapId, LocationMapDto? DetailMap,
    IReadOnlyList<PointOfInterestDto> PointsOfInterest, IReadOnlyList<LocationPlacementDto> Placements, IReadOnlyList<LocationChapterDto> Chapters,
    DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt, long? Version);
internal sealed record LocationWrite(Guid UserId, bool IsAdmin, Guid ModuleId, Guid? LocationId, string? Name, string? Description, long? ExpectedVersion);
internal sealed record PointWrite(Guid UserId, bool IsAdmin, Guid ModuleId, Guid LocationId, Guid? PointId, string? Name, string? Description, decimal? X, decimal? Y, long ExpectedVersion);

internal sealed class AdventureLocationService(
    IAdventureLocationRepository locations,
    ICampaignAdventureContext campaigns,
    IAdventureCatalogMetrics metrics,
    TimeProvider time)
{
    public Task<AdventureCatalogResult<IReadOnlyList<AdventureLocationDto>>> ListAdminAsync(AdventureCatalogActor actor, Guid moduleId, CancellationToken ct) =>
        !Authorized(actor) ? Task.FromResult(Forbidden<IReadOnlyList<AdventureLocationDto>>()) : ListCoreAsync(moduleId, null, true, ct);

    public async Task<AdventureCatalogResult<IReadOnlyList<AdventureLocationDto>>> ListCampaignAsync(Guid userId, Guid campaignId, CancellationToken ct)
    {
        var access = await campaigns.ResolveAsync(campaignId, userId, ct);
        if (!access.Exists || !access.IsDm || access.AdventureModuleId is null) return access.Exists && !access.IsDm ? Forbidden<IReadOnlyList<AdventureLocationDto>>() : NotFound<IReadOnlyList<AdventureLocationDto>>();
        return await ListCoreAsync(access.AdventureModuleId.Value, campaignId, false, ct);
    }

    public async Task<AdventureCatalogResult<AdventureLocationDto>> GetAdminAsync(AdventureCatalogActor actor, Guid moduleId, Guid locationId, CancellationToken ct) =>
        !Authorized(actor) ? Forbidden<AdventureLocationDto>() : await GetCoreAsync(moduleId, locationId, null, true, ct);

    public async Task<AdventureCatalogResult<AdventureLocationDto>> GetCampaignAsync(Guid userId, Guid campaignId, Guid locationId, CancellationToken ct)
    {
        var access = await campaigns.ResolveAsync(campaignId, userId, ct);
        if (!access.Exists || access.AdventureModuleId is null) return NotFound<AdventureLocationDto>();
        if (!access.IsDm) return Forbidden<AdventureLocationDto>();
        return await GetCoreAsync(access.AdventureModuleId.Value, locationId, campaignId, false, ct);
    }

    public async Task<AdventureCatalogResult<AdventureLocationDto>> CreateAsync(LocationWrite command, CancellationToken ct)
    {
        if (!Authorized(command.UserId, command.IsAdmin)) return Forbidden<AdventureLocationDto>();
        if (!await locations.ModuleExistsAsync(command.ModuleId, ct)) return NotFound<AdventureLocationDto>();
        try
        {
            var now = time.GetUtcNow();
            var location = AdventureLocation.Create(Guid.NewGuid(), command.ModuleId, command.Name ?? "", command.Description, command.UserId, now);
            locations.Add(location); await locations.SaveChangesAsync(ct); metrics.OperationCompleted("location_create", "success", 0);
            return await GetCoreAsync(command.ModuleId, location.Id, null, true, ct);
        }
        catch (ArgumentException ex) { return Validation<AdventureLocationDto>(ex.ParamName ?? "location", ex.Message); }
        catch (AdventureLocationRelationConflictException) { return Conflict<AdventureLocationDto>(); }
    }

    public async Task<AdventureCatalogResult<AdventureLocationDto>> UpdateAsync(LocationWrite command, CancellationToken ct)
    {
        if (!Authorized(command.UserId, command.IsAdmin)) return Forbidden<AdventureLocationDto>();
        var location = await locations.FindAsync(command.ModuleId, command.LocationId!.Value, true, ct);
        if (location is null) return NotFound<AdventureLocationDto>();
        if (location.Version != command.ExpectedVersion) return Conflict<AdventureLocationDto>();
        try { location.Update(command.Name ?? "", command.Description, command.UserId, time.GetUtcNow()); await locations.SaveChangesAsync(ct); metrics.OperationCompleted("location_update", "success", 0); return await GetCoreAsync(command.ModuleId, location.Id, null, true, ct); }
        catch (ArgumentException ex) { return Validation<AdventureLocationDto>(ex.ParamName ?? "location", ex.Message); }
        catch (AdventureLocationConcurrencyException) { return Conflict<AdventureLocationDto>(); }
        catch (AdventureLocationRelationConflictException) { return Conflict<AdventureLocationDto>(); }
    }

    public async Task<AdventureCatalogResult<bool>> DeleteAsync(AdventureCatalogActor actor, Guid moduleId, Guid locationId, long expectedVersion, CancellationToken ct)
    {
        if (!Authorized(actor)) return Forbidden<bool>();
        var location = await locations.FindAsync(moduleId, locationId, true, ct);
        if (location is null) return NotFound<bool>();
        if (location.Version != expectedVersion) return Conflict<bool>();
        locations.Remove(location);
        try { await locations.SaveChangesAsync(ct); metrics.OperationCompleted("location_delete", "success", 0); return AdventureCatalogResult<bool>.Success(true); }
        catch (AdventureLocationConcurrencyException) { return Conflict<bool>(); }
        catch (AdventureLocationRelationConflictException) { return Conflict<bool>(); }
    }

    public async Task<AdventureCatalogResult<AdventureLocationDto>> SetDetailMapAsync(AdventureCatalogActor actor, Guid moduleId, Guid locationId, Guid? mapId, long expectedVersion, CancellationToken ct)
    {
        if (!Authorized(actor)) return Forbidden<AdventureLocationDto>();
        var location = await locations.FindAsync(moduleId, locationId, true, ct);
        if (location is null) return NotFound<AdventureLocationDto>();
        if (location.Version != expectedVersion) return Conflict<AdventureLocationDto>();
        if (mapId.HasValue && !await locations.MapExistsAsync(moduleId, mapId.Value, ct)) return NotFound<AdventureLocationDto>();
        try { location.SetDetailMap(mapId, actor.UserId, time.GetUtcNow()); await locations.SaveChangesAsync(ct); metrics.OperationCompleted("location_detail_map", "success", 0); return await GetCoreAsync(moduleId, locationId, null, true, ct); }
        catch (AdventureLocationConcurrencyException) { return Conflict<AdventureLocationDto>(); }
        catch (AdventureLocationRelationConflictException) { return Conflict<AdventureLocationDto>(); }
    }

    public async Task<AdventureCatalogResult<AdventureLocationDto>> CreatePointAsync(PointWrite command, CancellationToken ct)
    {
        if (!Authorized(command.UserId, command.IsAdmin)) return Forbidden<AdventureLocationDto>();
        var location = await locations.FindAsync(command.ModuleId, command.LocationId, true, ct);
        if (location is null) return NotFound<AdventureLocationDto>();
        if (location.Version != command.ExpectedVersion) return Conflict<AdventureLocationDto>();
        try { location.AddPoint(Guid.NewGuid(), command.Name ?? "", command.Description, command.X, command.Y, command.UserId, time.GetUtcNow()); await locations.SaveChangesAsync(ct); metrics.OperationCompleted("location_point_create", "success", 0); return await GetCoreAsync(command.ModuleId, location.Id, null, true, ct); }
        catch (ArgumentException ex) { return Validation<AdventureLocationDto>(ex.ParamName ?? "point", ex.Message); }
        catch (AdventureLocationConcurrencyException) { return Conflict<AdventureLocationDto>(); }
        catch (AdventureLocationRelationConflictException) { return Conflict<AdventureLocationDto>(); }
    }

    public async Task<AdventureCatalogResult<AdventureLocationDto>> UpdatePointAsync(PointWrite command, CancellationToken ct)
    {
        if (!Authorized(command.UserId, command.IsAdmin)) return Forbidden<AdventureLocationDto>();
        var location = await locations.FindAsync(command.ModuleId, command.LocationId, true, ct);
        if (location is null) return NotFound<AdventureLocationDto>();
        if (location.Version != command.ExpectedVersion) return Conflict<AdventureLocationDto>();
        try
        {
            if (!location.UpdatePoint(command.PointId!.Value, command.Name ?? "", command.Description, command.X, command.Y, command.UserId, time.GetUtcNow())) return NotFound<AdventureLocationDto>();
            await locations.SaveChangesAsync(ct); metrics.OperationCompleted("location_point_update", "success", 0); return await GetCoreAsync(command.ModuleId, location.Id, null, true, ct);
        }
        catch (ArgumentException ex) { return Validation<AdventureLocationDto>(ex.ParamName ?? "point", ex.Message); }
        catch (AdventureLocationConcurrencyException) { return Conflict<AdventureLocationDto>(); }
        catch (AdventureLocationRelationConflictException) { return Conflict<AdventureLocationDto>(); }
    }

    public async Task<AdventureCatalogResult<AdventureLocationDto>> DeletePointAsync(PointWrite command, CancellationToken ct)
    {
        if (!Authorized(command.UserId, command.IsAdmin)) return Forbidden<AdventureLocationDto>();
        var location = await locations.FindAsync(command.ModuleId, command.LocationId, true, ct);
        if (location is null) return NotFound<AdventureLocationDto>();
        if (location.Version != command.ExpectedVersion) return Conflict<AdventureLocationDto>();
        if (!location.RemovePoint(command.PointId!.Value, command.UserId, time.GetUtcNow())) return NotFound<AdventureLocationDto>();
        try { await locations.SaveChangesAsync(ct); metrics.OperationCompleted("location_point_delete", "success", 0); return await GetCoreAsync(command.ModuleId, location.Id, null, true, ct); }
        catch (AdventureLocationConcurrencyException) { return Conflict<AdventureLocationDto>(); }
        catch (AdventureLocationRelationConflictException) { return Conflict<AdventureLocationDto>(); }
    }

    public async Task<AdventureCatalogResult<AdventureLocationDto>> SetChapterAsync(AdventureCatalogActor actor, Guid moduleId, Guid locationId, Guid chapterId, long expectedVersion, bool add, CancellationToken ct)
    {
        if (!Authorized(actor)) return Forbidden<AdventureLocationDto>();
        var location = await locations.FindAsync(moduleId, locationId, true, ct);
        if (location is null) return NotFound<AdventureLocationDto>();
        if (location.Version != expectedVersion) return Conflict<AdventureLocationDto>();
        if (!await locations.ChapterExistsAsync(moduleId, chapterId, ct)) return NotFound<AdventureLocationDto>();
        try { location.SetChapter(chapterId, actor.UserId, time.GetUtcNow(), add); await locations.SaveChangesAsync(ct); metrics.OperationCompleted("location_chapter", "success", 0); return await GetCoreAsync(moduleId, locationId, null, true, ct); }
        catch (AdventureLocationConcurrencyException) { return Conflict<AdventureLocationDto>(); }
        catch (AdventureLocationRelationConflictException) { return Conflict<AdventureLocationDto>(); }
    }

    public async Task<AdventureCatalogResult<AdventureLocationDto>> SetPlacementAsync(AdventureCatalogActor actor, Guid moduleId, Guid locationId, Guid mapId, decimal x, decimal y, long expectedVersion, CancellationToken ct)
    {
        if (!Authorized(actor)) return Forbidden<AdventureLocationDto>();
        var location = await locations.FindAsync(moduleId, locationId, true, ct);
        if (location is null) return NotFound<AdventureLocationDto>();
        if (location.Version != expectedVersion) return Conflict<AdventureLocationDto>();
        if (!await locations.MapExistsAsync(moduleId, mapId, ct)) return NotFound<AdventureLocationDto>();
        try { location.SetPlacement(mapId, x, y, actor.UserId, time.GetUtcNow()); await locations.SaveChangesAsync(ct); metrics.OperationCompleted("location_placement", "success", 0); return await GetCoreAsync(moduleId, locationId, null, true, ct); }
        catch (ArgumentException ex) { return Validation<AdventureLocationDto>("coordinates", ex.Message); }
        catch (AdventureLocationConcurrencyException) { return Conflict<AdventureLocationDto>(); }
        catch (AdventureLocationRelationConflictException) { return Conflict<AdventureLocationDto>(); }
    }

    public async Task<AdventureCatalogResult<AdventureLocationDto>> RemovePlacementAsync(AdventureCatalogActor actor, Guid moduleId, Guid locationId, Guid mapId, long expectedVersion, CancellationToken ct)
    {
        if (!Authorized(actor)) return Forbidden<AdventureLocationDto>();
        var location = await locations.FindAsync(moduleId, locationId, true, ct);
        if (location is null) return NotFound<AdventureLocationDto>();
        if (location.Version != expectedVersion) return Conflict<AdventureLocationDto>();
        location.RemovePlacement(mapId, actor.UserId, time.GetUtcNow());
        try { await locations.SaveChangesAsync(ct); metrics.OperationCompleted("location_placement_delete", "success", 0); return await GetCoreAsync(moduleId, locationId, null, true, ct); }
        catch (AdventureLocationConcurrencyException) { return Conflict<AdventureLocationDto>(); }
        catch (AdventureLocationRelationConflictException) { return Conflict<AdventureLocationDto>(); }
    }

    private async Task<AdventureCatalogResult<IReadOnlyList<AdventureLocationDto>>> ListCoreAsync(Guid moduleId, Guid? campaignId, bool admin, CancellationToken ct)
    {
        if (!await locations.ModuleExistsAsync(moduleId, ct)) return NotFound<IReadOnlyList<AdventureLocationDto>>();
        var maps = (await locations.ListMapsAsync(moduleId, ct)).ToDictionary(item => item.Id);
        var chapters = (await locations.ListChaptersAsync(moduleId, ct)).ToDictionary(item => item.Id);
        var result = (await locations.ListAsync(moduleId, false, ct)).Select(item => ToDto(item, maps, chapters, campaignId, admin)).ToArray();
        return AdventureCatalogResult<IReadOnlyList<AdventureLocationDto>>.Success(result);
    }

    private async Task<AdventureCatalogResult<AdventureLocationDto>> GetCoreAsync(Guid moduleId, Guid locationId, Guid? campaignId, bool admin, CancellationToken ct)
    {
        var location = await locations.FindAsync(moduleId, locationId, false, ct);
        if (location is null) return NotFound<AdventureLocationDto>();
        var maps = (await locations.ListMapsAsync(moduleId, ct)).ToDictionary(item => item.Id);
        var chapters = (await locations.ListChaptersAsync(moduleId, ct)).ToDictionary(item => item.Id);
        return AdventureCatalogResult<AdventureLocationDto>.Success(ToDto(location, maps, chapters, campaignId, admin));
    }

    private static AdventureLocationDto ToDto(AdventureLocation location, IReadOnlyDictionary<Guid, AdventureMap> maps, IReadOnlyDictionary<Guid, AdventureChapter> chapters, Guid? campaignId, bool admin)
    {
        LocationMapDto? detail = null;
        if (location.DetailMapId is { } detailId && maps.TryGetValue(detailId, out var detailMap)) detail = MapDto(detailMap, campaignId);
        return new(location.Id, location.ModuleId, location.Name, location.Description, location.DetailMapId, detail,
            location.PointsOfInterest.OrderBy(item => item.Name).ThenBy(item => item.Id).Select(item => new PointOfInterestDto(item.Id, item.Name, item.Description, item.X, item.Y, admin ? item.CreatedAt : null, admin ? item.UpdatedAt : null, admin ? item.Version : null)).ToArray(),
            location.Placements.Select(item => new LocationPlacementDto(item.MapId, item.X, item.Y)).ToArray(),
            location.Chapters.Where(item => chapters.ContainsKey(item.ChapterId)).Select(item => chapters[item.ChapterId]).OrderBy(item => item.Position).Select(item => new LocationChapterDto(item.Id, item.Name, item.Position)).ToArray(),
            admin ? location.CreatedAt : null, admin ? location.UpdatedAt : null, admin ? location.Version : null);
    }

    private static LocationMapDto MapDto(AdventureMap map, Guid? campaignId) => new(map.Id, map.Name, map.Image is not null, map.Image is null ? null : campaignId is null ? $"/api/v1/admin/adventure-modules/{map.ModuleId}/maps/{map.Id}/image" : $"/api/v1/campaigns/{campaignId}/adventure/maps/{map.Id}/image", map.Image?.Width, map.Image?.Height);
    private static bool Authorized(AdventureCatalogActor actor) => Authorized(actor.UserId, actor.IsPlatformAdmin);
    private static bool Authorized(Guid userId, bool isAdmin) => userId != Guid.Empty && isAdmin;
    private static AdventureCatalogResult<T> Forbidden<T>() => AdventureModuleHandlerSupport.Forbidden<T>();
    private static AdventureCatalogResult<T> NotFound<T>() => AdventureModuleHandlerSupport.NotFound<T>();
    private static AdventureCatalogResult<T> Conflict<T>() => AdventureCatalogResult<T>.Failure(new("adventure_catalog.location_conflict", AdventureCatalogErrorType.Conflict, "La localización ha cambiado; recarga antes de continuar."));
    private static AdventureCatalogResult<T> Validation<T>(string field, string message) => AdventureModuleHandlerSupport.Validation<T>(field, message);
}
