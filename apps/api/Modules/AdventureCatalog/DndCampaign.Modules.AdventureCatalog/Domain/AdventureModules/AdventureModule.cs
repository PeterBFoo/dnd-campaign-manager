namespace DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;

internal enum EditorialOriginKind
{
    Original,
    Licensed,
    Permission,
    PublicDomain,
    FanContentPolicy,
}

internal sealed record EditorialProvenance
{
    private EditorialProvenance()
    {
    }

    private EditorialProvenance(
        EditorialOriginKind originKind,
        string? sourceReference,
        string rightsBasis,
        string? attribution,
        DateTimeOffset verifiedAt,
        Guid verifiedByUserId)
    {
        if (!Enum.IsDefined(originKind))
        {
            throw new ArgumentException("The editorial origin is invalid.", nameof(originKind));
        }

        SourceReference = NormalizeOptional(sourceReference, 2000, nameof(sourceReference));
        if (originKind != EditorialOriginKind.Original && SourceReference is null)
        {
            throw new ArgumentException("A source reference is required for non-original content.", nameof(sourceReference));
        }

        RightsBasis = NormalizeRequired(rightsBasis, 3, 2000, nameof(rightsBasis));
        Attribution = NormalizeOptional(attribution, 2000, nameof(attribution));
        if (verifiedAt == default)
        {
            throw new ArgumentException("A verification timestamp is required.", nameof(verifiedAt));
        }
        if (verifiedByUserId == Guid.Empty)
        {
            throw new ArgumentException("A verifier is required.", nameof(verifiedByUserId));
        }

        OriginKind = originKind;
        VerifiedAt = verifiedAt;
        VerifiedByUserId = verifiedByUserId;
    }

    public EditorialOriginKind OriginKind { get; private init; }

    public string? SourceReference { get; private init; }

    public string RightsBasis { get; private init; } = string.Empty;

    public string? Attribution { get; private init; }

    public DateTimeOffset VerifiedAt { get; private init; }

    public Guid VerifiedByUserId { get; private init; }

    public static EditorialProvenance Create(
        EditorialOriginKind originKind,
        string? sourceReference,
        string rightsBasis,
        string? attribution,
        DateTimeOffset verifiedAt,
        Guid verifiedByUserId) => new(
            originKind,
            sourceReference,
            rightsBasis,
            attribution,
            verifiedAt,
            verifiedByUserId);

    private static string NormalizeRequired(string value, int minimum, int maximum, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var normalized = value.Trim();
        return normalized.Length >= minimum && normalized.Length <= maximum
            ? normalized
            : throw new ArgumentException(
                $"The value must contain between {minimum} and {maximum} characters.", parameter);
    }

    private static string? NormalizeOptional(string? value, int maximum, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maximum
            ? normalized
            : throw new ArgumentException($"The value cannot exceed {maximum} characters.", parameter);
    }
}

internal sealed record AdventureModuleCover
{
    private AdventureModuleCover()
    {
    }

    private AdventureModuleCover(string objectKey, string contentType, long sizeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            throw new ArgumentException("The cover content type is invalid.", nameof(contentType));
        }
        if (sizeBytes is < 1 or > 10 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        }

        ObjectKey = objectKey;
        ContentType = contentType;
        SizeBytes = sizeBytes;
    }

    public string ObjectKey { get; private init; } = string.Empty;

    public string ContentType { get; private init; } = string.Empty;

    public long SizeBytes { get; private init; }

    public static AdventureModuleCover Create(string objectKey, string contentType, long sizeBytes) =>
        new(objectKey, contentType, sizeBytes);
}

internal sealed class AdventureModule
{
    private AdventureModule()
    {
    }

    private AdventureModule(
        Guid id,
        string name,
        string? description,
        EditorialProvenance textProvenance,
        AdventureModuleCover? cover,
        EditorialProvenance? coverProvenance,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("A modifying actor is required.", nameof(actorUserId));
        }
        if (now == default)
        {
            throw new ArgumentException("A creation timestamp is required.", nameof(now));
        }

        Id = id;
        SetText(name, description, textProvenance);
        SetCover(cover, coverProvenance);
        CreatedAt = now;
        UpdatedAt = now;
        LastModifiedByUserId = actorUserId;
        Version = 1;
        ChaptersVersion = 1;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public EditorialProvenance TextProvenance { get; private set; } = null!;

    public AdventureModuleCover? Cover { get; private set; }

    public EditorialProvenance? CoverProvenance { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid LastModifiedByUserId { get; private set; }

    public long Version { get; private set; }

    public long ChaptersVersion { get; private set; }

    public void AdvanceChaptersVersion() => ChaptersVersion = checked(ChaptersVersion + 1);

    public static AdventureModule Create(
        Guid id,
        string name,
        string? description,
        EditorialProvenance textProvenance,
        AdventureModuleCover? cover,
        EditorialProvenance? coverProvenance,
        Guid actorUserId,
        DateTimeOffset now) => new(
            id != Guid.Empty ? id : throw new ArgumentException("An identifier is required.", nameof(id)),
            name,
            description,
            textProvenance,
            cover,
            coverProvenance,
            actorUserId,
            now);

    public void Update(
        string name,
        string? description,
        EditorialProvenance textProvenance,
        AdventureModuleCover? replacementCover,
        EditorialProvenance? replacementCoverProvenance,
        bool removeCover,
        Guid actorUserId,
        DateTimeOffset now)
    {
        if (replacementCover is not null && removeCover)
        {
            throw new ArgumentException("A cover cannot be replaced and removed at the same time.");
        }
        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("A modifying actor is required.", nameof(actorUserId));
        }
        if (now < UpdatedAt)
        {
            throw new ArgumentException("The update timestamp cannot move backwards.", nameof(now));
        }

        SetText(name, description, textProvenance);
        if (replacementCover is not null)
        {
            SetCover(replacementCover, replacementCoverProvenance);
        }
        else if (removeCover)
        {
            SetCover(null, null);
        }

        UpdatedAt = now;
        LastModifiedByUserId = actorUserId;
        Version = checked(Version + 1);
    }

    public static string NormalizeNameKey(string name) => NormalizeName(name).ToUpperInvariant();

    private void SetText(string name, string? description, EditorialProvenance textProvenance)
    {
        Name = NormalizeName(name);
        NormalizedName = Name.ToUpperInvariant();
        Description = NormalizeDescription(description);
        TextProvenance = textProvenance ?? throw new ArgumentNullException(nameof(textProvenance));
    }

    private void SetCover(AdventureModuleCover? cover, EditorialProvenance? coverProvenance)
    {
        if ((cover is null) != (coverProvenance is null))
        {
            throw new ArgumentException("Cover metadata and provenance must be supplied together.");
        }
        Cover = cover;
        CoverProvenance = coverProvenance;
    }

    private static string NormalizeName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        return normalized.Length is >= 3 and <= 120
            ? normalized
            : throw new ArgumentException(
                "The adventure module name must contain between 3 and 120 characters.", nameof(value));
    }

    private static string? NormalizeDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        return normalized.Length <= 5000
            ? normalized
            : throw new ArgumentException(
                "The adventure module description cannot exceed 5000 characters.", nameof(value));
    }
}
