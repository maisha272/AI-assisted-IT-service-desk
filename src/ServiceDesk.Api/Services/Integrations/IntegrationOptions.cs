namespace ServiceDesk.Api.Services.Integrations;

/// <summary>Configuration section "Integrations". Secrets come from environment variables, never the repo.</summary>
public class IntegrationOptions
{
    public const string SectionName = "Integrations";

    /// <summary>
    /// Teams incoming webhook URL (a Teams "Workflows" webhook). Contains a secret signature, so treat it as
    /// a secret: <c>Integrations__TeamsWebhookUrl</c>. Empty = notifications are logged but not sent.
    /// </summary>
    public string? TeamsWebhookUrl { get; set; }

    /// <summary>
    /// Shared secret the email provider must send in the <c>X-Webhook-Secret</c> header when posting
    /// inbound emails. Empty = the public inbound email endpoint is disabled.
    /// </summary>
    public string? InboundEmailSecret { get; set; }

    /// <summary>Public URL of the app, used for "Open ticket" links in Teams cards.</summary>
    public string PublicUrl { get; set; } = "http://localhost:5173";

    /// <summary>How often the background dispatcher checks for notifications to send.</summary>
    public int DispatchIntervalSeconds { get; set; } = 60;
}
