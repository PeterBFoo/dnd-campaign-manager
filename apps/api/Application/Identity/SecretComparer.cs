using System.Security.Cryptography;
using System.Text;

namespace DndCampaign.Api.Application.Identity;

public static class SecretComparer
{
    public static bool Equals(string expected, string candidate)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var candidateHash = SHA256.HashData(Encoding.UTF8.GetBytes(candidate ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(expectedHash, candidateHash);
    }
}
