namespace DndCampaign.Modules.Journal.Domain.Entries;

internal sealed class JournalEntry
{
    private JournalEntry()
    {
    }

    private JournalEntry(
        Guid id,
        Guid campaignId,
        Guid createdByUserId,
        Guid authorCharacterId,
        string authorCharacterName,
        string content,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entry requires an identifier.", nameof(id));
        if (campaignId == Guid.Empty) throw new ArgumentException("An entry requires a campaign.", nameof(campaignId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("An entry requires a creator.", nameof(createdByUserId));
        if (authorCharacterId == Guid.Empty) throw new ArgumentException("An entry requires a character.", nameof(authorCharacterId));
        if (createdAt == default) throw new ArgumentException("An entry requires a creation date.", nameof(createdAt));

        Id = id;
        CampaignId = campaignId;
        CreatedByUserId = createdByUserId;
        AuthorCharacterId = authorCharacterId;
        AuthorCharacterName = NormalizeAuthorName(authorCharacterName);
        Content = NormalizeContent(content);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid AuthorCharacterId { get; private set; }

    public string AuthorCharacterName { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public long PaginationSequence { get; private set; }

    public static JournalEntry Create(
        Guid campaignId,
        Guid createdByUserId,
        Guid authorCharacterId,
        string authorCharacterName,
        string content,
        DateTimeOffset createdAt) => new(
            Guid.NewGuid(), campaignId, createdByUserId, authorCharacterId, authorCharacterName, content, createdAt);

    public void UpdateContent(string content, DateTimeOffset updatedAt)
    {
        if (updatedAt < CreatedAt)
        {
            throw new ArgumentException("The update date cannot precede creation.", nameof(updatedAt));
        }

        Content = NormalizeContent(content);
        UpdatedAt = updatedAt;
    }

    private static string NormalizeAuthorName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        return normalized.Length <= 80
            ? normalized
            : throw new ArgumentException("The author name cannot exceed 80 characters.", nameof(name));
    }

    private static string NormalizeContent(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        var normalized = content.Trim();
        return normalized.Length <= 5_000
            ? normalized
            : throw new ArgumentException("The entry content cannot exceed 5000 characters.", nameof(content));
    }
}
