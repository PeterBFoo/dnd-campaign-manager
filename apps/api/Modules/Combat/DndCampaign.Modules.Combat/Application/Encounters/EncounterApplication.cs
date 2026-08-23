using System.Diagnostics;
using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Characters.Contracts.CombatParticipants;
using DndCampaign.Modules.Combat.Application.Abstractions;
using DndCampaign.Modules.Combat.Application.Ports;
using DndCampaign.Modules.Combat.Domain.Encounters;

namespace DndCampaign.Modules.Combat.Application.Encounters;

internal sealed record ListEncountersQuery(Guid UserId, Guid CampaignId);
internal sealed record GetEncounterQuery(Guid UserId, Guid CampaignId, Guid EncounterId);
internal sealed record GetActiveEncounterQuery(Guid UserId, Guid CampaignId);
internal sealed record CreateEncounterCommand(Guid UserId, Guid CampaignId, string? Name);
internal sealed record RenameEncounterCommand(
    Guid UserId, Guid CampaignId, Guid EncounterId, string? Name, long? ExpectedVersion);
internal sealed record AddCharacterCommand(
    Guid UserId, Guid CampaignId, Guid EncounterId, Guid? CharacterId, int? Initiative, long? ExpectedVersion);
internal sealed record AddEnemyCommand(
    Guid UserId, Guid CampaignId, Guid EncounterId, string? Name, int? Initiative,
    int? ArmorClass, int? MaximumHitPoints, int? Quantity, long? ExpectedVersion);
internal sealed record ChangeInitiativeCommand(
    Guid UserId, Guid CampaignId, Guid EncounterId, Guid ParticipantId,
    int? Initiative, long? ExpectedVersion);
internal sealed record RemoveParticipantCommand(
    Guid UserId, Guid CampaignId, Guid EncounterId, Guid ParticipantId, long? ExpectedVersion);
internal sealed record ConfirmInitiativeOrderCommand(
    Guid UserId, Guid CampaignId, Guid EncounterId,
    IReadOnlyList<Guid>? ParticipantIds, long? ExpectedVersion);
internal sealed record ActivateEncounterCommand(
    Guid UserId, Guid CampaignId, Guid EncounterId, long? ExpectedVersion);
internal sealed record AdvanceTurnCommand(
    Guid UserId, Guid CampaignId, Guid EncounterId, long? ExpectedVersion);
internal sealed record AdjustEnemyHitPointsCommand(
    Guid UserId, Guid CampaignId, Guid EncounterId, Guid ParticipantId, Guid MemberId,
    string? Kind, int? Amount, long? ExpectedVersion);
internal sealed record FinishEncounterCommand(
    Guid UserId, Guid CampaignId, Guid EncounterId, long? ExpectedVersion);
internal sealed record DeleteEncounterCommand(
    Guid UserId, Guid CampaignId, Guid EncounterId, long? ExpectedVersion);

internal sealed record EncounterSummaryDto(
    Guid Id,
    string Name,
    string Status,
    int ParticipantCount,
    bool TiesResolved,
    int? Round,
    string? CurrentParticipantName,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? FinishedAt);

internal sealed record EncountersDto(IReadOnlyList<EncounterSummaryDto> Items);

internal sealed record DmEnemyMemberDto(
    Guid Id,
    int Ordinal,
    int CurrentHitPoints,
    int MaximumHitPoints);

internal sealed record DmParticipantDto(
    Guid Id,
    Guid? CharacterId,
    string Name,
    string Kind,
    int ArmorClass,
    int Initiative,
    int OrderPosition,
    int Quantity,
    IReadOnlyList<DmEnemyMemberDto> Members,
    bool IsCurrentTurn);

internal sealed record DmEncounterDto(
    Guid Id,
    Guid CampaignId,
    string Name,
    string Status,
    int? Round,
    Guid? CurrentParticipantId,
    bool TiesResolved,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<DmParticipantDto> Participants);

internal sealed record ActiveParticipantDto(
    string Name,
    string Kind,
    int Initiative,
    int OrderPosition,
    int Quantity,
    bool IsCurrentTurn);

internal sealed record ActiveEncounterDto(
    Guid Id,
    string Name,
    int Round,
    string CurrentParticipantName,
    IReadOnlyList<ActiveParticipantDto> Participants);

internal sealed record ActiveEncounterResponse(ActiveEncounterDto? Encounter);

internal sealed class EncounterApplication(
    ICampaignAccessReader campaignAccess,
    ICombatCharacterReader characters,
    IEncounterRepository encounters,
    ICombatMetrics metrics,
    TimeProvider timeProvider)
{
    public Task<CombatResult<EncountersDto>> ListAsync(
        ListEncountersQuery query,
        CancellationToken cancellationToken = default) => TrackAsync("list", async () =>
    {
        var authorization = await AuthorizeAsync(query.CampaignId, query.UserId, dmRequired: true, cancellationToken);
        if (authorization is not null) return CombatResult<EncountersDto>.Failure(authorization);
        var values = await encounters.ListAsync(query.CampaignId, cancellationToken);
        return CombatResult<EncountersDto>.Success(new EncountersDto(
            values.Select(EncounterMapping.ToSummary).ToArray()));
    });

    public Task<CombatResult<DmEncounterDto>> GetAsync(
        GetEncounterQuery query,
        CancellationToken cancellationToken = default) => TrackAsync("get", async () =>
    {
        var authorization = await AuthorizeAsync(query.CampaignId, query.UserId, dmRequired: true, cancellationToken);
        if (authorization is not null) return CombatResult<DmEncounterDto>.Failure(authorization);
        var encounter = await encounters.FindAsync(
            query.CampaignId, query.EncounterId, tracking: false, cancellationToken);
        return encounter is null
            ? CombatResult<DmEncounterDto>.Failure(CombatErrors.EncounterNotFound())
            : CombatResult<DmEncounterDto>.Success(EncounterMapping.ToDmDto(encounter));
    });

    public Task<CombatResult<ActiveEncounterResponse>> GetActiveAsync(
        GetActiveEncounterQuery query,
        CancellationToken cancellationToken = default) => TrackAsync("get_active", async () =>
    {
        var authorization = await AuthorizeAsync(query.CampaignId, query.UserId, dmRequired: false, cancellationToken);
        if (authorization is not null) return CombatResult<ActiveEncounterResponse>.Failure(authorization);
        var encounter = await encounters.FindActiveAsync(query.CampaignId, cancellationToken);
        return CombatResult<ActiveEncounterResponse>.Success(new ActiveEncounterResponse(
            encounter is null ? null : EncounterMapping.ToActiveDto(encounter)));
    });

    public Task<CombatResult<DmEncounterDto>> CreateAsync(
        CreateEncounterCommand command,
        CancellationToken cancellationToken = default) => TrackAsync("create", async () =>
    {
        var authorization = await AuthorizeAsync(command.CampaignId, command.UserId, dmRequired: true, cancellationToken);
        if (authorization is not null) return CombatResult<DmEncounterDto>.Failure(authorization);
        try
        {
            var encounter = Encounter.Create(command.CampaignId, command.Name!, timeProvider.GetUtcNow());
            encounters.Add(encounter);
            await encounters.SaveChangesAsync(cancellationToken);
            return CombatResult<DmEncounterDto>.Success(EncounterMapping.ToDmDto(encounter));
        }
        catch (ArgumentException exception)
        {
            return CombatResult<DmEncounterDto>.Failure(CombatErrors.Invalid("encounter", exception.Message));
        }
    });

    public Task<CombatResult<DmEncounterDto>> RenameAsync(
        RenameEncounterCommand command,
        CancellationToken cancellationToken = default) => MutateAsync(
            "rename", command.UserId, command.CampaignId, command.EncounterId, command.ExpectedVersion,
            encounter => encounter.Rename(command.Name!), cancellationToken);

    public Task<CombatResult<DmEncounterDto>> AddCharacterAsync(
        AddCharacterCommand command,
        CancellationToken cancellationToken = default) => TrackAsync("add_character", async () =>
    {
        var loaded = await LoadForMutationAsync(
            command.UserId, command.CampaignId, command.EncounterId, command.ExpectedVersion, cancellationToken);
        if (loaded.Error is not null) return CombatResult<DmEncounterDto>.Failure(loaded.Error);
        if (command.CharacterId is null || command.CharacterId == Guid.Empty || command.Initiative is null)
        {
            return CombatResult<DmEncounterDto>.Failure(CombatErrors.Invalid(
                "character", "El personaje y su iniciativa son obligatorios."));
        }
        var snapshot = await characters.GetAsync(
            command.CampaignId, command.CharacterId.Value, cancellationToken);
        if (snapshot is null)
        {
            return CombatResult<DmEncounterDto>.Failure(CombatErrors.CharacterNotFound());
        }
        return await ApplyMutationAsync(loaded.Encounter!, encounter => encounter.AddCharacter(
            snapshot.CharacterId, snapshot.Name, snapshot.ArmorClass, command.Initiative.Value), cancellationToken);
    });

    public Task<CombatResult<DmEncounterDto>> AddEnemyAsync(
        AddEnemyCommand command,
        CancellationToken cancellationToken = default) => MutateAsync(
            "add_enemy", command.UserId, command.CampaignId, command.EncounterId, command.ExpectedVersion,
            encounter => encounter.AddEnemyGroup(
                command.Name!, command.ArmorClass ?? int.MinValue,
                command.Initiative ?? int.MinValue, command.MaximumHitPoints ?? int.MinValue,
                command.Quantity ?? int.MinValue),
            cancellationToken);

    public Task<CombatResult<DmEncounterDto>> ChangeInitiativeAsync(
        ChangeInitiativeCommand command,
        CancellationToken cancellationToken = default) => MutateAsync(
            "update_initiative", command.UserId, command.CampaignId, command.EncounterId, command.ExpectedVersion,
            encounter => encounter.ChangeInitiative(command.ParticipantId, command.Initiative ?? int.MinValue),
            cancellationToken);

    public Task<CombatResult<DmEncounterDto>> RemoveParticipantAsync(
        RemoveParticipantCommand command,
        CancellationToken cancellationToken = default) => MutateAsync(
            "remove_participant", command.UserId, command.CampaignId, command.EncounterId, command.ExpectedVersion,
            encounter => encounter.RemoveParticipant(command.ParticipantId), cancellationToken);

    public Task<CombatResult<DmEncounterDto>> ConfirmInitiativeOrderAsync(
        ConfirmInitiativeOrderCommand command,
        CancellationToken cancellationToken = default) => MutateAsync(
            "resolve_order", command.UserId, command.CampaignId, command.EncounterId, command.ExpectedVersion,
            encounter => encounter.ConfirmInitiativeOrder(command.ParticipantIds ?? []), cancellationToken);

    public Task<CombatResult<DmEncounterDto>> ActivateAsync(
        ActivateEncounterCommand command,
        CancellationToken cancellationToken = default) => TrackAsync("activate", async () =>
    {
        var loaded = await LoadForMutationAsync(
            command.UserId, command.CampaignId, command.EncounterId, command.ExpectedVersion, cancellationToken);
        if (loaded.Error is not null) return CombatResult<DmEncounterDto>.Failure(loaded.Error);
        if (await encounters.HasOtherActiveAsync(command.CampaignId, command.EncounterId, cancellationToken))
        {
            return CombatResult<DmEncounterDto>.Failure(CombatErrors.ActiveEncounterExists());
        }
        return await ApplyMutationAsync(
            loaded.Encounter!, encounter => encounter.Activate(timeProvider.GetUtcNow()), cancellationToken);
    });

    public Task<CombatResult<DmEncounterDto>> AdvanceAsync(
        AdvanceTurnCommand command,
        CancellationToken cancellationToken = default) => MutateAsync(
            "advance", command.UserId, command.CampaignId, command.EncounterId, command.ExpectedVersion,
            encounter => encounter.AdvanceTurn(), cancellationToken);

    public Task<CombatResult<DmEncounterDto>> AdjustHitPointsAsync(
        AdjustEnemyHitPointsCommand command,
        CancellationToken cancellationToken = default) => TrackAsync("adjust_hit_points", async () =>
    {
        if (!Enum.TryParse<HitPointAdjustmentKind>(command.Kind, true, out var kind)
            || !Enum.IsDefined(kind))
        {
            return CombatResult<DmEncounterDto>.Failure(CombatErrors.Invalid(
                "kind", "El ajuste debe ser damage o healing."));
        }
        var loaded = await LoadForMutationAsync(
            command.UserId, command.CampaignId, command.EncounterId, command.ExpectedVersion, cancellationToken);
        if (loaded.Error is not null) return CombatResult<DmEncounterDto>.Failure(loaded.Error);
        return await ApplyMutationAsync(
            loaded.Encounter!,
            encounter => encounter.AdjustEnemyHitPoints(
                command.ParticipantId, command.MemberId, kind, command.Amount ?? int.MinValue),
            cancellationToken);
    });

    public Task<CombatResult<DmEncounterDto>> FinishAsync(
        FinishEncounterCommand command,
        CancellationToken cancellationToken = default) => MutateAsync(
            "finish", command.UserId, command.CampaignId, command.EncounterId, command.ExpectedVersion,
            encounter => encounter.Finish(timeProvider.GetUtcNow()), cancellationToken);

    public Task<CombatResult<bool>> DeleteAsync(
        DeleteEncounterCommand command,
        CancellationToken cancellationToken = default) => TrackAsync("delete", async () =>
    {
        var loaded = await LoadForMutationAsync(
            command.UserId, command.CampaignId, command.EncounterId,
            command.ExpectedVersion, cancellationToken);
        if (loaded.Error is not null) return CombatResult<bool>.Failure(loaded.Error);
        try
        {
            loaded.Encounter!.EnsureCanDelete();
            encounters.Remove(loaded.Encounter);
            await encounters.SaveChangesAsync(cancellationToken);
            return CombatResult<bool>.Success(true);
        }
        catch (InvalidOperationException exception)
        {
            return CombatResult<bool>.Failure(CombatErrors.Conflict(exception.Message));
        }
        catch (CombatPersistenceConflictException exception)
        {
            return CombatResult<bool>.Failure(CombatErrors.Conflict(exception.Message));
        }
    });

    private Task<CombatResult<DmEncounterDto>> MutateAsync(
        string operation,
        Guid userId,
        Guid campaignId,
        Guid encounterId,
        long? expectedVersion,
        Action<Encounter> mutation,
        CancellationToken cancellationToken) => TrackAsync(operation, async () =>
    {
        var loaded = await LoadForMutationAsync(
            userId, campaignId, encounterId, expectedVersion, cancellationToken);
        return loaded.Error is not null
            ? CombatResult<DmEncounterDto>.Failure(loaded.Error)
            : await ApplyMutationAsync(loaded.Encounter!, mutation, cancellationToken);
    });

    private async Task<(Encounter? Encounter, CombatError? Error)> LoadForMutationAsync(
        Guid userId,
        Guid campaignId,
        Guid encounterId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeAsync(campaignId, userId, dmRequired: true, cancellationToken);
        if (authorization is not null) return (null, authorization);
        if (expectedVersion is null or < 1)
        {
            return (null, CombatErrors.Invalid("expectedVersion", "La versión esperada es obligatoria."));
        }
        var encounter = await encounters.FindAsync(campaignId, encounterId, tracking: true, cancellationToken);
        if (encounter is null) return (null, CombatErrors.EncounterNotFound());
        return encounter.Version != expectedVersion
            ? (null, CombatErrors.StaleVersion())
            : (encounter, null);
    }

    private async Task<CombatResult<DmEncounterDto>> ApplyMutationAsync(
        Encounter encounter,
        Action<Encounter> mutation,
        CancellationToken cancellationToken)
    {
        try
        {
            mutation(encounter);
            await encounters.SaveChangesAsync(cancellationToken);
            return CombatResult<DmEncounterDto>.Success(EncounterMapping.ToDmDto(encounter));
        }
        catch (KeyNotFoundException)
        {
            return CombatResult<DmEncounterDto>.Failure(CombatErrors.ParticipantNotFound());
        }
        catch (ArgumentException exception)
        {
            return CombatResult<DmEncounterDto>.Failure(CombatErrors.Invalid("encounter", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return CombatResult<DmEncounterDto>.Failure(CombatErrors.Conflict(exception.Message));
        }
        catch (CombatPersistenceConflictException exception)
        {
            return CombatResult<DmEncounterDto>.Failure(CombatErrors.Conflict(exception.Message));
        }
    }

    private async Task<CombatError?> AuthorizeAsync(
        Guid campaignId,
        Guid userId,
        bool dmRequired,
        CancellationToken cancellationToken)
    {
        var access = await campaignAccess.GetAccessAsync(campaignId, userId, cancellationToken);
        if (!access.Exists) return CombatErrors.CampaignNotFound();
        if (access.Role is null || (dmRequired && access.Role != CampaignRole.Dm))
        {
            return CombatErrors.Forbidden();
        }
        return null;
    }

    private async Task<CombatResult<T>> TrackAsync<T>(
        string operation,
        Func<Task<CombatResult<T>>> action)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var result = await action();
            outcome = result.Error is null ? "success" : CombatErrors.Outcome(result.Error);
            return result;
        }
        finally
        {
            metrics.OperationCompleted(operation, outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal static class EncounterMapping
{
    public static EncounterSummaryDto ToSummary(Encounter encounter) => new(
        encounter.Id,
        encounter.Name,
        MapStatus(encounter.Status),
        encounter.Participants.Count,
        encounter.TiesResolved,
        encounter.Round,
        encounter.Participants.SingleOrDefault(participant => participant.Id == encounter.CurrentParticipantId)?.NameSnapshot,
        encounter.Version,
        encounter.CreatedAt,
        encounter.ActivatedAt,
        encounter.FinishedAt);

    public static DmEncounterDto ToDmDto(Encounter encounter) => new(
        encounter.Id,
        encounter.CampaignId,
        encounter.Name,
        MapStatus(encounter.Status),
        encounter.Round,
        encounter.CurrentParticipantId,
        encounter.TiesResolved,
        encounter.Version,
        encounter.CreatedAt,
        encounter.ActivatedAt,
        encounter.FinishedAt,
        encounter.Participants.OrderBy(participant => participant.OrderPosition)
            .Select(participant => new DmParticipantDto(
                participant.Id,
                participant.SourceCharacterId,
                participant.NameSnapshot,
                participant.Kind == EncounterParticipantKind.Character ? "character" : "enemy",
                participant.ArmorClass,
                participant.InitiativeTotal,
                participant.OrderPosition,
                participant.Kind == EncounterParticipantKind.Enemy ? participant.EnemyMembers.Count : 1,
                participant.EnemyMembers.OrderBy(member => member.Ordinal)
                    .Select(member => new DmEnemyMemberDto(
                        member.Id, member.Ordinal, member.CurrentHitPoints, member.MaximumHitPoints))
                    .ToArray(),
                participant.Id == encounter.CurrentParticipantId))
            .ToArray());

    public static ActiveEncounterDto ToActiveDto(Encounter encounter)
    {
        var current = encounter.Participants.Single(participant => participant.Id == encounter.CurrentParticipantId);
        return new ActiveEncounterDto(
            encounter.Id,
            encounter.Name,
            encounter.Round!.Value,
            current.NameSnapshot,
            encounter.Participants.OrderBy(participant => participant.OrderPosition)
                .Select(participant => new ActiveParticipantDto(
                    participant.NameSnapshot,
                    participant.Kind == EncounterParticipantKind.Character ? "character" : "enemy",
                    participant.InitiativeTotal,
                    participant.OrderPosition,
                    participant.Kind == EncounterParticipantKind.Enemy ? participant.EnemyMembers.Count : 1,
                    participant.Id == encounter.CurrentParticipantId))
                .ToArray());
    }

    private static string MapStatus(EncounterStatus status) => status switch
    {
        EncounterStatus.Draft => "draft",
        EncounterStatus.Active => "active",
        EncounterStatus.Finished => "finished",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}

internal static class CombatErrors
{
    public static CombatError Invalid(string field, string description) => new(
        CombatErrorType.Validation,
        "combat.invalid",
        description,
        new Dictionary<string, string[]> { [field] = [description] });

    public static CombatError Forbidden() => new(
        CombatErrorType.Forbidden, "combat.forbidden", "No tienes permiso para realizar esta operación.");

    public static CombatError CampaignNotFound() => new(
        CombatErrorType.NotFound, "combat.campaign_not_found", "La campaña no existe.");

    public static CombatError EncounterNotFound() => new(
        CombatErrorType.NotFound, "combat.encounter_not_found", "El encuentro no existe.");

    public static CombatError ParticipantNotFound() => new(
        CombatErrorType.NotFound, "combat.participant_not_found", "El participante no existe.");

    public static CombatError CharacterNotFound() => new(
        CombatErrorType.NotFound, "combat.character_not_found", "El personaje no existe en esta campaña.");

    public static CombatError ActiveEncounterExists() => new(
        CombatErrorType.Conflict, "combat.active_exists", "Ya existe un encuentro activo en la campaña.");

    public static CombatError StaleVersion() => new(
        CombatErrorType.Conflict, "combat.stale_version", "El encuentro ha cambiado; vuelve a cargarlo.");

    public static CombatError Conflict(string description) => new(
        CombatErrorType.Conflict, "combat.conflict", description);

    public static string Outcome(CombatError error) => error.Type switch
    {
        CombatErrorType.Validation => "validation",
        CombatErrorType.Forbidden => "forbidden",
        CombatErrorType.NotFound => "not_found",
        CombatErrorType.Conflict => "conflict",
        _ => "failure",
    };
}
