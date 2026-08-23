namespace DndCampaign.Modules.Combat.Domain.Encounters;

internal enum EncounterStatus
{
    Draft,
    Active,
    Finished,
}

internal sealed class Encounter
{
    private readonly List<EncounterParticipant> _participants = [];

    private Encounter()
    {
    }

    private Encounter(Guid id, Guid campaignId, string name, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("An encounter requires an identifier.", nameof(id));
        if (campaignId == Guid.Empty) throw new ArgumentException("An encounter requires a campaign.", nameof(campaignId));
        if (createdAt == default) throw new ArgumentException("An encounter requires a creation timestamp.", nameof(createdAt));

        Id = id;
        CampaignId = campaignId;
        Name = NormalizeName(name);
        CreatedAt = createdAt;
        Status = EncounterStatus.Draft;
        TiesResolved = true;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public EncounterStatus Status { get; private set; }

    public int? Round { get; private set; }

    public Guid? CurrentParticipantId { get; private set; }

    public bool TiesResolved { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public IReadOnlyCollection<EncounterParticipant> Participants => _participants;

    public static Encounter Create(Guid campaignId, string name, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), campaignId, name, createdAt);

    public void Rename(string name)
    {
        EnsureDraft();
        Name = NormalizeName(name);
        Version++;
    }

    public EncounterParticipant AddCharacter(
        Guid characterId,
        string name,
        int armorClass,
        int initiative)
    {
        EnsureDraft();
        if (_participants.Any(participant => participant.SourceCharacterId == characterId))
        {
            throw new InvalidOperationException("The character is already part of this encounter.");
        }
        var participant = EncounterParticipant.CreateCharacter(
            Id, characterId, name, armorClass, initiative, NextCreatedOrder());
        _participants.Add(participant);
        RecalculateOrder();
        Version++;
        return participant;
    }

    public EncounterParticipant AddEnemy(
        string name,
        int armorClass,
        int initiative,
        int maximumHitPoints)
        => AddEnemyGroup(
            name, armorClass, initiative, maximumHitPoints, 1);

    public EncounterParticipant AddEnemyGroup(
        string name,
        int armorClass,
        int initiative,
        int maximumHitPoints,
        int quantity)
    {
        EnsureDraft();
        var participant = EncounterParticipant.CreateEnemyGroup(
            Id, name, armorClass, initiative, maximumHitPoints, quantity, NextCreatedOrder());
        _participants.Add(participant);
        RecalculateOrder();
        Version++;
        return participant;
    }

    public void ChangeInitiative(Guid participantId, int initiative)
    {
        EnsureDraft();
        FindParticipant(participantId).ChangeInitiative(initiative);
        RecalculateOrder();
        Version++;
    }

    public void RemoveParticipant(Guid participantId)
    {
        EnsureDraft();
        var participant = FindParticipant(participantId);
        _participants.Remove(participant);
        RecalculateOrder();
        Version++;
    }

    public void ConfirmInitiativeOrder(IReadOnlyList<Guid> participantIds)
    {
        EnsureDraft();
        if (participantIds.Count != _participants.Count
            || participantIds.Distinct().Count() != _participants.Count)
        {
            throw new ArgumentException("The initiative order must include every participant exactly once.", nameof(participantIds));
        }

        var byId = _participants.ToDictionary(participant => participant.Id);
        var ordered = new List<EncounterParticipant>(_participants.Count);
        foreach (var participantId in participantIds)
        {
            if (!byId.TryGetValue(participantId, out var participant))
            {
                throw new ArgumentException("The initiative order contains an unknown participant.", nameof(participantIds));
            }
            ordered.Add(participant);
        }
        for (var index = 1; index < ordered.Count; index++)
        {
            if (ordered[index - 1].InitiativeTotal < ordered[index].InitiativeTotal)
            {
                throw new ArgumentException("The initiative order must remain descending.", nameof(participantIds));
            }
        }
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].SetOrderPosition(index);
        }
        TiesResolved = true;
        Version++;
    }

    public void Activate(DateTimeOffset activatedAt)
    {
        EnsureDraft();
        if (_participants.Count == 0)
        {
            throw new InvalidOperationException("An encounter requires at least one participant before activation.");
        }
        if (!TiesResolved)
        {
            throw new InvalidOperationException("Initiative ties must be resolved before activation.");
        }
        EnsureTimestamp(activatedAt);
        Status = EncounterStatus.Active;
        Round = 1;
        CurrentParticipantId = OrderedParticipants()[0].Id;
        ActivatedAt = activatedAt;
        Version++;
    }

    public void AdvanceTurn()
    {
        EnsureActive();
        var ordered = OrderedParticipants();
        var currentIndex = ordered.FindIndex(participant => participant.Id == CurrentParticipantId);
        if (currentIndex < 0)
        {
            throw new InvalidOperationException("The current participant is not part of the encounter.");
        }
        var nextIndex = Enumerable.Range(1, ordered.Count)
            .Select(offset => (currentIndex + offset) % ordered.Count)
            .FirstOrDefault(index => !ordered[index].IsDefeated, -1);
        if (nextIndex < 0)
        {
            throw new InvalidOperationException("The encounter has no participant able to take a turn.");
        }
        if (nextIndex <= currentIndex)
        {
            Round = checked(Round!.Value + 1);
        }
        CurrentParticipantId = ordered[nextIndex].Id;
        Version++;
    }

    public void AdjustEnemyHitPoints(
        Guid participantId,
        Guid memberId,
        HitPointAdjustmentKind kind,
        int amount)
    {
        EnsureActive();
        FindParticipant(participantId).AdjustHitPoints(memberId, kind, amount);
        Version++;
    }

    public void EnsureCanDelete()
    {
        if (Status == EncounterStatus.Active)
        {
            throw new InvalidOperationException("An active encounter must be finished before deletion.");
        }
    }

    public void Finish(DateTimeOffset finishedAt)
    {
        EnsureActive();
        EnsureTimestamp(finishedAt);
        Status = EncounterStatus.Finished;
        FinishedAt = finishedAt;
        Version++;
    }

    private void RecalculateOrder()
    {
        var ordered = _participants
            .OrderByDescending(participant => participant.InitiativeTotal)
            .ThenBy(participant => participant.CreatedOrder)
            .ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            ordered[index].SetOrderPosition(index);
        }
        TiesResolved = !_participants
            .GroupBy(participant => participant.InitiativeTotal)
            .Any(group => group.Count() > 1);
    }

    private List<EncounterParticipant> OrderedParticipants() =>
        _participants.OrderBy(participant => participant.OrderPosition).ToList();

    private EncounterParticipant FindParticipant(Guid participantId) =>
        _participants.SingleOrDefault(participant => participant.Id == participantId)
        ?? throw new KeyNotFoundException("The participant does not exist in this encounter.");

    private long NextCreatedOrder() => _participants.Count == 0
        ? 1
        : _participants.Max(participant => participant.CreatedOrder) + 1;

    private void EnsureDraft()
    {
        if (Status != EncounterStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft encounter can be prepared.");
        }
    }

    private void EnsureActive()
    {
        if (Status != EncounterStatus.Active)
        {
            throw new InvalidOperationException("The encounter must be active for this operation.");
        }
    }

    private void EnsureTimestamp(DateTimeOffset value)
    {
        if (value == default || value < CreatedAt)
        {
            throw new ArgumentException("The encounter timestamp is invalid.", nameof(value));
        }
    }

    private static string NormalizeName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        return normalized.Length is >= 2 and <= 120
            ? normalized
            : throw new ArgumentException("The encounter name must contain between 2 and 120 characters.", nameof(value));
    }
}
