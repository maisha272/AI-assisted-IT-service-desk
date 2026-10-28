namespace ServiceDesk.Api.Domain;

public class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>One-line summary from AI triage, set only once an agent approves it.</summary>
    public string? AiSummary { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketCategory Category { get; set; } = TicketCategory.Other;
    public TicketSource Source { get; set; } = TicketSource.Portal;

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // ---- SLA (see Services/SlaPolicy.cs) ----
    // Deadlines are stored (not just calculated) so overdue/near-breach tickets can be queried in SQL
    // and reports can compare resolution time against the deadline that applied at the time.
    public DateTime ResponseDueAt { get; set; }
    public DateTime ResolutionDueAt { get; set; }

    /// <summary>First agent action (public reply or status change). Stops the response clock.</summary>
    public DateTime? FirstRespondedAt { get; set; }

    // ---- Notification markers (see Services/Integrations/NotificationDispatcher.cs) ----
    // Null = not yet announced. Each notification is sent once, then its marker is set.
    public DateTime? NewTicketNotifiedAt { get; set; }
    public DateTime? AtRiskNotifiedAt { get; set; }
    public DateTime? BreachNotifiedAt { get; set; }

    public List<Comment> Comments { get; set; } = [];
    public List<TicketStatusChange> StatusChanges { get; set; } = [];
}
