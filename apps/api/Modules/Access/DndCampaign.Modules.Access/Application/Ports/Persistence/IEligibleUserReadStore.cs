namespace DndCampaign.Modules.Access.Application.Ports.Persistence;

internal interface IEligibleUserReadStore
{
    Task<EligibleUserPage> SearchAsync(
        Guid campaignId,
        Guid actorUserId,
        string? query,
        int offset,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

internal sealed record EligibleUserRecord(Guid UserId, string DisplayName, string Email);

internal sealed record EligibleUserPage(IReadOnlyList<EligibleUserRecord> Items, bool HasMore);
