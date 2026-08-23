namespace DndCampaign.Modules.Combat.Domain.Encounters;

internal sealed class EnemyGroupMember
{
    private EnemyGroupMember()
    {
    }

    private EnemyGroupMember(
        Guid id,
        Guid participantId,
        int ordinal,
        int currentHitPoints,
        int maximumHitPoints)
    {
        if (id == Guid.Empty) throw new ArgumentException("An enemy group member requires an identifier.", nameof(id));
        if (participantId == Guid.Empty) throw new ArgumentException("An enemy group member requires a participant.", nameof(participantId));
        if (ordinal is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (maximumHitPoints is < 1 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));
        }
        if (currentHitPoints < 0 || currentHitPoints > maximumHitPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(currentHitPoints));
        }

        Id = id;
        ParticipantId = participantId;
        Ordinal = ordinal;
        CurrentHitPoints = currentHitPoints;
        MaximumHitPoints = maximumHitPoints;
    }

    public Guid Id { get; private set; }

    public Guid ParticipantId { get; private set; }

    public int Ordinal { get; private set; }

    public int CurrentHitPoints { get; private set; }

    public int MaximumHitPoints { get; private set; }

    public static EnemyGroupMember Create(Guid participantId, int ordinal, int maximumHitPoints) =>
        new(Guid.NewGuid(), participantId, ordinal, maximumHitPoints, maximumHitPoints);

    public void AdjustHitPoints(HitPointAdjustmentKind kind, int amount)
    {
        if (amount is < 1 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The amount must be between 1 and 100000.");
        }

        CurrentHitPoints = kind == HitPointAdjustmentKind.Damage
            ? Math.Max(0, CurrentHitPoints - amount)
            : Math.Min(MaximumHitPoints, CurrentHitPoints + amount);
    }
}
