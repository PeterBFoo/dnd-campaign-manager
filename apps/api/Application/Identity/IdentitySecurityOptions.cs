using System.Security.Cryptography;
using System.Text;

namespace DndCampaign.Api.Application.Identity;

public sealed class IdentitySecurityOptions
{
    public string BootstrapToken { get; }
    public string OutboxEncryptionKey { get; }
    public Uri FrontendBaseUrl { get; }

    internal IdentitySecurityOptions(string bootstrapToken, string outboxEncryptionKey, Uri frontendBaseUrl)
    {
        BootstrapToken = bootstrapToken;
        OutboxEncryptionKey = outboxEncryptionKey;
        FrontendBaseUrl = frontendBaseUrl;
    }
}
