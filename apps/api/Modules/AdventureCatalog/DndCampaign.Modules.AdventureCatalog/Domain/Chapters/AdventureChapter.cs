using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;

namespace DndCampaign.Modules.AdventureCatalog.Domain.Chapters;

internal sealed class AdventureChapter
{
    private AdventureChapter() { }

    private AdventureChapter(Guid id, Guid moduleId, string name, string? description,
        int position, EditorialProvenance provenance, Guid actorUserId, DateTimeOffset now)
    {
        if (id == Guid.Empty || moduleId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Identifiers are required.");
        if (position < 1) throw new ArgumentOutOfRangeException(nameof(position));
        Id = id; ModuleId = moduleId; Position = position;
        SetContent(name, description, provenance);
        CreatedAt = UpdatedAt = now != default ? now : throw new ArgumentException("A timestamp is required.");
        LastModifiedByUserId = actorUserId; Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid ModuleId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Position { get; private set; }
    public EditorialProvenance Provenance { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid LastModifiedByUserId { get; private set; }
    public long Version { get; private set; }

    public static AdventureChapter Create(Guid id, Guid moduleId, string name, string? description,
        int position, EditorialProvenance provenance, Guid actorUserId, DateTimeOffset now) =>
        new(id, moduleId, name, description, position, provenance, actorUserId, now);

    public void Update(string name, string? description, EditorialProvenance provenance,
        Guid actorUserId, DateTimeOffset now)
    {
        if (actorUserId == Guid.Empty || now < UpdatedAt) throw new ArgumentException("The update is invalid.");
        SetContent(name, description, provenance);
        LastModifiedByUserId = actorUserId; UpdatedAt = now; Version = checked(Version + 1);
    }

    public void MoveTo(int position)
    {
        if (position < 1) throw new ArgumentOutOfRangeException(nameof(position));
        Position = position;
    }

    private void SetContent(string name, string? description, EditorialProvenance provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        if (Name.Length is < 2 or > 120) throw new ArgumentException("The name must contain between 2 and 120 characters.", nameof(name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (Description?.Length > 20000) throw new ArgumentException("The description cannot exceed 20000 characters.", nameof(description));
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
    }
}
