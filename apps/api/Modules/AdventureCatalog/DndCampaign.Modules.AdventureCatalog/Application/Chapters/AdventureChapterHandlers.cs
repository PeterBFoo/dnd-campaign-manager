using System.Diagnostics;
using DndCampaign.Modules.AdventureCatalog.Application.Abstractions;
using DndCampaign.Modules.AdventureCatalog.Application.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;

namespace DndCampaign.Modules.AdventureCatalog.Application.Chapters;

internal sealed record ChapterDto(Guid Id, string Name, string? Description, int Position,
    EditorialProvenanceDto? Provenance, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt, long? Version);
internal sealed record ChapterIndexDto(long IndexVersion, IReadOnlyList<ChapterDto> Chapters);
internal sealed record ChapterWrite(Guid UserId, bool IsAdmin, Guid ModuleId, Guid? ChapterId,
    string? Name, string? Description, EditorialProvenanceInput Provenance, long? ExpectedVersion);

internal sealed class AdventureChapterService(IAdventureChapterRepository repository,
    ICampaignAdventureContext campaigns, IAdventureCatalogMetrics metrics, TimeProvider time)
{
    public async Task<AdventureCatalogResult<ChapterIndexDto>> ListAdminAsync(Guid userId, bool admin, Guid moduleId, CancellationToken ct)
    {
        if (!Authorized(userId, admin)) return Forbidden<ChapterIndexDto>();
        return await ListAsync(moduleId, true, ct);
    }

    public async Task<AdventureCatalogResult<ChapterDto>> GetAdminAsync(Guid userId, bool admin, Guid moduleId, Guid chapterId, CancellationToken ct)
    {
        if (!Authorized(userId, admin)) return Forbidden<ChapterDto>();
        var chapter = await repository.FindAsync(moduleId, chapterId, false, ct);
        return chapter is null ? NotFound<ChapterDto>() : AdventureCatalogResult<ChapterDto>.Success(ToDto(chapter, true));
    }

    public async Task<AdventureCatalogResult<ChapterIndexDto>> ListCampaignAsync(Guid userId, Guid campaignId, CancellationToken ct)
    {
        var context = await campaigns.ResolveAsync(campaignId, userId, ct);
        if (!context.Exists || context.AdventureModuleId is null) return NotFound<ChapterIndexDto>();
        if (!context.IsDm) return Forbidden<ChapterIndexDto>();
        return await ListAsync(context.AdventureModuleId.Value, false, ct);
    }

    public async Task<AdventureCatalogResult<ChapterDto>> GetCampaignAsync(Guid userId, Guid campaignId, Guid chapterId, CancellationToken ct)
    {
        var context = await campaigns.ResolveAsync(campaignId, userId, ct);
        if (!context.Exists || context.AdventureModuleId is null) return NotFound<ChapterDto>();
        if (!context.IsDm) return Forbidden<ChapterDto>();
        var chapter = await repository.FindAsync(context.AdventureModuleId.Value, chapterId, false, ct);
        return chapter is null ? NotFound<ChapterDto>() : AdventureCatalogResult<ChapterDto>.Success(ToDto(chapter, false));
    }

    public async Task<AdventureCatalogResult<ChapterDto>> CreateAsync(ChapterWrite command, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp(); var outcome = "failure";
        try
        {
            if (!Authorized(command.UserId, command.IsAdmin)) { outcome = "forbidden"; return Forbidden<ChapterDto>(); }
            var module = await repository.FindModuleAsync(command.ModuleId, true, ct);
            if (module is null) { outcome = "not_found"; return NotFound<ChapterDto>(); }
            var chapters = await repository.ListAsync(command.ModuleId, false, ct);
            try
            {
                var provenance = AdventureModuleHandlerSupport.CreateProvenance(command.Provenance, command.UserId, time.GetUtcNow());
                var chapter = AdventureChapter.Create(Guid.NewGuid(), command.ModuleId, command.Name ?? "", command.Description,
                    chapters.Count + 1, provenance, command.UserId, time.GetUtcNow());
                repository.Add(chapter); module.AdvanceChaptersVersion(); await repository.SaveChangesAsync(ct);
                outcome = "success"; return AdventureCatalogResult<ChapterDto>.Success(ToDto(chapter, true));
            }
            catch (ArgumentException ex) { outcome = "validation"; return Validation<ChapterDto>(ex.ParamName ?? "chapter", ex.Message); }
            catch (AdventureModuleConcurrencyException) { outcome = "conflict"; return Conflict<ChapterDto>(); }
        }
        finally { metrics.OperationCompleted("chapter_create", outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }

    public async Task<AdventureCatalogResult<ChapterDto>> UpdateAsync(ChapterWrite command, CancellationToken ct)
    {
        if (!Authorized(command.UserId, command.IsAdmin)) return Forbidden<ChapterDto>();
        var chapter = await repository.FindAsync(command.ModuleId, command.ChapterId!.Value, true, ct);
        if (chapter is null) return NotFound<ChapterDto>();
        if (chapter.Version != command.ExpectedVersion) return Conflict<ChapterDto>();
        try
        {
            chapter.Update(command.Name ?? "", command.Description,
                AdventureModuleHandlerSupport.CreateProvenance(command.Provenance, command.UserId, time.GetUtcNow()), command.UserId, time.GetUtcNow());
            await repository.SaveChangesAsync(ct);
            return AdventureCatalogResult<ChapterDto>.Success(ToDto(chapter, true));
        }
        catch (ArgumentException ex) { return Validation<ChapterDto>(ex.ParamName ?? "chapter", ex.Message); }
        catch (AdventureModuleConcurrencyException) { return Conflict<ChapterDto>(); }
    }

    public async Task<AdventureCatalogResult<bool>> DeleteAsync(Guid userId, bool admin, Guid moduleId, Guid chapterId, long expectedVersion, CancellationToken ct)
    {
        if (!Authorized(userId, admin)) return Forbidden<bool>();
        var module = await repository.FindModuleAsync(moduleId, true, ct);
        var chapters = await repository.ListAsync(moduleId, true, ct);
        var chapter = chapters.SingleOrDefault(x => x.Id == chapterId);
        if (module is null || chapter is null) return NotFound<bool>();
        if (chapter.Version != expectedVersion) return Conflict<bool>();
        try { await repository.DeleteAndCompactAsync(module, chapter, chapters.Where(x => x.Id != chapterId).ToArray(), ct); return AdventureCatalogResult<bool>.Success(true); }
        catch (AdventureModuleConcurrencyException) { return Conflict<bool>(); }
    }

    public async Task<AdventureCatalogResult<ChapterIndexDto>> ReorderAsync(Guid userId, bool admin, Guid moduleId, long expectedIndexVersion, IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        if (!Authorized(userId, admin)) return Forbidden<ChapterIndexDto>();
        var module = await repository.FindModuleAsync(moduleId, true, ct);
        if (module is null) return NotFound<ChapterIndexDto>();
        var chapters = await repository.ListAsync(moduleId, true, ct);
        if (module.ChaptersVersion != expectedIndexVersion || ids is null || ids.Count != chapters.Count || ids.Distinct().Count() != ids.Count || ids.Any(id => chapters.All(x => x.Id != id)))
            return Conflict<ChapterIndexDto>();
        try
        {
            var byId = chapters.ToDictionary(x => x.Id);
            await repository.ReorderAsync(module, ids.Select((id, index) => (byId[id], index + 1)).ToArray(), ct);
            return AdventureCatalogResult<ChapterIndexDto>.Success(new(module.ChaptersVersion, ids.Select(id => ToDto(byId[id], true)).ToArray()));
        }
        catch (AdventureModuleConcurrencyException) { return Conflict<ChapterIndexDto>(); }
    }

    private async Task<AdventureCatalogResult<ChapterIndexDto>> ListAsync(Guid moduleId, bool admin, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp(); var outcome = "not_found";
        try
        {
            var module = await repository.FindModuleAsync(moduleId, false, ct);
            if (module is null) return NotFound<ChapterIndexDto>();
            var chapters = await repository.ListAsync(moduleId, false, ct); outcome = "success";
            return AdventureCatalogResult<ChapterIndexDto>.Success(new(module.ChaptersVersion, chapters.Select(x => ToDto(x, admin)).ToArray()));
        }
        finally { metrics.OperationCompleted(admin ? "chapter_list" : "campaign_chapter_list", outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }

    private static ChapterDto ToDto(AdventureChapter x, bool admin) => new(x.Id, x.Name, x.Description, x.Position,
        admin ? AdventureModuleHandlerSupport.ToProvenanceDto(x.Provenance) : null,
        admin ? x.CreatedAt : null, admin ? x.UpdatedAt : null, admin ? x.Version : null);
    private static bool Authorized(Guid id, bool admin) => id != Guid.Empty && admin;
    private static AdventureCatalogResult<T> Forbidden<T>() => AdventureModuleHandlerSupport.Forbidden<T>();
    private static AdventureCatalogResult<T> NotFound<T>() => AdventureModuleHandlerSupport.NotFound<T>();
    private static AdventureCatalogResult<T> Conflict<T>() => AdventureCatalogResult<T>.Failure(new("adventure_catalog.chapter_conflict", AdventureCatalogErrorType.Conflict, "El índice o capítulo ha cambiado; recarga antes de continuar."));
    private static AdventureCatalogResult<T> Validation<T>(string field, string message) => AdventureModuleHandlerSupport.Validation<T>(field, message);
}
