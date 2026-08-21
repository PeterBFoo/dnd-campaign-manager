using System.Net.Mail;

namespace DndCampaign.Modules.Access.Domain.Accounts;

internal sealed class UserAccount
{
    private UserAccount()
    {
    }

    private UserAccount(
        Guid id,
        string email,
        string displayName,
        bool isPlatformAdmin,
        DateTimeOffset createdAt)
    {
        Id = id;
        Email = NormalizeEmail(email);
        DisplayName = NormalizeDisplayName(displayName);
        IsPlatformAdmin = isPlatformAdmin;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public bool IsPlatformAdmin { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static UserAccount Create(
        string email,
        string displayName,
        bool isPlatformAdmin,
        DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), email, displayName, isPlatformAdmin, createdAt);

    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        try
        {
            return new MailAddress(email.Trim()).Address.ToLowerInvariant();
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The email address is not valid.", nameof(email), exception);
        }
    }

    private static string NormalizeDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var normalized = displayName.Trim();
        if (normalized.Length is < 2 or > 80)
        {
            throw new ArgumentException("The display name must contain between 2 and 80 characters.", nameof(displayName));
        }

        return normalized;
    }
}
