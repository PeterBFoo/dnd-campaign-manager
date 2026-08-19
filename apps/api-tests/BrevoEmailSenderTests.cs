using System.Net;
using System.Text;
using System.Text.Json;
using DndCampaign.Api.Application.Email;
using DndCampaign.Api.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class BrevoEmailSenderTests
{
    [Fact]
    public async Task Sends_transactional_email_through_the_brevo_v3_contract()
    {
        var senderSecretFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(
            senderSecretFile,
            "no-reply@example.com\n",
            TestContext.Current.CancellationToken);

        try
        {
            string? requestBody = null;
            string? apiKey = null;
            var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
            {
                requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                apiKey = request.Headers.GetValues("api-key").Single();
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(
                        "{\"messageId\":\"provider-message-id\"}",
                        Encoding.UTF8,
                        "application/json"),
                };
            });
            using var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.brevo.com/v3/"),
            };
            var sender = CreateSender(httpClient, senderSecretFile);

            var receipt = await sender.SendAsync(
                new TransactionalEmail(
                    "player@example.com",
                    "Player",
                    "Invitation",
                    "Open the invitation link.",
                    "<p>Open the invitation link.</p>",
                    "platform-invitation",
                    "test-correlation-id"),
                TestContext.Current.CancellationToken);

            Assert.Equal("provider-message-id", receipt.ProviderMessageId);
            Assert.Equal("test-api-key", apiKey);
            Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.RequestUri?.ToString());
            using var payload = JsonDocument.Parse(requestBody!);
            Assert.Equal(
                "Open the invitation link.",
                payload.RootElement.GetProperty("textContent").GetString());
            Assert.Equal(
                "<p>Open the invitation link.</p>",
                payload.RootElement.GetProperty("htmlContent").GetString());
            Assert.Equal(
                "test-correlation-id",
                payload.RootElement.GetProperty("headers").GetProperty("X-Correlation-Id").GetString());
            Assert.Equal(
                "no-reply@example.com",
                payload.RootElement.GetProperty("sender").GetProperty("email").GetString());
        }
        finally
        {
            File.Delete(senderSecretFile);
        }
    }

    [Fact]
    public async Task Provider_error_does_not_expose_recipient_in_the_public_exception()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.brevo.com/v3/"),
        };
        var sender = CreateSender(httpClient);
        var email = new TransactionalEmail(
            "private-address@example.com",
            null,
            "Invitation",
            "Text",
            "<p>Text</p>",
            "campaign-invitation",
            "test-correlation-id");

        var exception = await Assert.ThrowsAsync<TransactionalEmailDeliveryException>(() =>
            sender.SendAsync(email, TestContext.Current.CancellationToken));

        Assert.DoesNotContain(email.RecipientEmail, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("429", exception.Message, StringComparison.Ordinal);
    }

    private static BrevoEmailSender CreateSender(HttpClient httpClient, string senderEmailFile = "") =>
        new(
            httpClient,
            Options.Create(new BrevoOptions
            {
                ApiKey = "test-api-key",
                SenderEmail = string.IsNullOrEmpty(senderEmailFile) ? "no-reply@example.com" : string.Empty,
                SenderEmailFile = senderEmailFile,
                SenderName = "D&D Campaign Manager",
            }),
            NullLogger<BrevoEmailSender>.Instance);

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return responseFactory(request, cancellationToken);
        }
    }
}
