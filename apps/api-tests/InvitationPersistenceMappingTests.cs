using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Persistence;
using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class InvitationPersistenceMappingTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 8, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid IssuerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AcceptorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Issue_record_restore_round_trips_business_state()
    {
        var issued = Invitation.IssuePlatform("player@example.com", IssuerId, IssuedAt);
        issued.Invitation.Accept(issued.Token, AcceptorId, IssuedAt.AddHours(1));

        var record = InvitationPersistenceMapping.ToRecord(issued.Invitation);
        var restored = InvitationPersistenceMapping.ToDomain(record);

        Assert.Equal(issued.Invitation.Id, restored.Id);
        Assert.Equal(issued.Invitation.Kind, restored.Kind);
        Assert.Equal(issued.Invitation.RecipientEmail, restored.RecipientEmail);
        Assert.Equal(IssuerId, restored.IssuedByUserId);
        Assert.Equal(InvitationStatus.Accepted, restored.Status);
        Assert.Equal(AcceptorId, restored.AcceptedByUserId);
        Assert.Equal(IssuedAt.AddHours(1), restored.AcceptedAt);
        Assert.True(restored.MatchesToken(issued.Token));
    }
}
