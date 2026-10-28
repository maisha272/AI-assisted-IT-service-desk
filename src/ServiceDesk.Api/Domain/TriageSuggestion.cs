namespace ServiceDesk.Api.Domain;

public enum TriageStatus
{
    /// <summary>Waiting for an agent to review. Nothing has been applied to the ticket yet.</summary>
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// An AI suggestion for a ticket, plus the human decision about it. Both the original suggestion and
/// the final (possibly edited) values are kept, so we can measure how often agents change the AI's
/// answer — a simple, honest quality metric for the AI feature.
/// </summary>
public class TriageSuggestion
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public TriageStatus Status { get; set; } = TriageStatus.Pending;

    // ---- What the AI suggested ----
    public TicketCategory SuggestedCategory { get; set; }
    public TicketPriority SuggestedPriority { get; set; }
    public string Summary { get; set; } = "";

    /// <summary>Suggested solution steps, one per line.</summary>
    public string Steps { get; set; } = "";

    /// <summary>Short explanation from the model of why it chose this category/priority.</summary>
    public string Rationale { get; set; } = "";

    /// <summary>"low" | "medium" | "high" — the model's self-reported confidence.</summary>
    public string Confidence { get; set; } = "medium";

    /// <summary>Knowledge articles used, as JSON: [{"id":3,"title":"...","score":0.62}].</summary>
    public string RelatedArticlesJson { get; set; } = "[]";

    /// <summary>Which engine produced it, e.g. "mock" or "openai:gpt-4o-mini".</summary>
    public string Provider { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    // ---- The human decision ----
    public int? ReviewedById { get; set; }
    public User? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public TicketCategory? FinalCategory { get; set; }
    public TicketPriority? FinalPriority { get; set; }
    public string? FinalSummary { get; set; }
    public string? FinalSteps { get; set; }

    /// <summary>True if the agent changed anything before approving.</summary>
    public bool WasEdited { get; set; }

    public string? RejectionReason { get; set; }
}
