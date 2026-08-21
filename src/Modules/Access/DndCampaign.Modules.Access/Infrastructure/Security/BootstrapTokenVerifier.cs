using System.Security.Cryptography;
using System.Text;
using DndCampaign.Modules.Access.Application.Ports.Security;

namespace DndCampaign.Modules.Access.Infrastructure.Security;

internal sealed class BootstrapTokenVerifier(AccessSecurityOptions options) : IBootstrapTokenVerifier
{
    public bool Matches(string candidate)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.BootstrapToken));
        var candidateHash = SHA256.HashData(Encoding.UTF8.GetBytes(candidate ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(expectedHash, candidateHash);
    }
}
