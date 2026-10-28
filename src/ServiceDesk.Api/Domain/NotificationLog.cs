namespace ServiceDesk.Api.Domain;

public enum NotificationEvent
{
    NewTicket,
    SlaAtRisk,
    SlaBreached,
    Test
}

/// <summary>Audit log of every outbound notification attempt (sent, failed, or logged only).</summary>
public class NotificationLog
{
    public int Id { get; set; }
    public string Channel { get; set; } = "Teams";
    public NotificationEvent Event { get; set; }
    public int? TicketId { get; set; }
    public string Message { get; set; } = "";

    /// <summary>True if the webhook accepted it; false if it failed or no webhook is configured.</summary>
    public bool Delivered { get; set; }

    /// <summary>Why it wasn't delivered ("Webhook not configured", or the HTTP error).</summary>
    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; }
}
