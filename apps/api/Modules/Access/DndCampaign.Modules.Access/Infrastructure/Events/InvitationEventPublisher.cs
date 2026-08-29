using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using DndCampaign.Modules.Access.Application.Ports.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DndCampaign.Modules.Access.Infrastructure.Events;

internal sealed class InvitationEventPublisher(
    HttpClient httpClient,
    IOptions<EventGridOptions> options,
    TokenCredential credential,
    ILogger<InvitationEventPublisher> logger) : IInvitationEventPublisher
{
    private static readonly TokenRequestContext Scope =
        new(["https://eventgrid.azure.net/.default"]);

    public async Task PublishAsync(
        InvitationEmailRequested message,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        if (!Uri.TryCreate(settings.TopicEndpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvitationEventPublishException("Event Grid topic endpoint is not configured.");
        }

        try
        {
            var token = await credential.GetTokenAsync(Scope, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "/api/events"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Content = JsonContent.Create(
                new CloudEventEnvelope(
                    "1.0",
                    "access.invitation-email.requested.v1",
                    "dnd-campaign/access",
                    message.EventId.ToString("N"),
                    message.OccurredAt,
                    Data: new InvitationEventData(
                        message.InvitationId,
                        message.EncryptedToken,
                        string.IsNullOrWhiteSpace(message.KeyVersion) ? settings.KeyVersion : message.KeyVersion,
                        message.SchemaVersion)),
                new MediaTypeHeaderValue("application/cloudevents+json"));
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvitationEventPublishException(
                    $"Event Grid rejected the event ({(int)response.StatusCode}): {body}");
            }

            logger.LogDebug("Published invitation event {EventId} to Event Grid", message.EventId);
        }
        catch (InvitationEventPublishException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvitationEventPublishException("Event Grid could not accept the invitation event.", exception);
        }
    }

    private sealed record CloudEventEnvelope(
        [property: JsonPropertyName("specversion")] string SpecVersion,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("time")] DateTimeOffset Time,
        [property: JsonPropertyName("datacontenttype")] string DataContentType = "application/json",
        [property: JsonPropertyName("data")] InvitationEventData? Data = null);

    private sealed record InvitationEventData(
        [property: JsonPropertyName("invitationId")] Guid InvitationId,
        [property: JsonPropertyName("encryptedToken")] string EncryptedToken,
        [property: JsonPropertyName("keyVersion")] string KeyVersion,
        [property: JsonPropertyName("schemaVersion")] string SchemaVersion);
}

internal sealed class NullInvitationEventPublisher : IInvitationEventPublisher
{
    public Task PublishAsync(InvitationEmailRequested message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
