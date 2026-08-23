using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DndCampaign.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MissionsDbContext>
{
    public MissionsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MISSIONS_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=dnd_migrations;Username=dnd;Password=dnd";
        var options = new DbContextOptionsBuilder<MissionsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new MissionsDbContext(options);
    }
}
