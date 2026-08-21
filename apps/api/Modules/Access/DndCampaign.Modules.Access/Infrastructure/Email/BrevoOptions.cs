namespace DndCampaign.Modules.Access.Infrastructure.Email;

internal sealed class BrevoOptions
{
    public const string SectionName = "Email:Brevo";

    public string ApiKey { get; set; } = string.Empty;

    public string ApiKeyFile { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderEmailFile { get; set; } = string.Empty;

    public string SenderName { get; set; } = "D&D Campaign Manager";
}
