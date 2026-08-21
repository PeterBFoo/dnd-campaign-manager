using DndCampaign.Modules.Access.Domain.Sessions;
using Xunit;

namespace DndCampaign.Modules.Access.Tests.Domain;

public sealed class UserSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void User_session_is_opaque_hashed_and_revocable()
    {
        var issued = UserSession.Issue(Guid.NewGuid(), Now);

        Assert.Equal(43, issued.Token.Length);
        Assert.Equal(64, issued.Session.TokenHash.Length);
        Assert.DoesNotContain(issued.Token, issued.Session.TokenHash, StringComparison.Ordinal);
        Assert.True(issued.Session.IsActive(Now.AddHours(7)));
        Assert.False(issued.Session.IsActive(Now.AddHours(8)));

        issued.Session.Revoke(Now.AddMinutes(1));

        Assert.False(issued.Session.IsActive(Now.AddMinutes(2)));
    }
}
