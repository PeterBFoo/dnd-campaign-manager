using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DndCampaign.Api.Application.Email;
using Microsoft.Extensions.Options;

namespace DndCampaign.Api.Infrastructure.Email;

public sealed class BrevoEmailSender(
    HttpClient httpClient,
    IOptions<BrevoOptions> options,
    ILogger<BrevoEmailSender> logger) : ITransactionalEmailSender
{
    private static readonly Meter Meter = new("DndCampaign.Api.Email", "1.0.0");
    private static readonly Counter<long> SendAttempts = Meter.CreateCounter<long>("email.send.attempts");
    private static readonly Counter<long> SendFailures = Meter.CreateCounter<long>("email.send.failures");
    private static readonly Histogram<double> SendDuration = Meter.CreateHistogram<double>("email.send.duration", "ms");

    private readonly BrevoOptions settings = options.Value;

    public async Task<TransactionalEmailReceipt> SendAsync(
        TransactionalEmail email,
        CancellationToken cancellationToken = default)
    {
        ValidateEmail(email);
        var apiKey = ReadSecret(settings.ApiKey, settings.ApiKeyFile, "Brevo API key");
        var senderEmail = ReadSecret(
            settings.SenderEmail,
            settings.SenderEmailFile,
            "Brevo sender email");
        var startedAt = TimeProvider.System.GetTimestamp();
        SendAttempts.Add(1, new KeyValuePair<string, object?>("email.category", email.Category));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email");
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
            request.Content = JsonContent.Create(new BrevoSendEmailRequest(
                new BrevoSender(senderEmail, settings.SenderName),
                [new BrevoRecipient(email.RecipientEmail, email.RecipientName)],
                email.Subject,
                email.HtmlContent,
                email.TextContent,
                [email.Category],
                new Dictionary<string, string>
                {
                    ["X-Correlation-Id"] = email.CorrelationId,
                }));

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                SendFailures.Add(
                    1,
                    new KeyValuePair<string, object?>("email.category", email.Category),
                    new KeyValuePair<string, object?>("http.response.status_code", (int)response.StatusCode));
                logger.LogWarning(
                    "Brevo rejected transactional email category {EmailCategory} with status {StatusCode}",
                    email.Category,
                    (int)response.StatusCode);
                throw new TransactionalEmailDeliveryException(
                    $"The transactional email provider returned HTTP {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<BrevoSendEmailResponse>(
                cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(result?.MessageId))
            {
                SendFailures.Add(1, new KeyValuePair<string, object?>("email.category", email.Category));
                throw new TransactionalEmailDeliveryException(
                    "The transactional email provider did not return a message identifier.");
            }

            return new TransactionalEmailReceipt(result.MessageId);
        }
        catch (HttpRequestException exception)
        {
            SendFailures.Add(1, new KeyValuePair<string, object?>("email.category", email.Category));
            logger.LogWarning(exception, "Brevo request failed for category {EmailCategory}", email.Category);
            throw new TransactionalEmailDeliveryException(
                "The transactional email provider could not be reached.",
                exception);
        }
        finally
        {
            SendDuration.Record(
                TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("email.category", email.Category));
        }
    }

    private static string ReadSecret(string configuredValue, string configuredFile, string description)
    {
        if (!string.IsNullOrWhiteSpace(configuredFile))
        {
            try
            {
                var secret = File.ReadAllText(configuredFile).Trim();
                if (!string.IsNullOrWhiteSpace(secret))
                {
                    return secret;
                }

                throw new InvalidOperationException($"The configured {description} file is empty.");
            }
            catch (IOException exception)
            {
                throw new InvalidOperationException(
                    $"The configured {description} file could not be read.",
                    exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new InvalidOperationException(
                    $"The configured {description} file is not readable.",
                    exception);
            }
        }

        return !string.IsNullOrWhiteSpace(configuredValue)
            ? configuredValue
            : throw new InvalidOperationException($"{description} configuration is required to send email.");
    }

    private void ValidateEmail(TransactionalEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.RecipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.TextContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.HtmlContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.Category);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.CorrelationId);
    }

    private sealed record BrevoSendEmailRequest(
        [property: JsonPropertyName("sender")] BrevoSender Sender,
        [property: JsonPropertyName("to")] IReadOnlyList<BrevoRecipient> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("htmlContent")] string HtmlContent,
        [property: JsonPropertyName("textContent")] string TextContent,
        [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
        [property: JsonPropertyName("headers")] IReadOnlyDictionary<string, string> Headers);

    private sealed record BrevoSender(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string Name);

    private sealed record BrevoRecipient(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record BrevoSendEmailResponse(
        [property: JsonPropertyName("messageId")] string MessageId);
}
