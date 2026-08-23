namespace DndCampaign.Modules.Missions.Domain.Missions;

internal enum MissionAuthorType
{
    Dm,
    Player,
}

internal enum MissionStatus
{
    Active,
    Completed,
    Failed,
    Cancelled,
}

internal sealed class Mission
{
    private Mission()
    {
    }

    private Mission(
        Guid id,
        Guid campaignId,
        Guid createdByUserId,
        MissionAuthorType authorType,
        Guid? authorCharacterId,
        string? authorCharacterName,
        string title,
        string? description,
        bool isMain,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("A mission requires an identifier.", nameof(id));
        if (campaignId == Guid.Empty) throw new ArgumentException("A mission requires a campaign.", nameof(campaignId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("A mission requires a creator.", nameof(createdByUserId));
        if (createdAt == default) throw new ArgumentException("A mission requires a creation timestamp.", nameof(createdAt));

        ValidateAuthor(authorType, authorCharacterId, authorCharacterName);
        Id = id;
        CampaignId = campaignId;
        CreatedByUserId = createdByUserId;
        AuthorType = authorType;
        AuthorCharacterId = authorCharacterId;
        AuthorCharacterName = authorType == MissionAuthorType.Player
            ? NormalizeAuthorName(authorCharacterName!)
            : null;
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        Status = MissionStatus.Active;
        IsMain = isMain;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public MissionAuthorType AuthorType { get; private set; }

    public Guid? AuthorCharacterId { get; private set; }

    public string? AuthorCharacterName { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public MissionStatus Status { get; private set; }

    public bool IsMain { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public long SortSequence { get; private set; }

    public static Mission CreateForPlayer(
        Guid campaignId,
        Guid createdByUserId,
        Guid authorCharacterId,
        string authorCharacterName,
        string title,
        string? description,
        bool isMain,
        DateTimeOffset createdAt) => new(
            Guid.NewGuid(), campaignId, createdByUserId, MissionAuthorType.Player,
            authorCharacterId, authorCharacterName, title, description, isMain, createdAt);

    public static Mission CreateForDm(
        Guid campaignId,
        Guid createdByUserId,
        string title,
        string? description,
        bool isMain,
        DateTimeOffset createdAt) => new(
            Guid.NewGuid(), campaignId, createdByUserId, MissionAuthorType.Dm,
            null, null, title, description, isMain, createdAt);

    public void Update(string title, string? description, MissionStatus status, DateTimeOffset updatedAt)
    {
        EnsureValidUpdateTime(updatedAt);
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        Status = status;
        if (status != MissionStatus.Active)
        {
            IsMain = false;
        }
        UpdatedAt = updatedAt;
    }

    public void MarkAsMain(DateTimeOffset updatedAt)
    {
        EnsureValidUpdateTime(updatedAt);
        if (Status != MissionStatus.Active)
        {
            throw new InvalidOperationException("Only an active mission can be marked as main.");
        }
        IsMain = true;
        UpdatedAt = updatedAt;
    }

    public bool ClearMain(DateTimeOffset updatedAt)
    {
        EnsureValidUpdateTime(updatedAt);
        if (!IsMain)
        {
            return false;
        }
        IsMain = false;
        UpdatedAt = updatedAt;
        return true;
    }

    private void EnsureValidUpdateTime(DateTimeOffset value)
    {
        if (value < CreatedAt)
        {
            throw new ArgumentException("The update timestamp cannot precede creation.", nameof(value));
        }
    }

    private static void ValidateAuthor(
        MissionAuthorType authorType,
        Guid? authorCharacterId,
        string? authorCharacterName)
    {
        if (authorType == MissionAuthorType.Player
            && (!authorCharacterId.HasValue
                || authorCharacterId.Value == Guid.Empty
                || string.IsNullOrWhiteSpace(authorCharacterName)))
        {
            throw new ArgumentException("A player mission requires an author character.");
        }
        if (authorType == MissionAuthorType.Dm
            && (authorCharacterId is not null || authorCharacterName is not null))
        {
            throw new ArgumentException("A DM mission cannot have an author character.");
        }
    }

    private static string NormalizeAuthorName(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= 80
            ? normalized
            : throw new ArgumentException("The author name cannot exceed 80 characters.", nameof(value));
    }

    private static string NormalizeTitle(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        return normalized.Length is >= 2 and <= 120
            ? normalized
            : throw new ArgumentException("The title must contain between 2 and 120 characters.", nameof(value));
    }

    private static string? NormalizeDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        return normalized.Length <= 5_000
            ? normalized
            : throw new ArgumentException("The description cannot exceed 5000 characters.", nameof(value));
    }
}
