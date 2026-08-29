namespace DndCampaign.Modules.Access.Infrastructure.Events;

internal sealed class EventGridOptions
{
    public const string SectionName = "EventGrid";

    public bool Enabled { get; set; }
    public string? TopicEndpoint { get; set; }
    public string KeyVersion { get; set; } = "v1";
    public string? TenantId { get; set; }
    public string? Audience { get; set; }
    public string DeliveryRole { get; set; } = "AzureEventGridSecureWebhookSubscriber";
}
