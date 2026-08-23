using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DndCampaign.Modules.Characters.Infrastructure.Persistence;

internal sealed class CharactersDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CharactersDbContext>
{
    public CharactersDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CHARACTERS_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=dnd_migrations;Username=dnd;Password=dnd";
        var options = new DbContextOptionsBuilder<CharactersDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new CharactersDbContext(options);
    }
}
