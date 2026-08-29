using DndCampaign.Modules.Campaigns.Domain.Campaigns;
using Xunit;

namespace DndCampaign.Modules.Campaigns.Tests.Domain;

public sealed class CampaignTests
{
    [Fact]
    public void Campaign_is_created_without_an_adventure_module_and_with_one_dm()
    {
        var dmUserId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);

        var campaign = Campaign.Create("  Mesa del viernes  ", dmUserId, createdAt);

        Assert.Equal("Mesa del viernes", campaign.Name);
        Assert.Equal(dmUserId, campaign.DmUserId);
        Assert.Null(campaign.AdventureModuleId);
        Assert.Equal(createdAt, campaign.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Campaign_rejects_invalid_names(string name) =>
        Assert.Throws<ArgumentException>(() => Campaign.Create(name, Guid.NewGuid(), DateTimeOffset.UtcNow));

    [Fact]
    public void Campaign_rejects_an_empty_dm_identifier() =>
        Assert.Throws<ArgumentException>(() =>
            Campaign.Create("Mesa válida", Guid.Empty, DateTimeOffset.UtcNow));

    [Fact]
    public void Delete_marks_the_campaign_once()
    {
        var campaign = Campaign.Create("Mesa efímera", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var deletedAt = new DateTimeOffset(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);

        campaign.Delete(deletedAt);

        Assert.Equal(deletedAt, campaign.DeletedAt);
        Assert.Throws<InvalidOperationException>(() => campaign.Delete(deletedAt.AddMinutes(1)));
    }
}
