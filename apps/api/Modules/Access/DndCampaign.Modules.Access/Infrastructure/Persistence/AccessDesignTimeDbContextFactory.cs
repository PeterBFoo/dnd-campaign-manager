using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class AccessDesignTimeDbContextFactory :
    IDesignTimeDbContextFactory<AccessDbContext>
{
    public AccessDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ACCESS_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=dnd_migrations;Username=dnd;Password=dnd";
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AccessDbContext(options);
    }
}
