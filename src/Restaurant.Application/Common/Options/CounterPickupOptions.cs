namespace Restaurant.Application.Common.Options;

/// <summary>
/// Platform credentials for the counter-ready notice. Secrets stay here, never in tenant rows.
/// WhatsApp uses a pre-approved template whose body is order number then trade name.
/// The message is transactional only: the order is ready for counter pickup.
/// </summary>
public sealed class CounterPickupOptions
{
    public const string SectionName = "CounterPickup";

    public bool WhatsAppEnabled { get; set; }

    public string WhatsAppPhoneNumberId { get; set; } = string.Empty;

    public string WhatsAppAccessToken { get; set; } = string.Empty;

    public string WhatsAppTemplateName { get; set; } = "order_ready";

    public string WhatsAppTemplateLanguage { get; set; } = "es";

    /// <summary>Optional HTTPS endpoint used only when WhatsApp fails. Body is { "to", "message" }.</summary>
    public string SmsFallbackUrl { get; set; } = string.Empty;
}
