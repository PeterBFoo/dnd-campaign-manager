namespace DndCampaign.Api.Infrastructure.Persistence;

public sealed class InvitationOutboxMessage
{
    private InvitationOutboxMessage()
    {
    }

    private InvitationOutboxMessage(
        Guid id,
        Guid invitationId,
        string encryptedToken,
        DateTimeOffset createdAt)
    {
        Id = id;
        InvitationId = invitationId;
        EncryptedToken = encryptedToken;
        CreatedAt = createdAt;
        NextAttemptAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid InvitationId { get; private set; }

    public string EncryptedToken { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? LeaseUntil { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? LastErrorCode { get; private set; }

    public static InvitationOutboxMessage Create(
        Guid invitationId,
        string encryptedToken,
        DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), invitationId, encryptedToken, createdAt);

    public void Acquire(DateTimeOffset now)
    {
        LeaseUntil = now.AddMinutes(1);
    }

    public void MarkProcessed(string providerMessageId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerMessageId);
        ProviderMessageId = providerMessageId;
        ProcessedAt = now;
        LeaseUntil = null;
        LastErrorCode = null;
        EncryptedToken = string.Empty;
    }

    public void MarkDiscarded(DateTimeOffset now)
    {
        ProviderMessageId = "discarded";
        ProcessedAt = now;
        LeaseUntil = null;
        LastErrorCode = null;
        EncryptedToken = string.Empty;
    }

    public void MarkFailed(string errorCode, DateTimeOffset now)
    {
        Attempts++;
        LastErrorCode = errorCode;
        LeaseUntil = null;
        NextAttemptAt = now.Add(Attempts switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            4 => TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(6),
        });
    }
}
