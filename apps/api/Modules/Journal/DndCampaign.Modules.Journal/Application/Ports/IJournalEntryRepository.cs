using DndCampaign.Modules.Journal.Domain.Entries;

namespace DndCampaign.Modules.Journal.Application.Ports;

internal sealed record JournalPageCursor(DateTimeOffset CreatedAt, long PaginationSequence);

internal interface IJournalEntryRepository
{
    Task<IReadOnlyList<JournalEntry>> ListPageAsync(
        Guid campaignId,
        JournalPageCursor? cursor,
        int take,
        CancellationToken cancellationToken = default);

    Task<JournalEntry?> FindForUpdateAsync(
        Guid campaignId,
        Guid entryId,
        CancellationToken cancellationToken = default);

    void Add(JournalEntry entry);

    void Delete(JournalEntry entry);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
