namespace DndCampaign.Modules.Access.Domain.CampaignAccess;

internal enum CampaignRole
{
    Dm,
    Player,
}

internal sealed class CampaignMembership
{
    private CampaignMembership()
    {
    }

    private CampaignMembership(
        Guid id,
        Guid campaignId,
        Guid userId,
        CampaignRole role,
        DateTimeOffset joinedAt)
    {
        Id = id;
        CampaignId = campaignId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    public Guid UserId { get; private set; }

    public CampaignRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public static CampaignMembership JoinAsPlayer(Guid campaignId, Guid userId, DateTimeOffset joinedAt)
    {
        ValidateIdentifiers(campaignId, userId);
        return new CampaignMembership(Guid.NewGuid(), campaignId, userId, CampaignRole.Player, joinedAt);
    }

    public static CampaignMembership CreateDm(Guid campaignId, Guid userId, DateTimeOffset joinedAt)
    {
        ValidateIdentifiers(campaignId, userId);
        return new CampaignMembership(Guid.NewGuid(), campaignId, userId, CampaignRole.Dm, joinedAt);
    }

    private static void ValidateIdentifiers(Guid campaignId, Guid userId)
    {
        if (campaignId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("A campaign membership requires campaign and user identifiers.");
        }
    }
}
