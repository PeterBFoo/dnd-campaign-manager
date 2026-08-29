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
    public void Campaign_can_be_created_with_an_adventure_module()
    {
        var adventureModuleId = Guid.NewGuid();

        var campaign = Campaign.Create(
            "Mesa con módulo",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            adventureModuleId);

        Assert.Equal(adventureModuleId, campaign.AdventureModuleId);
    }

    [Fact]
    public void Campaign_rejects_an_empty_adventure_module_identifier() =>
        Assert.Throws<ArgumentException>(() =>
            Campaign.Create(
                "Mesa válida",
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                Guid.Empty));

    [Fact]
    public void Adventure_module_can_be_assigned_changed_and_removed()
    {
        var campaign = Campaign.Create("Mesa modular", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var firstModuleId = Guid.NewGuid();
        var secondModuleId = Guid.NewGuid();

        Assert.True(campaign.AssignAdventureModule(firstModuleId));
        Assert.Equal(firstModuleId, campaign.AdventureModuleId);
        Assert.True(campaign.AssignAdventureModule(secondModuleId));
        Assert.Equal(secondModuleId, campaign.AdventureModuleId);
        Assert.True(campaign.RemoveAdventureModule());
        Assert.Null(campaign.AdventureModuleId);
    }

    [Fact]
    public void Adventure_module_assignment_and_removal_are_idempotent()
    {
        var adventureModuleId = Guid.NewGuid();
        var campaign = Campaign.Create(
            "Mesa idempotente",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            adventureModuleId);

        Assert.False(campaign.AssignAdventureModule(adventureModuleId));
        Assert.True(campaign.RemoveAdventureModule());
        Assert.False(campaign.RemoveAdventureModule());
    }

    [Fact]
    public void Adventure_module_cannot_change_after_campaign_deletion()
    {
        var campaign = Campaign.Create("Mesa eliminada", Guid.NewGuid(), DateTimeOffset.UtcNow);
        campaign.Delete(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => campaign.AssignAdventureModule(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => campaign.RemoveAdventureModule());
    }

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
