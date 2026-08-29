using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.AdventureCatalog.Contracts.Campaigns;
using DndCampaign.Modules.Campaigns.Application.Abstractions;
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

    [Fact]
    public async Task Only_the_dm_can_delete_a_campaign()
    {
        var repository = new FakeCampaignRepository();
        var dmUserId = Guid.NewGuid();
        var campaign = Campaign.Create("Mesa que termina", dmUserId, DateTimeOffset.UtcNow);
        repository.Items.Add(campaign);
        var handler = new DeleteCampaignHandler(repository, new FakeCampaignMetrics(), TimeProvider.System);

        var forbidden = await handler.HandleAsync(
            new DeleteCampaignCommand(Guid.NewGuid(), campaign.Id),
            TestContext.Current.CancellationToken);
        var deleted = await handler.HandleAsync(
            new DeleteCampaignCommand(dmUserId, campaign.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal("campaign.forbidden", forbidden.Error!.Code);
        Assert.True(deleted.IsSuccess);
        Assert.NotNull(campaign.DeletedAt);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task Create_with_an_existing_module_returns_its_safe_summary()
    {
        var repository = new FakeCampaignRepository();
        var module = new AdventureModuleCampaignSummary(Guid.NewGuid(), "Módulo compartido", "/api/v1/adventure-modules/cover");
        var handler = new CreateCampaignHandler(
            repository,
            new FakeCampaignMetrics(),
            TimeProvider.System,
            new FakeAdventureModuleReader(module));

        var result = await handler.HandleAsync(
            new CreateCampaignCommand(Guid.NewGuid(), "Mesa con módulo", module.Id),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(module.Id, result.Value!.AdventureModuleId);
        Assert.Equal(module, result.Value.AdventureModule);
        Assert.Equal(1, result.Value.Version);
    }

    [Fact]
    public async Task Assignment_requires_the_dm_and_the_current_version()
    {
        var repository = new FakeCampaignRepository();
        var dm = Guid.NewGuid();
        var campaign = Campaign.Create("Mesa", dm, DateTimeOffset.UtcNow);
        repository.Items.Add(campaign);
        var module = new AdventureModuleCampaignSummary(Guid.NewGuid(), "Módulo", null);
        var handler = new AssignAdventureModuleHandler(
            repository,
            new FakeAdventureModuleReader(module),
            new FakeCampaignMetrics());

        var forbidden = await handler.HandleAsync(
            new AssignAdventureModuleCommand(Guid.NewGuid(), campaign.Id, module.Id, campaign.Version),
            TestContext.Current.CancellationToken);
        var conflict = await handler.HandleAsync(
            new AssignAdventureModuleCommand(dm, campaign.Id, module.Id, campaign.Version + 1),
            TestContext.Current.CancellationToken);
        var assigned = await handler.HandleAsync(
            new AssignAdventureModuleCommand(dm, campaign.Id, module.Id, campaign.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(CampaignErrorType.Forbidden, forbidden.Error!.Type);
        Assert.Equal(CampaignErrorType.Conflict, conflict.Error!.Type);
        Assert.True(assigned.IsSuccess);
        Assert.Equal(module.Id, campaign.AdventureModuleId);
        Assert.Equal(2, campaign.Version);
    }

    [Fact]
    public async Task Removing_a_module_is_idempotent_but_changes_the_version_only_once()
    {
        var repository = new FakeCampaignRepository();
        var dm = Guid.NewGuid();
        var campaign = Campaign.Create("Mesa", dm, DateTimeOffset.UtcNow);
        repository.Items.Add(campaign);
        var handler = new RemoveAdventureModuleHandler(repository, new FakeCampaignMetrics());

        var first = await handler.HandleAsync(
            new RemoveAdventureModuleCommand(dm, campaign.Id, campaign.Version),
            TestContext.Current.CancellationToken);
        var second = await handler.HandleAsync(
            new RemoveAdventureModuleCommand(dm, campaign.Id, campaign.Version),
            TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Null(campaign.AdventureModuleId);
        Assert.Equal(1, campaign.Version);
    }

    private sealed class FakeCampaignRepository : ICampaignRepository
    {
        public List<Campaign> Items { get; } = [];

        public bool Saved { get; private set; }

        public void Add(Campaign campaign) => Items.Add(campaign);

        public Task<Campaign?> FindAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == campaignId));

        public Task<Campaign?> FindForUpdateAsync(
            Guid campaignId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == campaignId && item.DeletedAt is null));

        public Task<IReadOnlyList<Campaign>> ListAccessibleAsync(
            Guid dmUserId,
            IReadOnlyCollection<Guid> playerCampaignIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Campaign>>(Items.Where(item =>
                item.DeletedAt is null
                && (item.DmUserId == dmUserId || playerCampaignIds.Contains(item.Id))).ToArray());

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

    private sealed class FakeAdventureModuleReader(AdventureModuleCampaignSummary? module) : IAdventureModuleCampaignReader
    {
        public Task<IReadOnlyList<AdventureModuleCampaignSummary>> ListOptionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdventureModuleCampaignSummary>>(module is null ? [] : [module]);

        public Task<AdventureModuleCampaignSummary?> FindAsync(
            Guid moduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(module?.Id == moduleId ? module : null);

        public Task<IReadOnlyList<AdventureModuleCampaignSummary>> ListAsync(
            IReadOnlyCollection<Guid> moduleIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdventureModuleCampaignSummary>>(
                module is not null && moduleIds.Contains(module.Id) ? [module] : []);
    }

    private sealed class FakeCampaignMetrics : ICampaignMetrics
    {
        public void OperationCompleted(string operation, string outcome, double elapsedMilliseconds)
        {
        }
    }
}
