using DndCampaign.Modules.Journal.Application.Ports;
using DndCampaign.Modules.Journal.Domain.Entries;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Journal.Infrastructure.Persistence;

internal sealed class JournalRepository(JournalDbContext database) : IJournalEntryRepository
{
    public async Task<IReadOnlyList<JournalEntry>> ListPageAsync(
        Guid campaignId,
        JournalPageCursor? cursor,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = database.Entries.AsNoTracking().Where(entry => entry.CampaignId == campaignId);
        if (cursor is not null)
        {
            query = query.Where(entry => entry.CreatedAt < cursor.CreatedAt
                || (entry.CreatedAt == cursor.CreatedAt
                    && entry.PaginationSequence < cursor.PaginationSequence));
        }

        return await query
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.PaginationSequence)
            .Take(take)
            .ToArrayAsync(cancellationToken);
    }

    public Task<JournalEntry?> FindForUpdateAsync(
        Guid campaignId,
        Guid entryId,
        CancellationToken cancellationToken = default) =>
        database.Entries.SingleOrDefaultAsync(
            entry => entry.CampaignId == campaignId && entry.Id == entryId,
            cancellationToken);

    public void Add(JournalEntry entry) => database.Entries.Add(entry);

    public void Delete(JournalEntry entry) => database.Entries.Remove(entry);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        database.SaveChangesAsync(cancellationToken);
}
