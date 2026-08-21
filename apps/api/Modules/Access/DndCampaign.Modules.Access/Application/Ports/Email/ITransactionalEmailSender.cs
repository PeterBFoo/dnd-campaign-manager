namespace DndCampaign.Modules.Access.Application.Ports.Email;

internal sealed record TransactionalEmail(
    string RecipientEmail,
    string? RecipientName,
    string Subject,
    string TextContent,
    string HtmlContent,
    string Category,
    string CorrelationId);

internal sealed record TransactionalEmailReceipt(string ProviderMessageId);

internal interface ITransactionalEmailSender
{
    Task<TransactionalEmailReceipt> SendAsync(
        TransactionalEmail email,
        CancellationToken cancellationToken = default);
}

internal sealed class TransactionalEmailDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
