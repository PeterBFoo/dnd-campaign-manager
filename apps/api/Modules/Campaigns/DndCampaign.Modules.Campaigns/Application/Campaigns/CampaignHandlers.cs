using System.Diagnostics;
using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.Campaigns.Application.Abstractions;
using DndCampaign.Modules.Campaigns.Application.Ports;
using DndCampaign.Modules.Campaigns.Domain.Campaigns;

namespace DndCampaign.Modules.Campaigns.Application.Campaigns;

internal sealed record CreateCampaignCommand(Guid UserId, string? Name);

internal sealed record ListCampaignsQuery(Guid UserId);

internal sealed record GetCampaignQuery(Guid UserId, Guid CampaignId);

internal sealed record CampaignDto(
    Guid Id,
    string Name,
    string Role,
    Guid? AdventureModuleId,
    DateTimeOffset CreatedAt);

internal sealed class CreateCampaignHandler(
    ICampaignRepository campaigns,
    ICampaignMetrics metrics,
    TimeProvider timeProvider)
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
                return Forbidden<CampaignDto>();
            }

            Campaign campaign;
            try
            {
                campaign = Campaign.Create(command.Name ?? string.Empty, command.UserId, timeProvider.GetUtcNow());
            }
            catch (ArgumentException exception)
            {
                outcome = "validation";
                return CampaignResult<CampaignDto>.Failure(new CampaignError(
                    "campaign.invalid_name",
                    CampaignErrorType.Validation,
                    "El nombre de la campaña no es válido.",
                    new Dictionary<string, string[]> { ["name"] = [exception.Message] }));
            }

            campaigns.Add(campaign);
            await campaigns.SaveChangesAsync(cancellationToken);
            outcome = "success";
            return CampaignResult<CampaignDto>.Success(ToDto(campaign, "dm"));
        }
        finally
        {
            metrics.OperationCompleted("create", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    private static CampaignResult<T> Forbidden<T>() => CampaignResult<T>.Failure(new CampaignError(
        "campaign.forbidden",
        CampaignErrorType.Forbidden,
        "No tienes acceso a esta campaña."));

    internal static CampaignDto ToDto(Campaign campaign, string role) => new(
        campaign.Id,
        campaign.Name,
        role,
        campaign.AdventureModuleId,
        campaign.CreatedAt);
}

internal sealed class ListCampaignsHandler(
    ICampaignRepository campaigns,
    IPlayerCampaignAccessReader playerAccess,
    ICampaignMetrics metrics)
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
            return accessible
                .Select(campaign => CreateCampaignHandler.ToDto(
                    campaign,
                    campaign.DmUserId == query.UserId ? "dm" : "player"))
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
    ICampaignMetrics metrics)
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
                return CampaignResult<CampaignDto>.Success(CreateCampaignHandler.ToDto(campaign, "dm"));
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
            return CampaignResult<CampaignDto>.Success(CreateCampaignHandler.ToDto(campaign, "player"));
        }
        finally
        {
            metrics.OperationCompleted("detail", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
