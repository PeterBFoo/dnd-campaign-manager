using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;

namespace DndCampaign.Modules.AdventureCatalog.Domain.Maps;

internal sealed class AdventureMap
{
    private readonly List<AdventureMapChapter> chapters = [];
    private AdventureMap() { }

    private AdventureMap(Guid id, Guid moduleId, string name, string? description, Guid actorId, DateTimeOffset now)
    {
        if (id == Guid.Empty || moduleId == Guid.Empty || actorId == Guid.Empty) throw new ArgumentException("Los identificadores son obligatorios.");
        Id = id; ModuleId = moduleId; SetText(name, description); CreatedAt = UpdatedAt = now; LastModifiedByUserId = actorId; Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid ModuleId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public AdventureMapImage? Image { get; private set; }
    public EditorialProvenance? ImageProvenance { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid LastModifiedByUserId { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyCollection<AdventureMapChapter> Chapters => chapters;

    public static AdventureMap Create(Guid id, Guid moduleId, string name, string? description, Guid actorId, DateTimeOffset now) => new(id, moduleId, name, description, actorId, now);

    public void Update(string name, string? description, Guid actorId, DateTimeOffset now)
    { SetText(name, description); Touch(actorId, now); }

    public void SetImage(AdventureMapImage image, EditorialProvenance provenance, Guid actorId, DateTimeOffset now)
    { Image = image ?? throw new ArgumentNullException(nameof(image)); ImageProvenance = provenance ?? throw new ArgumentNullException(nameof(provenance)); Touch(actorId, now); }

    public void RemoveImage(Guid actorId, DateTimeOffset now)
    { Image = null; ImageProvenance = null; Touch(actorId, now); }

    public bool AddChapter(Guid chapterId, Guid actorId, DateTimeOffset now)
    {
        if (chapters.Any(link => link.ChapterId == chapterId)) return false;
        chapters.Add(new AdventureMapChapter(Id, chapterId)); Touch(actorId, now); return true;
    }

    public bool RemoveChapter(Guid chapterId, Guid actorId, DateTimeOffset now)
    {
        var link = chapters.SingleOrDefault(item => item.ChapterId == chapterId);
        if (link is null) return false;
        chapters.Remove(link); Touch(actorId, now); return true;
    }

    private void SetText(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        if (Name.Length is < 2 or > 120) throw new ArgumentException("El nombre debe contener entre 2 y 120 caracteres.", nameof(name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (Description?.Length > 10000) throw new ArgumentException("La descripción no puede superar 10.000 caracteres.", nameof(description));
    }

    private void Touch(Guid actorId, DateTimeOffset now)
    { if (actorId == Guid.Empty) throw new ArgumentException("El actor es obligatorio."); UpdatedAt = now; LastModifiedByUserId = actorId; Version = checked(Version + 1); }
}

internal sealed record AdventureMapImage
{
    private AdventureMapImage() { }
    private AdventureMapImage(string objectKey, string contentType, long sizeBytes, int width, int height)
    { ObjectKey = objectKey; ContentType = contentType; SizeBytes = sizeBytes; Width = width; Height = height; }
    public string ObjectKey { get; private init; } = string.Empty;
    public string ContentType { get; private init; } = string.Empty;
    public long SizeBytes { get; private init; }
    public int Width { get; private init; }
    public int Height { get; private init; }
    public static AdventureMapImage Create(string key, string type, long size, int width, int height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (type is not ("image/jpeg" or "image/png" or "image/webp") || size is < 1 or > 20 * 1024 * 1024 || width < 1 || height < 1 || (long)width * height > 50_000_000)
            throw new ArgumentException("La imagen del mapa no es válida.");
        return new(key, type, size, width, height);
    }
}

internal sealed class AdventureMapChapter
{
    private AdventureMapChapter() { }
    internal AdventureMapChapter(Guid mapId, Guid chapterId) { MapId = mapId; ChapterId = chapterId; }
    public Guid MapId { get; private set; }
    public Guid ChapterId { get; private set; }
}
