using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence;

internal sealed class AdventureCatalogDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<AdventureCatalogDbContext>
{
    public AdventureCatalogDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Campaigns")
            ?? "Host=localhost;Port=5432;Database=dnd_campaigns;Username=dnd_app;Password=local-development-only";
        var options = new DbContextOptionsBuilder<AdventureCatalogDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AdventureCatalogDbContext(options);
    }
}
