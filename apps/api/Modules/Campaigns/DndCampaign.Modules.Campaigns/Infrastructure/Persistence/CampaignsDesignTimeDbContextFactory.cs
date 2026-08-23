using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DndCampaign.Modules.Campaigns.Infrastructure.Persistence;

internal sealed class CampaignsDesignTimeDbContextFactory :
    IDesignTimeDbContextFactory<CampaignsDbContext>
{
    public CampaignsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CAMPAIGNS_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=dnd_migrations;Username=dnd;Password=dnd";
        var options = new DbContextOptionsBuilder<CampaignsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new CampaignsDbContext(options);
    }
}
