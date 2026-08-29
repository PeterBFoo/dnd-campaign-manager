using System.Net;
using System.Text.Json;
using Azure.Core;
using DndCampaign.Modules.Access.Application.Ports.Events;
using DndCampaign.Modules.Access.Infrastructure.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DndCampaign.Modules.Access.Tests.Infrastructure;

public sealed class InvitationEventPublisherTests
{
    [Fact]
    public async Task Publishes_a_single_structured_CloudEvent_instead_of_an_array()
    {
        string? requestBody = null;
        string? contentType = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            contentType = request.Content.Headers.ContentType?.MediaType;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = new HttpClient(handler);
        var publisher = new InvitationEventPublisher(
            httpClient,
            Options.Create(new EventGridOptions
            {
                Enabled = true,
                TopicEndpoint = "https://events.example.com/api/events",
                KeyVersion = "v1",
            }),
            new StubTokenCredential(),
            NullLogger<InvitationEventPublisher>.Instance);
        var eventId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();

        await publisher.PublishAsync(
            new InvitationEmailRequested(
                eventId,
                invitationId,
                "encrypted-token",
                "v1",
                DateTimeOffset.Parse("2026-08-30T10:00:00Z")),
            TestContext.Current.CancellationToken);

        Assert.Equal("application/cloudevents+json", contentType);
        using var payload = JsonDocument.Parse(requestBody!);
        Assert.Equal(JsonValueKind.Object, payload.RootElement.ValueKind);
        Assert.Equal("1.0", payload.RootElement.GetProperty("specversion").GetString());
        Assert.Equal(eventId.ToString("N"), payload.RootElement.GetProperty("id").GetString());
        Assert.Equal(
            invitationId,
            payload.RootElement.GetProperty("data").GetProperty("invitationId").GetGuid());
    }

    private sealed class StubTokenCredential : TokenCredential
    {
        private static readonly AccessToken Token =
            new("test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => Token;

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => ValueTask.FromResult(Token);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request, cancellationToken);
    }
}
