namespace DndCampaign.Modules.Characters.Domain.Characters;

internal sealed class PlayerCharacter
{
    public const string DefaultImageUrl = "/images/default-character.svg";

    private PlayerCharacter()
    {
    }

    private PlayerCharacter(
        Guid id,
        Guid campaignId,
        Guid? ownerUserId,
        string name,
        int armorClass,
        int initiative,
        string? imageObjectKey,
        string? imageContentType,
        long? imageSizeBytes,
        bool isActive,
        DateTimeOffset createdAt)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("A character requires a campaign.", nameof(campaignId));
        }

        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("A character requires an owner.", nameof(ownerUserId));
        }

        Id = id;
        CampaignId = campaignId;
        OwnerUserId = ownerUserId;
        Name = NormalizeName(name);
        ArmorClass = ValidateArmorClass(armorClass);
        Initiative = ValidateInitiative(initiative);
        SetImage(imageObjectKey, imageContentType, imageSizeBytes);
        IsActive = ownerUserId is not null && isActive;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    public Guid? OwnerUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int ArmorClass { get; private set; }

    public int Initiative { get; private set; }

    public string? ImageObjectKey { get; private set; }

    public string? ImageContentType { get; private set; }

    public long? ImageSizeBytes { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static PlayerCharacter Create(
        Guid campaignId,
        Guid? ownerUserId,
        string name,
        int armorClass,
        int initiative,
        string? imageObjectKey,
        string? imageContentType,
        long? imageSizeBytes,
        bool isActive,
        DateTimeOffset createdAt) => new(
            Guid.NewGuid(),
            campaignId,
            ownerUserId,
            name,
            armorClass,
            initiative,
            imageObjectKey,
            imageContentType,
            imageSizeBytes,
            isActive,
            createdAt);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void Update(
        string name,
        int armorClass,
        int initiative,
        Guid? ownerUserId,
        string? imageObjectKey,
        string? imageContentType,
        long? imageSizeBytes)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("A character owner identifier cannot be empty.", nameof(ownerUserId));
        }

        Name = NormalizeName(name);
        ArmorClass = ValidateArmorClass(armorClass);
        Initiative = ValidateInitiative(initiative);
        OwnerUserId = ownerUserId;
        SetImage(imageObjectKey, imageContentType, imageSizeBytes);
        if (ownerUserId is null)
        {
            IsActive = false;
        }
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length is < 2 or > 80)
        {
            throw new ArgumentException("The character name must contain between 2 and 80 characters.", nameof(name));
        }

        return normalized;
    }

    private static int ValidateArmorClass(int armorClass) => armorClass is >= 0 and <= 40
        ? armorClass
        : throw new ArgumentOutOfRangeException(nameof(armorClass), "Armor class must be between 0 and 40.");

    private static int ValidateInitiative(int initiative) => initiative is >= -20 and <= 30
        ? initiative
        : throw new ArgumentOutOfRangeException(nameof(initiative), "Initiative must be between -20 and 30.");

    private void SetImage(string? objectKey, string? contentType, long? sizeBytes)
    {
        if (objectKey is null && contentType is null && sizeBytes is null)
        {
            ImageObjectKey = null;
            ImageContentType = null;
            ImageSizeBytes = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(objectKey)
            || objectKey.Length > 512
            || contentType is not ("image/jpeg" or "image/png" or "image/webp")
            || sizeBytes is <= 0 or > 5 * 1024 * 1024)
        {
            throw new ArgumentException("Image metadata is invalid.", nameof(objectKey));
        }

        ImageObjectKey = objectKey;
        ImageContentType = contentType;
        ImageSizeBytes = sizeBytes;
    }
}
