using DndCampaign.Modules.Access.Domain.Invitations;
using Xunit;

namespace DndCampaign.Modules.Access.Tests.Domain;

public sealed class InvitationTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 8, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid IssuerId = Guid.Parse("21c30f86-c203-4cd5-9c02-d7ef22b31f6a");
    private static readonly Guid RecipientUserId = Guid.Parse("9e6f1586-a0ae-45ff-a422-3e111eb61b0b");

    [Fact]
    public void Platform_invitation_expires_exactly_seven_days_after_issue()
    {
        var issued = Invitation.IssuePlatform(" Player@Example.com ", IssuerId, IssuedAt);

        Assert.Equal(InvitationKind.Platform, issued.Invitation.Kind);
        Assert.Equal("player@example.com", issued.Invitation.RecipientEmail);
        Assert.Equal(IssuerId, issued.Invitation.IssuedByUserId);
        Assert.Equal(IssuedAt.AddDays(7), issued.Invitation.ExpiresAt);
        Assert.Equal(64, issued.Invitation.TokenHash.Length);
        Assert.True(issued.Invitation.MatchesToken(issued.Token));
    }

    [Fact]
    public void Invitation_cannot_be_accepted_at_the_expiration_instant()
    {
        var issued = Invitation.IssuePlatform("player@example.com", IssuerId, IssuedAt);

        var result = issued.Invitation.Accept(issued.Token, RecipientUserId, IssuedAt.AddDays(7));

        Assert.Equal(InvitationAcceptanceResult.Expired, result);
        Assert.Equal(InvitationStatus.Expired, issued.Invitation.Status);
    }

    [Fact]
    public void Invitation_token_is_valid_only_once()
    {
        var issued = Invitation.IssueCampaign("player@example.com", Guid.NewGuid(), IssuerId, IssuedAt);

        var firstAttempt = issued.Invitation.Accept(issued.Token, RecipientUserId, IssuedAt.AddHours(1));
        var secondAttempt = issued.Invitation.Accept(issued.Token, RecipientUserId, IssuedAt.AddHours(2));

        Assert.Equal(InvitationAcceptanceResult.Accepted, firstAttempt);
        Assert.Equal(InvitationAcceptanceResult.AlreadyFinalized, secondAttempt);
        Assert.Equal(InvitationStatus.Accepted, issued.Invitation.Status);
        Assert.Equal(RecipientUserId, issued.Invitation.AcceptedByUserId);
        Assert.Equal(IssuedAt.AddHours(1), issued.Invitation.AcceptedAt);
    }

    [Fact]
    public void Invalid_token_does_not_change_the_invitation()
    {
        var issued = Invitation.IssuePlatform("player@example.com", IssuerId, IssuedAt);

        var result = issued.Invitation.Accept("a-different-token", RecipientUserId, IssuedAt.AddHours(1));

        Assert.Equal(InvitationAcceptanceResult.InvalidToken, result);
        Assert.Equal(InvitationStatus.Pending, issued.Invitation.Status);
    }

    [Fact]
    public void Revoked_invitation_cannot_be_accepted()
    {
        var issued = Invitation.IssuePlatform("player@example.com", IssuerId, IssuedAt);

        Assert.True(issued.Invitation.Revoke(IssuedAt.AddHours(1)));
        var result = issued.Invitation.Accept(issued.Token, RecipientUserId, IssuedAt.AddHours(2));

        Assert.Equal(InvitationAcceptanceResult.AlreadyFinalized, result);
        Assert.Equal(InvitationStatus.Revoked, issued.Invitation.Status);
    }

    [Fact]
    public void Every_invitation_receives_a_different_256_bit_token()
    {
        var first = Invitation.IssuePlatform("first@example.com", IssuerId, IssuedAt);
        var second = Invitation.IssuePlatform("second@example.com", IssuerId, IssuedAt);

        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(43, first.Token.Length);
        Assert.Equal(43, second.Token.Length);
    }
}
