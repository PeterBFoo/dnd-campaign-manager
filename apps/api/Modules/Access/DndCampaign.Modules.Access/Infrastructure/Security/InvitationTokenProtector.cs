using System.Security.Cryptography;
using System.Text;
using DndCampaign.Modules.Access.Application.Ports.Security;

namespace DndCampaign.Modules.Access.Infrastructure.Security;

internal sealed class InvitationTokenProtector(AccessSecurityOptions options) : IInvitationTokenProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] key = Convert.FromBase64String(options.OutboxEncryptionKey);

    public string Protect(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var plaintext = Encoding.UTF8.GetBytes(token);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[1 + NonceSize + TagSize + ciphertext.Length];
        payload[0] = 1;
        nonce.CopyTo(payload, 1);
        tag.CopyTo(payload, 1 + NonceSize);
        ciphertext.CopyTo(payload, 1 + NonceSize + TagSize);
        CryptographicOperations.ZeroMemory(plaintext);
        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedToken);
        var payload = Convert.FromBase64String(protectedToken);
        if (payload.Length <= 1 + NonceSize + TagSize || payload[0] != 1)
        {
            throw new CryptographicException("The invitation token payload is invalid.");
        }

        var nonce = payload.AsSpan(1, NonceSize);
        var tag = payload.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = payload.AsSpan(1 + NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
