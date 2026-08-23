namespace DndCampaign.Modules.Access.Contracts.CampaignAccess;

public interface ICampaignInvitationContext
{
    Task<CampaignInvitationAccess> GetAccessAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record CampaignInvitationAccess(bool Exists, bool IsDm);
