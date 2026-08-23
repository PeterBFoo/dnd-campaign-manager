using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.Campaigns.Application.Campaigns;
using DndCampaign.Modules.Campaigns.Application.Ports;
using DndCampaign.Modules.Campaigns.Domain.Campaigns;
using Xunit;

namespace DndCampaign.Modules.Campaigns.Tests.Application;

public sealed class CampaignHandlerTests
{
    [Fact]
    public async Task Create_assigns_the_actor_as_dm()
    {
        var repository = new FakeCampaignRepository();
        var handler = new CreateCampaignHandler(repository, new FakeCampaignMetrics(), TimeProvider.System);
        var userId = Guid.NewGuid();

        var result = await handler.HandleAsync(
            new CreateCampaignCommand(userId, "Mesa propia"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("dm", result.Value!.Role);
        Assert.Equal(userId, repository.Items.Single().DmUserId);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task Detail_distinguishes_forbidden_from_not_found()
    {
        var repository = new FakeCampaignRepository();
        var campaign = Campaign.Create("Mesa privada", Guid.NewGuid(), DateTimeOffset.UtcNow);
        repository.Items.Add(campaign);
        var handler = new GetCampaignHandler(repository, new FakePlayerAccessReader(), new FakeCampaignMetrics());

        var forbidden = await handler.HandleAsync(
            new GetCampaignQuery(Guid.NewGuid(), campaign.Id),
            TestContext.Current.CancellationToken);
        var missing = await handler.HandleAsync(
            new GetCampaignQuery(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal("campaign.forbidden", forbidden.Error!.Code);
        Assert.Equal("campaign.not_found", missing.Error!.Code);
    }

    private sealed class FakeCampaignRepository : ICampaignRepository
    {
        public List<Campaign> Items { get; } = [];

        public bool Saved { get; private set; }

        public void Add(Campaign campaign) => Items.Add(campaign);

        public Task<Campaign?> FindAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == campaignId));

        public Task<IReadOnlyList<Campaign>> ListAccessibleAsync(
            Guid dmUserId,
            IReadOnlyCollection<Guid> playerCampaignIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Campaign>>(Items.Where(item =>
                item.DmUserId == dmUserId || playerCampaignIds.Contains(item.Id)).ToArray());

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePlayerAccessReader : IPlayerCampaignAccessReader
    {
        public Task<IReadOnlyCollection<Guid>> ListCampaignIdsAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<bool> HasPlayerAccessAsync(
            Guid campaignId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeCampaignMetrics : ICampaignMetrics
    {
        public void OperationCompleted(string operation, string outcome, double elapsedMilliseconds)
        {
        }
    }
}
