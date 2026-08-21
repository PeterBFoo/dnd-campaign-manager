using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DndCampaign.Modules.Access.Infrastructure.Security;

internal sealed class AccessSecurityOptions
{
    private const string DevelopmentBootstrapToken = "local-bootstrap-only-change-before-use";
    private static readonly string DevelopmentOutboxKey = Convert.ToBase64String(
        SHA256.HashData(Encoding.UTF8.GetBytes("dnd-campaign-local-outbox-encryption-key")));

    private AccessSecurityOptions(
        string bootstrapToken,
        string outboxEncryptionKey,
        Uri frontendBaseUrl)
    {
        BootstrapToken = bootstrapToken;
        OutboxEncryptionKey = outboxEncryptionKey;
        FrontendBaseUrl = frontendBaseUrl;
    }

    public string BootstrapToken { get; }

    public string OutboxEncryptionKey { get; }

    public Uri FrontendBaseUrl { get; }

    public static AccessSecurityOptions FromConfiguration(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var bootstrapToken = ReadSecret(configuration, "Identity:BootstrapToken");
        var outboxKey = ReadSecret(configuration, "Identity:OutboxEncryptionKey");

        if (environment.IsDevelopment())
        {
            bootstrapToken = string.IsNullOrWhiteSpace(bootstrapToken)
                ? DevelopmentBootstrapToken
                : bootstrapToken;
            outboxKey = string.IsNullOrWhiteSpace(outboxKey)
                ? DevelopmentOutboxKey
                : outboxKey;
        }

        if (bootstrapToken.Length < 32)
        {
            throw new InvalidOperationException(
                "Identity:BootstrapToken must contain at least 32 characters.");
        }

        byte[] decodedKey;
        try
        {
            decodedKey = Convert.FromBase64String(outboxKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Identity:OutboxEncryptionKey must be a Base64 encoded 256-bit key.",
                exception);
        }

        if (decodedKey.Length != 32)
        {
            throw new InvalidOperationException(
                "Identity:OutboxEncryptionKey must decode to exactly 32 bytes.");
        }

        var frontendBaseUrlValue = configuration["Frontend:BaseUrl"];
        if (string.IsNullOrWhiteSpace(frontendBaseUrlValue) && environment.IsDevelopment())
        {
            frontendBaseUrlValue = "http://localhost:4200/";
        }

        if (!Uri.TryCreate(frontendBaseUrlValue, UriKind.Absolute, out var frontendBaseUrl)
            || (frontendBaseUrl.Scheme != Uri.UriSchemeHttp
                && frontendBaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Frontend:BaseUrl must be an absolute HTTP(S) URL.");
        }

        return new AccessSecurityOptions(bootstrapToken, outboxKey, frontendBaseUrl);
    }

    private static string ReadSecret(IConfiguration configuration, string key)
    {
        var file = configuration[$"{key}File"];
        if (string.IsNullOrWhiteSpace(file))
        {
            return configuration[key]?.Trim() ?? string.Empty;
        }

        try
        {
            return File.ReadAllText(file).Trim();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Secret file configured by '{key}File' is not readable.", exception);
        }
    }
}
