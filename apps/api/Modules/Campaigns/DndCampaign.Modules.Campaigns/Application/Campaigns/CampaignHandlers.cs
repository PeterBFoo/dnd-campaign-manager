using System.Diagnostics;
using DndCampaign.Modules.AdventureCatalog.Contracts.Campaigns;
using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.Campaigns.Application.Abstractions;
using DndCampaign.Modules.Campaigns.Application.Ports;
using DndCampaign.Modules.Campaigns.Domain.Campaigns;

namespace DndCampaign.Modules.Campaigns.Application.Campaigns;

internal sealed record CreateCampaignCommand(Guid UserId, string? Name, Guid? AdventureModuleId = null);

internal sealed record ListCampaignsQuery(Guid UserId);

internal sealed record GetCampaignQuery(Guid UserId, Guid CampaignId);

internal sealed record DeleteCampaignCommand(Guid UserId, Guid CampaignId);

internal sealed record AssignAdventureModuleCommand(
    Guid UserId,
    Guid CampaignId,
    Guid AdventureModuleId,
    long ExpectedVersion);

internal sealed record RemoveAdventureModuleCommand(
    Guid UserId,
    Guid CampaignId,
    long ExpectedVersion);

internal sealed record CampaignDto(
    Guid Id,
    string Name,
    string Role,
    Guid? AdventureModuleId,
    DateTimeOffset CreatedAt,
    AdventureModuleCampaignSummary? AdventureModule,
    long Version);

internal sealed class CreateCampaignHandler(
    ICampaignRepository campaigns,
    ICampaignMetrics metrics,
    TimeProvider timeProvider,
    IAdventureModuleCampaignReader? modules = null)
{
    public async Task<CampaignResult<CampaignDto>> HandleAsync(
        CreateCampaignCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            if (command.UserId == Guid.Empty)
            {
                outcome = "forbidden";
                return CampaignSupport.Forbidden<CampaignDto>();
            }

            if (command.AdventureModuleId == Guid.Empty)
            {
                outcome = "validation";
                return CampaignSupport.Validation<CampaignDto>("adventureModuleId", "El identificador del módulo no es válido.");
            }

            AdventureModuleCampaignSummary? module = null;
            if (command.AdventureModuleId is { } moduleId)
            {
                module = modules is null
                    ? null
                    : await modules.FindAsync(moduleId, cancellationToken);
                if (module is null)
                {
                    outcome = "not_found";
                    return CampaignSupport.NotFound<CampaignDto>("No se ha encontrado el módulo de aventura.");
                }
            }

            Campaign campaign;
            try
            {
                campaign = Campaign.Create(
                    command.Name ?? string.Empty,
                    command.UserId,
                    timeProvider.GetUtcNow(),
                    command.AdventureModuleId);
            }
            catch (ArgumentException exception)
            {
                outcome = "validation";
                return CampaignSupport.Validation<CampaignDto>("name", exception.Message);
            }

            campaigns.Add(campaign);
            try
            {
                await campaigns.SaveChangesAsync(cancellationToken);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException exception)
                when (exception.InnerException is Npgsql.PostgresException
                { SqlState: Npgsql.PostgresErrorCodes.ForeignKeyViolation })
            {
                outcome = "not_found";
                return CampaignSupport.NotFound<CampaignDto>("No se ha encontrado el módulo de aventura.");
            }
            outcome = "success";
            return CampaignResult<CampaignDto>.Success(ToDto(campaign, "dm", module));
        }
        finally
        {
            metrics.OperationCompleted(
                command.AdventureModuleId.HasValue ? "create_with_module" : "create",
                outcome,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    internal static CampaignDto ToDto(
        Campaign campaign,
        string role,
        AdventureModuleCampaignSummary? module = null) => new(
        campaign.Id,
        campaign.Name,
        role,
        campaign.AdventureModuleId,
        campaign.CreatedAt,
        module,
        campaign.Version);
}

internal sealed class ListCampaignsHandler(
    ICampaignRepository campaigns,
    IPlayerCampaignAccessReader playerAccess,
    ICampaignMetrics metrics,
    IAdventureModuleCampaignReader? modules = null)
{
    public async Task<IReadOnlyList<CampaignDto>> HandleAsync(
        ListCampaignsQuery query,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var playerCampaignIds = await playerAccess.ListCampaignIdsAsync(query.UserId, cancellationToken);
            var accessible = await campaigns.ListAccessibleAsync(
                query.UserId,
                playerCampaignIds,
                cancellationToken);
            outcome = "success";
            IReadOnlyList<AdventureModuleCampaignSummary> summaries = modules is null
                ? []
                : await modules.ListAsync(
                    accessible.Where(campaign => campaign.AdventureModuleId.HasValue)
                        .Select(campaign => campaign.AdventureModuleId!.Value)
                        .Distinct()
                        .ToArray(), cancellationToken);
            var byId = summaries.ToDictionary(summary => summary.Id);
            return accessible
                .Select(campaign => CreateCampaignHandler.ToDto(
                    campaign,
                    campaign.DmUserId == query.UserId ? "dm" : "player",
                    campaign.AdventureModuleId is { } moduleId && byId.TryGetValue(moduleId, out var summary)
                        ? summary
                        : null))
                .ToArray();
        }
        finally
        {
            metrics.OperationCompleted("list", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class GetCampaignHandler(
    ICampaignRepository campaigns,
    IPlayerCampaignAccessReader playerAccess,
    ICampaignMetrics metrics,
    IAdventureModuleCampaignReader? modules = null)
{
    public async Task<CampaignResult<CampaignDto>> HandleAsync(
        GetCampaignQuery query,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var campaign = await campaigns.FindAsync(query.CampaignId, cancellationToken);
            if (campaign is null)
            {
                outcome = "not_found";
                return CampaignResult<CampaignDto>.Failure(new CampaignError(
                    "campaign.not_found",
                    CampaignErrorType.NotFound,
                    "No se ha encontrado la campaña."));
            }

            if (campaign.DmUserId == query.UserId)
            {
                outcome = "success";
                return CampaignResult<CampaignDto>.Success(CreateCampaignHandler.ToDto(
                    campaign, "dm", modules is null || campaign.AdventureModuleId is null
                        ? null
                        : await modules.FindAsync(campaign.AdventureModuleId.Value, cancellationToken)));
            }

            if (!await playerAccess.HasPlayerAccessAsync(
                campaign.Id,
                query.UserId,
                cancellationToken))
            {
                outcome = "forbidden";
                return CampaignResult<CampaignDto>.Failure(new CampaignError(
                    "campaign.forbidden",
                    CampaignErrorType.Forbidden,
                    "No tienes acceso a esta campaña."));
            }

            outcome = "success";
            return CampaignResult<CampaignDto>.Success(CreateCampaignHandler.ToDto(
                campaign, "player", modules is null || campaign.AdventureModuleId is null
                    ? null
                    : await modules.FindAsync(campaign.AdventureModuleId.Value, cancellationToken)));
        }
        finally
        {
            metrics.OperationCompleted("detail", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class AssignAdventureModuleHandler(
    ICampaignRepository campaigns,
    IAdventureModuleCampaignReader modules,
    ICampaignMetrics metrics)
{
    public async Task<CampaignResult<CampaignDto>> HandleAsync(
        AssignAdventureModuleCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var campaign = await campaigns.FindForUpdateAsync(command.CampaignId, cancellationToken);
            if (campaign is null) { outcome = "not_found"; return CampaignSupport.NotFound<CampaignDto>(); }
            if (campaign.DmUserId != command.UserId) { outcome = "forbidden"; return CampaignSupport.Forbidden<CampaignDto>(); }
            if (campaign.Version != command.ExpectedVersion) { outcome = "conflict"; return CampaignSupport.Conflict<CampaignDto>(); }
            if (command.AdventureModuleId == Guid.Empty)
            {
                outcome = "validation";
                return CampaignSupport.Validation<CampaignDto>(
                    "adventureModuleId", "El identificador del módulo no es válido.");
            }
            var module = await modules.FindAsync(command.AdventureModuleId, cancellationToken);
            if (module is null) { outcome = "not_found"; return CampaignSupport.NotFound<CampaignDto>("No se ha encontrado el módulo de aventura."); }
            campaign.AssignAdventureModule(command.AdventureModuleId);
            try
            {
                await campaigns.SaveChangesAsync(cancellationToken);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException exception)
                when (exception.InnerException is Npgsql.PostgresException
                { SqlState: Npgsql.PostgresErrorCodes.ForeignKeyViolation })
            {
                outcome = "not_found";
                return CampaignSupport.NotFound<CampaignDto>("No se ha encontrado el módulo de aventura.");
            }
            outcome = "success";
            return CampaignResult<CampaignDto>.Success(CreateCampaignHandler.ToDto(campaign, "dm", module));
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            outcome = "conflict";
            return CampaignSupport.Conflict<CampaignDto>();
        }
        finally { metrics.OperationCompleted("assign_module", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds); }
    }
}

internal sealed class RemoveAdventureModuleHandler(
    ICampaignRepository campaigns,
    ICampaignMetrics metrics)
{
    public async Task<CampaignResult<CampaignDto>> HandleAsync(
        RemoveAdventureModuleCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var campaign = await campaigns.FindForUpdateAsync(command.CampaignId, cancellationToken);
            if (campaign is null) { outcome = "not_found"; return CampaignSupport.NotFound<CampaignDto>(); }
            if (campaign.DmUserId != command.UserId) { outcome = "forbidden"; return CampaignSupport.Forbidden<CampaignDto>(); }
            if (campaign.Version != command.ExpectedVersion) { outcome = "conflict"; return CampaignSupport.Conflict<CampaignDto>(); }
            campaign.RemoveAdventureModule();
            await campaigns.SaveChangesAsync(cancellationToken);
            outcome = "success";
            return CampaignResult<CampaignDto>.Success(CreateCampaignHandler.ToDto(campaign, "dm"));
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            outcome = "conflict";
            return CampaignSupport.Conflict<CampaignDto>();
        }
        finally { metrics.OperationCompleted("remove_module", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds); }
    }
}

internal static class CampaignSupport
{
    public static CampaignResult<T> Forbidden<T>() => CampaignResult<T>.Failure(new CampaignError(
        "campaign.forbidden", CampaignErrorType.Forbidden, "No tienes acceso a esta campaña."));

    public static CampaignResult<T> NotFound<T>(string description = "No se ha encontrado la campaña.") =>
        CampaignResult<T>.Failure(new CampaignError("campaign.not_found", CampaignErrorType.NotFound, description));

    public static CampaignResult<T> Conflict<T>() => CampaignResult<T>.Failure(new CampaignError(
        "campaign.stale_version", CampaignErrorType.Conflict,
        "La campaña ha cambiado. Recarga la versión vigente antes de continuar."));

    public static CampaignResult<T> Validation<T>(string field, string description) =>
        CampaignResult<T>.Failure(new CampaignError(
            "campaign.validation", CampaignErrorType.Validation, "La campaña no es válida.",
            new Dictionary<string, string[]> { [field] = [description] }));
}

internal sealed class DeleteCampaignHandler(
    ICampaignRepository campaigns,
    ICampaignMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task<CampaignResult<bool>> HandleAsync(
        DeleteCampaignCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var campaign = await campaigns.FindForUpdateAsync(command.CampaignId, cancellationToken);
            if (campaign is null)
            {
                outcome = "not_found";
                return CampaignResult<bool>.Failure(new CampaignError(
                    "campaign.not_found",
                    CampaignErrorType.NotFound,
                    "No se ha encontrado la campaña."));
            }

            if (command.UserId == Guid.Empty || campaign.DmUserId != command.UserId)
            {
                outcome = "forbidden";
                return CampaignResult<bool>.Failure(new CampaignError(
                    "campaign.forbidden",
                    CampaignErrorType.Forbidden,
                    "Solo el DM puede eliminar la campaña."));
            }

            campaign.Delete(timeProvider.GetUtcNow());
            await campaigns.SaveChangesAsync(cancellationToken);
            outcome = "success";
            return CampaignResult<bool>.Success(true);
        }
        finally
        {
            metrics.OperationCompleted("delete", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
