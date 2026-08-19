namespace DndCampaign.Api.Application.Email;

public sealed record TransactionalEmail(
    string RecipientEmail,
    string? RecipientName,
    string Subject,
    string TextContent,
    string HtmlContent,
    string Category,
    string CorrelationId);

public sealed record TransactionalEmailReceipt(string ProviderMessageId);

public interface ITransactionalEmailSender
{
    Task<TransactionalEmailReceipt> SendAsync(
        TransactionalEmail email,
        CancellationToken cancellationToken = default);
}

public sealed class TransactionalEmailDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
