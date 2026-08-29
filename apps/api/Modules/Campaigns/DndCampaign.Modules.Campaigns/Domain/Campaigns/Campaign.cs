namespace DndCampaign.Modules.Campaigns.Domain.Campaigns;

internal sealed class Campaign
{
    private Campaign()
    {
    }

    private Campaign(
        Guid id,
        string name,
        Guid dmUserId,
        Guid? adventureModuleId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = NormalizeName(name);
        if (dmUserId == Guid.Empty)
        {
            throw new ArgumentException("A campaign requires a DM.", nameof(dmUserId));
        }

        DmUserId = dmUserId;
        AdventureModuleId = ValidateAdventureModuleId(adventureModuleId);
        CreatedAt = createdAt;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Guid DmUserId { get; private set; }

    public Guid? AdventureModuleId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public long Version { get; private set; }

    public static Campaign Create(
        string name,
        Guid dmUserId,
        DateTimeOffset createdAt,
        Guid? adventureModuleId = null) =>
        new(Guid.NewGuid(), name, dmUserId, adventureModuleId, createdAt);

    public bool AssignAdventureModule(Guid adventureModuleId)
    {
        EnsureActive();
        ValidateAdventureModuleId(adventureModuleId);
        if (AdventureModuleId == adventureModuleId)
        {
            return false;
        }

        AdventureModuleId = adventureModuleId;
        Version = checked(Version + 1);
        return true;
    }

    public bool RemoveAdventureModule()
    {
        EnsureActive();
        if (!AdventureModuleId.HasValue)
        {
            return false;
        }

        AdventureModuleId = null;
        Version = checked(Version + 1);
        return true;
    }

    public void Delete(DateTimeOffset deletedAt)
    {
        EnsureActive();

        DeletedAt = deletedAt;
        Version = checked(Version + 1);
    }

    private void EnsureActive()
    {
        if (DeletedAt.HasValue)
        {
            throw new InvalidOperationException("The campaign has already been deleted.");
        }
    }

    private static Guid? ValidateAdventureModuleId(Guid? adventureModuleId)
    {
        if (adventureModuleId == Guid.Empty)
        {
            throw new ArgumentException(
                "An adventure module identifier cannot be empty.",
                nameof(adventureModuleId));
        }

        return adventureModuleId;
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length is < 3 or > 100)
        {
            throw new ArgumentException(
                "The campaign name must contain between 3 and 100 characters.",
                nameof(name));
        }

        return normalized;
    }
}
