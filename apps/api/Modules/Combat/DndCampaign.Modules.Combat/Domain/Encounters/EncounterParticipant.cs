namespace DndCampaign.Modules.Combat.Domain.Encounters;

internal enum EncounterParticipantKind
{
    Character,
    Enemy,
}

internal enum HitPointAdjustmentKind
{
    Damage,
    Healing,
}

internal sealed class EncounterParticipant
{
    private readonly List<EnemyGroupMember> _enemyMembers = [];

    private EncounterParticipant()
    {
    }

    private EncounterParticipant(
        Guid id,
        Guid encounterId,
        EncounterParticipantKind kind,
        Guid? sourceCharacterId,
        string nameSnapshot,
        int armorClass,
        int initiativeTotal,
        long createdOrder)
    {
        if (id == Guid.Empty) throw new ArgumentException("A participant requires an identifier.", nameof(id));
        if (encounterId == Guid.Empty) throw new ArgumentException("A participant requires an encounter.", nameof(encounterId));
        if (createdOrder < 1) throw new ArgumentOutOfRangeException(nameof(createdOrder));

        Id = id;
        EncounterId = encounterId;
        Kind = kind;
        SourceCharacterId = sourceCharacterId;
        NameSnapshot = NormalizeName(nameSnapshot);
        ArmorClass = ValidateArmorClass(armorClass);
        InitiativeTotal = ValidateInitiative(initiativeTotal);
        CreatedOrder = createdOrder;

        if (kind == EncounterParticipantKind.Character)
        {
            if (sourceCharacterId is null || sourceCharacterId == Guid.Empty)
            {
                throw new ArgumentException("A character participant requires its source character.", nameof(sourceCharacterId));
            }
        }
        else
        {
            if (sourceCharacterId is not null)
            {
                throw new ArgumentException("An enemy cannot reference a character.", nameof(sourceCharacterId));
            }
        }
    }

    public Guid Id { get; private set; }

    public Guid EncounterId { get; private set; }

    public EncounterParticipantKind Kind { get; private set; }

    public Guid? SourceCharacterId { get; private set; }

    public string NameSnapshot { get; private set; } = string.Empty;

    public int ArmorClass { get; private set; }

    public int InitiativeTotal { get; private set; }

    public int OrderPosition { get; private set; }

    public long CreatedOrder { get; private set; }

    public IReadOnlyCollection<EnemyGroupMember> EnemyMembers => _enemyMembers;

    public bool IsDefeated => Kind == EncounterParticipantKind.Enemy
        && _enemyMembers.Count > 0
        && _enemyMembers.All(member => member.CurrentHitPoints == 0);

    public static EncounterParticipant CreateCharacter(
        Guid encounterId,
        Guid sourceCharacterId,
        string name,
        int armorClass,
        int initiative,
        long createdOrder) => new(
            Guid.NewGuid(), encounterId, EncounterParticipantKind.Character, sourceCharacterId,
            name, armorClass, initiative, createdOrder);

    public static EncounterParticipant CreateEnemy(
        Guid encounterId,
        string name,
        int armorClass,
        int initiative,
        int maximumHitPoints,
        long createdOrder) => CreateEnemyGroup(
            encounterId, name, armorClass, initiative, maximumHitPoints, 1, createdOrder);

    public static EncounterParticipant CreateEnemyGroup(
        Guid encounterId,
        string name,
        int armorClass,
        int initiative,
        int maximumHitPoints,
        int quantity,
        long createdOrder)
    {
        if (quantity is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Enemy quantity must be between 1 and 100.");
        }
        var participant = new EncounterParticipant(
            Guid.NewGuid(), encounterId, EncounterParticipantKind.Enemy, null,
            name, armorClass, initiative, createdOrder);
        for (var ordinal = 1; ordinal <= quantity; ordinal++)
        {
            participant._enemyMembers.Add(EnemyGroupMember.Create(
                participant.Id, ordinal, ValidateMaximumHitPoints(maximumHitPoints)));
        }
        return participant;
    }

    public void ChangeInitiative(int initiative) => InitiativeTotal = ValidateInitiative(initiative);

    public void SetOrderPosition(int position)
    {
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        OrderPosition = position;
    }

    public void AdjustHitPoints(Guid memberId, HitPointAdjustmentKind kind, int amount)
    {
        if (Kind != EncounterParticipantKind.Enemy)
        {
            throw new InvalidOperationException("Only enemies have hit points in an encounter.");
        }
        var member = _enemyMembers.SingleOrDefault(item => item.Id == memberId)
            ?? throw new KeyNotFoundException("The enemy group member does not exist.");
        member.AdjustHitPoints(kind, amount);
    }

    private static string NormalizeName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        return normalized.Length is >= 2 and <= 80
            ? normalized
            : throw new ArgumentException("The participant name must contain between 2 and 80 characters.", nameof(value));
    }

    private static int ValidateArmorClass(int value) => value is >= 0 and <= 40
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), "Armor class must be between 0 and 40.");

    private static int ValidateInitiative(int value) => value is >= -20 and <= 30
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), "Initiative must be between -20 and 30.");

    private static int ValidateMaximumHitPoints(int value) => value is >= 1 and <= 100_000
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), "Maximum hit points must be between 1 and 100000.");
}
