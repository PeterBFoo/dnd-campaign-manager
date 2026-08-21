using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using DndCampaign.Api.Composition;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class IdentitySecurityTests
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

    [Fact]
    public void Password_policy_requires_length_and_character_groups()
    {
        Assert.NotEmpty(PasswordPolicy.Validate("short"));
        Assert.NotEmpty(PasswordPolicy.Validate("onlylowercaseletters"));
        Assert.Empty(PasswordPolicy.Validate("A-valid-password-123!"));
    }

    [Fact]
    public void Outbox_token_is_encrypted_and_can_be_recovered()
    {
        var protector = new InvitationTokenProtector(CreateOptions());

        var encrypted = protector.Protect("an-opaque-invitation-token");

        Assert.DoesNotContain("an-opaque-invitation-token", encrypted, StringComparison.Ordinal);
        Assert.Equal("an-opaque-invitation-token", protector.Unprotect(encrypted));
    }

    [Fact]
    public void Invitation_link_uses_a_browser_fragment_instead_of_a_query_parameter()
    {
        var issued = Invitation.IssuePlatform("player@example.com", Guid.NewGuid(), Now);
        var email = new InvitationEmailComposer(CreateOptions()).Compose(
            issued.Invitation,
            issued.Token,
            "correlation-id");

        Assert.Contains("accept-invitation#token=", email.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("?token=", email.TextContent, StringComparison.Ordinal);
        Assert.Equal("platform-invitation", email.Category);
    }

    private static IdentitySecurityOptions CreateOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:BootstrapToken"] = "a-bootstrap-token-with-more-than-32-characters",
                ["Identity:OutboxEncryptionKey"] = "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=",
                ["Frontend:BaseUrl"] = "https://example.com/application/",
            })
            .Build();
        return IdentitySecurityOptionsFactory.FromConfiguration(configuration, new TestHostEnvironment());
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "DndCampaign.Api.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
