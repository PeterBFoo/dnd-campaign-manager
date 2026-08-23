using System.Diagnostics;
using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Characters.Contracts.ActiveCharacters;
using DndCampaign.Modules.Missions.Application.Abstractions;
using DndCampaign.Modules.Missions.Application.Ports;
using DndCampaign.Modules.Missions.Domain.Missions;

namespace DndCampaign.Modules.Missions.Application.Missions;

internal sealed record ListMissionsQuery(Guid UserId, Guid CampaignId);
internal sealed record CreateMissionCommand(
    Guid UserId, Guid CampaignId, string? Title, string? Description, bool IsMain);
internal sealed record UpdateMissionCommand(
    Guid UserId, Guid CampaignId, Guid MissionId, string? Title, string? Description, string? Status);
internal sealed record SetMainMissionCommand(Guid UserId, Guid CampaignId, Guid MissionId);
internal sealed record ClearMainMissionCommand(Guid UserId, Guid CampaignId, Guid MissionId);
internal sealed record DeleteMissionCommand(Guid UserId, Guid CampaignId, Guid MissionId);

internal sealed record MissionDto(
    Guid Id,
    Guid CampaignId,
    string Title,
    string? Description,
    string Status,
    bool IsMain,
    string AuthorType,
    Guid? AuthorCharacterId,
    string AuthorDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool CanDelete);

internal sealed record MissionsDto(IReadOnlyList<MissionDto> Items);

internal sealed class ListMissionsHandler(
    ICampaignAccessReader campaignAccess,
    IMissionRepository missions,
    IMissionMetrics metrics)
{
    public async Task<MissionResult<MissionsDto>> HandleAsync(
        ListMissionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await MissionAuthorization.AuthorizeAsync(
                campaignAccess, query.CampaignId, query.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = MissionErrors.Outcome(access.Error);
                return MissionResult<MissionsDto>.Failure(access.Error);
            }
            var items = await missions.ListAsync(query.CampaignId, cancellationToken);
            var mapped = items.Select(mission => MissionMapping.ToDto(
                mission,
                access.Role == CampaignRole.Dm || mission.CreatedByUserId == query.UserId)).ToArray();
            outcome = "success";
            return MissionResult<MissionsDto>.Success(new MissionsDto(mapped));
        }
        finally
        {
            metrics.OperationCompleted("list", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class CreateMissionHandler(
    ICampaignAccessReader campaignAccess,
    IActiveCharacterReader activeCharacters,
    IMissionRepository missions,
    IMissionMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task<MissionResult<MissionDto>> HandleAsync(
        CreateMissionCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await MissionAuthorization.AuthorizeAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = MissionErrors.Outcome(access.Error);
                return MissionResult<MissionDto>.Failure(access.Error);
            }

            Mission mission;
            try
            {
                if (access.Role == CampaignRole.Dm)
                {
                    mission = Mission.CreateForDm(
                        command.CampaignId, command.UserId, command.Title!, command.Description,
                        command.IsMain, timeProvider.GetUtcNow());
                }
                else
                {
                    var active = await activeCharacters.GetActiveAsync(
                        command.CampaignId, command.UserId, cancellationToken);
                    if (active is null)
                    {
                        outcome = "conflict";
                        return MissionResult<MissionDto>.Failure(MissionErrors.ActiveCharacterRequired());
                    }
                    mission = Mission.CreateForPlayer(
                        command.CampaignId, command.UserId, active.CharacterId, active.Name,
                        command.Title!, command.Description, command.IsMain, timeProvider.GetUtcNow());
                }
            }
            catch (ArgumentException exception)
            {
                outcome = "validation";
                return MissionResult<MissionDto>.Failure(MissionErrors.InvalidMission(exception.Message));
            }

            missions.Add(mission);
            if (mission.IsMain)
            {
                await missions.SaveAsMainAsync(command.CampaignId, mission, cancellationToken);
            }
            else
            {
                await missions.SaveChangesAsync(cancellationToken);
            }
            outcome = "success";
            return MissionResult<MissionDto>.Success(MissionMapping.ToDto(mission, true));
        }
        finally
        {
            metrics.OperationCompleted("create", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class UpdateMissionHandler(
    ICampaignAccessReader campaignAccess,
    IMissionRepository missions,
    IMissionMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task<MissionResult<MissionDto>> HandleAsync(
        UpdateMissionCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await MissionAuthorization.AuthorizeAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = MissionErrors.Outcome(access.Error);
                return MissionResult<MissionDto>.Failure(access.Error);
            }
            if (!MissionMapping.TryParseStatus(command.Status, out var status))
            {
                outcome = "validation";
                return MissionResult<MissionDto>.Failure(MissionErrors.InvalidStatus());
            }
            var mission = await missions.FindForUpdateAsync(
                command.CampaignId, command.MissionId, cancellationToken);
            if (mission is null)
            {
                outcome = "not_found";
                return MissionResult<MissionDto>.Failure(MissionErrors.MissionNotFound());
            }
            try
            {
                mission.Update(command.Title!, command.Description, status, timeProvider.GetUtcNow());
                await missions.SaveChangesAsync(cancellationToken);
            }
            catch (ArgumentException exception)
            {
                outcome = "validation";
                return MissionResult<MissionDto>.Failure(MissionErrors.InvalidMission(exception.Message));
            }
            outcome = "success";
            return MissionResult<MissionDto>.Success(MissionMapping.ToDto(
                mission,
                access.Role == CampaignRole.Dm || mission.CreatedByUserId == command.UserId));
        }
        finally
        {
            metrics.OperationCompleted("update", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class SetMainMissionHandler(
    ICampaignAccessReader campaignAccess,
    IMissionRepository missions,
    IMissionMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task<MissionResult<MissionDto>> HandleAsync(
        SetMainMissionCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await MissionAuthorization.AuthorizeAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = MissionErrors.Outcome(access.Error);
                return MissionResult<MissionDto>.Failure(access.Error);
            }
            var mission = await missions.FindForUpdateAsync(
                command.CampaignId, command.MissionId, cancellationToken);
            if (mission is null)
            {
                outcome = "not_found";
                return MissionResult<MissionDto>.Failure(MissionErrors.MissionNotFound());
            }
            try
            {
                mission.MarkAsMain(timeProvider.GetUtcNow());
            }
            catch (InvalidOperationException)
            {
                outcome = "conflict";
                return MissionResult<MissionDto>.Failure(MissionErrors.ActiveMissionRequired());
            }
            await missions.SaveAsMainAsync(command.CampaignId, mission, cancellationToken);
            outcome = "success";
            return MissionResult<MissionDto>.Success(MissionMapping.ToDto(
                mission,
                access.Role == CampaignRole.Dm || mission.CreatedByUserId == command.UserId));
        }
        finally
        {
            metrics.OperationCompleted("set_main", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class ClearMainMissionHandler(
    ICampaignAccessReader campaignAccess,
    IMissionRepository missions,
    IMissionMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task<MissionResult<bool>> HandleAsync(
        ClearMainMissionCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await MissionAuthorization.AuthorizeAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = MissionErrors.Outcome(access.Error);
                return MissionResult<bool>.Failure(access.Error);
            }
            var mission = await missions.FindForUpdateAsync(
                command.CampaignId, command.MissionId, cancellationToken);
            if (mission is null)
            {
                outcome = "not_found";
                return MissionResult<bool>.Failure(MissionErrors.MissionNotFound());
            }
            if (mission.ClearMain(timeProvider.GetUtcNow()))
            {
                await missions.SaveChangesAsync(cancellationToken);
            }
            outcome = "success";
            return MissionResult<bool>.Success(true);
        }
        finally
        {
            metrics.OperationCompleted("clear_main", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class DeleteMissionHandler(
    ICampaignAccessReader campaignAccess,
    IMissionRepository missions,
    IMissionMetrics metrics)
{
    public async Task<MissionResult<bool>> HandleAsync(
        DeleteMissionCommand command,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await MissionAuthorization.AuthorizeAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = MissionErrors.Outcome(access.Error);
                return MissionResult<bool>.Failure(access.Error);
            }
            var mission = await missions.FindForUpdateAsync(
                command.CampaignId, command.MissionId, cancellationToken);
            if (mission is null)
            {
                outcome = "not_found";
                return MissionResult<bool>.Failure(MissionErrors.MissionNotFound());
            }
            if (access.Role != CampaignRole.Dm && mission.CreatedByUserId != command.UserId)
            {
                outcome = "forbidden";
                return MissionResult<bool>.Failure(MissionErrors.Forbidden());
            }
            missions.Delete(mission);
            await missions.SaveChangesAsync(cancellationToken);
            outcome = "success";
            return MissionResult<bool>.Success(true);
        }
        finally
        {
            metrics.OperationCompleted("delete", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal static class MissionAuthorization
{
    public static async Task<(CampaignRole? Role, MissionError? Error)> AuthorizeAsync(
        ICampaignAccessReader campaignAccess,
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var access = await campaignAccess.GetAccessAsync(campaignId, userId, cancellationToken);
        if (!access.Exists)
        {
            return (null, MissionErrors.CampaignNotFound());
        }
        return access.Role is null
            ? (null, MissionErrors.Forbidden())
            : (access.Role, null);
    }
}

internal static class MissionMapping
{
    public static MissionDto ToDto(Mission mission, bool canDelete) => new(
        mission.Id,
        mission.CampaignId,
        mission.Title,
        mission.Description,
        mission.Status.ToString().ToLowerInvariant(),
        mission.IsMain,
        mission.AuthorType.ToString().ToLowerInvariant(),
        mission.AuthorCharacterId,
        mission.AuthorType == MissionAuthorType.Dm
            ? "Dirección de campaña"
            : mission.AuthorCharacterName!,
        mission.CreatedAt,
        mission.UpdatedAt,
        canDelete);

    public static bool TryParseStatus(string? value, out MissionStatus status) =>
        Enum.TryParse(value, true, out status) && Enum.IsDefined(status);
}

internal static class MissionErrors
{
    public static MissionError InvalidMission(string description) => new(
        MissionErrorType.Validation,
        "missions.invalid",
        description,
        new Dictionary<string, string[]> { ["mission"] = [description] });

    public static MissionError InvalidStatus() => new(
        MissionErrorType.Validation,
        "missions.status_invalid",
        "El estado de la misión no es válido.",
        new Dictionary<string, string[]> { ["status"] = ["El estado de la misión no es válido."] });

    public static MissionError Forbidden() => new(
        MissionErrorType.Forbidden, "missions.forbidden", "No tienes permiso para realizar esta operación.");

    public static MissionError CampaignNotFound() => new(
        MissionErrorType.NotFound, "missions.campaign_not_found", "La campaña no existe.");

    public static MissionError MissionNotFound() => new(
        MissionErrorType.NotFound, "missions.mission_not_found", "La misión no existe.");

    public static MissionError ActiveCharacterRequired() => new(
        MissionErrorType.Conflict,
        "missions.active_character_required",
        "Necesitas un personaje activo para registrar una misión.");

    public static MissionError ActiveMissionRequired() => new(
        MissionErrorType.Conflict,
        "missions.active_mission_required",
        "Solo una misión activa puede marcarse como principal.");

    public static string Outcome(MissionError error) => error.Type switch
    {
        MissionErrorType.Validation => "validation",
        MissionErrorType.Forbidden => "forbidden",
        MissionErrorType.NotFound => "not_found",
        MissionErrorType.Conflict => "conflict",
        _ => "failure",
    };
}
