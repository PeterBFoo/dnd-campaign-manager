using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;

namespace DndCampaign.Modules.AdventureCatalog.Domain.Locations;

internal sealed class AdventureLocation
{
    private readonly List<AdventurePointOfInterest> pointsOfInterest = [];
    private readonly List<AdventureLocationPlacement> placements = [];
    private readonly List<AdventureLocationChapter> chapters = [];

    private AdventureLocation() { }

    private AdventureLocation(Guid id, Guid moduleId, string name, string? description, Guid actorId, DateTimeOffset now)
    {
        if (id == Guid.Empty || moduleId == Guid.Empty || actorId == Guid.Empty) throw new ArgumentException("Los identificadores son obligatorios.");
        if (now == default) throw new ArgumentException("La fecha es obligatoria.", nameof(now));
        Id = id;
        ModuleId = moduleId;
        SetText(name, description);
        CreatedAt = UpdatedAt = now;
        LastModifiedByUserId = actorId;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid ModuleId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? DetailMapId { get; private set; }
    public Guid? DetailMapModuleId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid LastModifiedByUserId { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyCollection<AdventurePointOfInterest> PointsOfInterest => pointsOfInterest;
    public IReadOnlyCollection<AdventureLocationPlacement> Placements => placements;
    public IReadOnlyCollection<AdventureLocationChapter> Chapters => chapters;

    public static AdventureLocation Create(Guid id, Guid moduleId, string name, string? description, Guid actorId, DateTimeOffset now) =>
        new(id, moduleId, name, description, actorId, now);

    public void Update(string name, string? description, Guid actorId, DateTimeOffset now)
    {
        SetText(name, description);
        Touch(actorId, now);
    }

    public void SetDetailMap(Guid? mapId, Guid actorId, DateTimeOffset now)
    {
        if (mapId == DetailMapId) return;
        DetailMapId = mapId;
        DetailMapModuleId = mapId is null ? null : ModuleId;
        foreach (var point in pointsOfInterest) point.ClearPosition();
        Touch(actorId, now);
    }

    public AdventurePointOfInterest AddPoint(Guid id, string name, string? description, decimal? x, decimal? y, Guid actorId, DateTimeOffset now)
    {
        if (pointsOfInterest.Any(point => point.Id == id)) throw new InvalidOperationException("El punto de interés ya existe.");
        var point = AdventurePointOfInterest.Create(id, ModuleId, Id, name, description, x, y, actorId, now);
        if (point.HasPosition && DetailMapId is null) throw new ArgumentException("No se puede posicionar un POI sin mapa detallado.", nameof(x));
        pointsOfInterest.Add(point); Touch(actorId, now); return point;
    }

    public bool UpdatePoint(Guid pointId, string name, string? description, decimal? x, decimal? y, Guid actorId, DateTimeOffset now)
    {
        var point = pointsOfInterest.SingleOrDefault(item => item.Id == pointId);
        if (point is null) return false;
        if (x.HasValue != y.HasValue) throw new ArgumentException("Las coordenadas deben formar una pareja.");
        if (x.HasValue && DetailMapId is null) throw new ArgumentException("No se puede posicionar un POI sin mapa detallado.", nameof(x));
        point.Update(name, description, x, y, actorId, now); Touch(actorId, now); return true;
    }

    public bool RemovePoint(Guid pointId, Guid actorId, DateTimeOffset now)
    {
        var point = pointsOfInterest.SingleOrDefault(item => item.Id == pointId);
        if (point is null) return false;
        pointsOfInterest.Remove(point); Touch(actorId, now); return true;
    }

    public bool SetChapter(Guid chapterId, Guid actorId, DateTimeOffset now, bool add)
    {
        var existing = chapters.SingleOrDefault(item => item.ChapterId == chapterId);
        if (add)
        {
            if (existing is not null) return false;
            chapters.Add(new AdventureLocationChapter(Id, ModuleId, chapterId)); Touch(actorId, now); return true;
        }
        if (existing is null) return false;
        chapters.Remove(existing); Touch(actorId, now); return true;
    }

    public bool SetPlacement(Guid mapId, decimal x, decimal y, Guid actorId, DateTimeOffset now)
    {
        var existing = placements.SingleOrDefault(item => item.MapId == mapId);
        if (existing is null)
        {
            placements.Add(AdventureLocationPlacement.Create(ModuleId, mapId, Id, x, y));
            Touch(actorId, now); return true;
        }
        if (existing.X == x && existing.Y == y) return false;
        existing.MoveTo(x, y); Touch(actorId, now); return true;
    }

    public bool RemovePlacement(Guid mapId, Guid actorId, DateTimeOffset now)
    {
        var placement = placements.SingleOrDefault(item => item.MapId == mapId);
        if (placement is null) return false;
        placements.Remove(placement); Touch(actorId, now); return true;
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
    {
        if (actorId == Guid.Empty || now < UpdatedAt) throw new ArgumentException("La actualización no es válida.");
        UpdatedAt = now; LastModifiedByUserId = actorId; Version = checked(Version + 1);
    }
}

internal sealed class AdventurePointOfInterest
{
    private AdventurePointOfInterest() { }

    private AdventurePointOfInterest(Guid id, Guid moduleId, Guid locationId, string name, string? description,
        decimal? x, decimal? y, Guid actorId, DateTimeOffset now)
    {
        if (id == Guid.Empty || moduleId == Guid.Empty || locationId == Guid.Empty || actorId == Guid.Empty) throw new ArgumentException("Los identificadores son obligatorios.");
        if (now == default) throw new ArgumentException("La fecha es obligatoria.", nameof(now));
        Id = id; ModuleId = moduleId; LocationId = locationId; SetText(name, description); SetPosition(x, y);
        CreatedAt = UpdatedAt = now; LastModifiedByUserId = actorId; Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid ModuleId { get; private set; }
    public Guid LocationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal? X { get; private set; }
    public decimal? Y { get; private set; }
    public bool HasPosition => X.HasValue && Y.HasValue;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid LastModifiedByUserId { get; private set; }
    public long Version { get; private set; }

    public static AdventurePointOfInterest Create(Guid id, Guid moduleId, Guid locationId, string name, string? description, decimal? x, decimal? y, Guid actorId, DateTimeOffset now) =>
        new(id, moduleId, locationId, name, description, x, y, actorId, now);

    public void Update(string name, string? description, decimal? x, decimal? y, Guid actorId, DateTimeOffset now)
    {
        SetText(name, description); SetPosition(x, y);
        if (actorId == Guid.Empty || now < UpdatedAt) throw new ArgumentException("La actualización no es válida.");
        UpdatedAt = now; LastModifiedByUserId = actorId; Version = checked(Version + 1);
    }

    internal void ClearPosition() { X = null; Y = null; }

    private void SetText(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        if (Name.Length is < 2 or > 120) throw new ArgumentException("El nombre debe contener entre 2 y 120 caracteres.", nameof(name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (Description?.Length > 5000) throw new ArgumentException("La descripción no puede superar 5.000 caracteres.", nameof(description));
    }

    private void SetPosition(decimal? x, decimal? y)
    {
        if (x.HasValue != y.HasValue || (x.HasValue && (x.Value is < 0 or > 1 || y!.Value is < 0 or > 1)))
            throw new ArgumentException("Las coordenadas deben ser una pareja dentro de [0,1].", nameof(x));
        X = x; Y = y;
    }
}

internal sealed class AdventureLocationPlacement
{
    private AdventureLocationPlacement() { }
    private AdventureLocationPlacement(Guid moduleId, Guid mapId, Guid locationId, decimal x, decimal y)
    { ModuleId = moduleId; MapId = mapId; LocationId = locationId; MoveTo(x, y); }
    public Guid ModuleId { get; private set; }
    public Guid MapId { get; private set; }
    public Guid LocationId { get; private set; }
    public decimal X { get; private set; }
    public decimal Y { get; private set; }
    internal static AdventureLocationPlacement Create(Guid moduleId, Guid mapId, Guid locationId, decimal x, decimal y) => new(moduleId, mapId, locationId, x, y);
    internal void MoveTo(decimal x, decimal y)
    {
        if (moduleIdInvalid() || x is < 0 or > 1 || y is < 0 or > 1) throw new ArgumentException("Las coordenadas deben estar dentro de [0,1].");
        X = x; Y = y;
        bool moduleIdInvalid() => ModuleId == Guid.Empty || MapId == Guid.Empty || LocationId == Guid.Empty;
    }
}

internal sealed class AdventureLocationChapter
{
    private AdventureLocationChapter() { }
    internal AdventureLocationChapter(Guid locationId, Guid moduleId, Guid chapterId) { LocationId = locationId; ModuleId = moduleId; ChapterId = chapterId; }
    public Guid LocationId { get; private set; }
    public Guid ModuleId { get; private set; }
    public Guid ChapterId { get; private set; }
}
