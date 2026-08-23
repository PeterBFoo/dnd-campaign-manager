using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DndCampaign.Modules.Journal.Infrastructure.Persistence;

internal sealed class JournalDesignTimeDbContextFactory : IDesignTimeDbContextFactory<JournalDbContext>
{
    public JournalDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("JOURNAL_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=dnd_migrations;Username=dnd;Password=dnd";
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new JournalDbContext(options);
    }
}
