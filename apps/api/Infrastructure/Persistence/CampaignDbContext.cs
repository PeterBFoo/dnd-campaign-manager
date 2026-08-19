using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Infrastructure.Persistence;

public sealed class CampaignDbContext(DbContextOptions<CampaignDbContext> options)
    : DbContext(options);
