using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DndCampaign.Api.Infrastructure.Persistence;

internal static class CampaignDbContextPersistence
{
    public static Task SaveEntitiesAsync(
        CampaignDbContext database,
        CancellationToken cancellationToken,
        params object[] entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return SaveEntitiesAsync(database, entities, cancellationToken);
    }

    public static async Task SaveEntitiesAsync(
        CampaignDbContext database,
        IReadOnlyCollection<object> entities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(entities);
        if (entities.Any(entity => entity is null))
        {
            throw new ArgumentException("A persistence operation requires concrete entities.", nameof(entities));
        }

        database.ChangeTracker.DetectChanges();
        var intended = entities.ToHashSet();
        var suppressed = CaptureUnrelated(database, intended);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            Restore(suppressed);
        }
    }

    private static List<SuppressedEntry> CaptureUnrelated(
        CampaignDbContext database,
        HashSet<object> intended)
    {
        var suppressed = new List<SuppressedEntry>();
        foreach (var entry in database.ChangeTracker.Entries().ToArray())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            if (intended.Contains(entry.Entity))
            {
                continue;
            }

            suppressed.Add(SuppressedEntry.Capture(entry));
        }

        return suppressed;
    }

    private static void Restore(List<SuppressedEntry> suppressed)
    {
        foreach (var captured in suppressed)
        {
            captured.Restore();
        }
    }

    private sealed class SuppressedEntry
    {
        private SuppressedEntry(EntityEntry entry, EntityState state, IReadOnlyList<SuppressedProperty> properties)
        {
            Entry = entry;
            State = state;
            Properties = properties;
        }

        private EntityEntry Entry { get; }

        private EntityState State { get; }

        private IReadOnlyList<SuppressedProperty> Properties { get; }

        public static SuppressedEntry Capture(EntityEntry entry)
        {
            var properties = entry.Properties
                .Select(property => new SuppressedProperty(
                    property,
                    property.IsModified,
                    property.CurrentValue,
                    property.OriginalValue))
                .ToArray();

            var captured = new SuppressedEntry(entry, entry.State, properties);
            if (entry.State == EntityState.Added)
            {
                entry.State = EntityState.Detached;
            }
            else if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Unchanged;
            }
            else
            {
                foreach (var property in entry.Properties)
                {
                    property.IsModified = false;
                }
            }

            return captured;
        }

        public void Restore()
        {
            if (State == EntityState.Added)
            {
                Entry.Context.Add(Entry.Entity);
                return;
            }

            if (Entry.State == EntityState.Detached)
            {
                Entry.Context.Attach(Entry.Entity);
            }

            foreach (var property in Properties)
            {
                property.Restore();
            }

            Entry.State = State;
        }
    }

    private sealed class SuppressedProperty(
        PropertyEntry property,
        bool isModified,
        object? currentValue,
        object? originalValue)
    {
        public void Restore()
        {
            property.OriginalValue = originalValue;
            property.CurrentValue = currentValue;
            property.IsModified = isModified;
        }
    }
}
