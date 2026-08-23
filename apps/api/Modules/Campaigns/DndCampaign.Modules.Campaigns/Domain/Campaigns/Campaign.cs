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
        AdventureModuleId = adventureModuleId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Guid DmUserId { get; private set; }

    public Guid? AdventureModuleId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Campaign Create(string name, Guid dmUserId, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), name, dmUserId, adventureModuleId: null, createdAt);

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
