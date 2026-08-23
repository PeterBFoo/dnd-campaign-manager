using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DndCampaign.Modules.Combat.Infrastructure.Persistence;

internal sealed class CombatDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CombatDbContext>
{
    public CombatDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("COMBAT_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=dnd_migrations;Username=dnd;Password=dnd";
        var options = new DbContextOptionsBuilder<CombatDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new CombatDbContext(options);
    }
}
