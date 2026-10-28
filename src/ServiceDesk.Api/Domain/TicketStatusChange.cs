namespace ServiceDesk.Api.Domain;

/// <summary>Audit trail entry: who moved a ticket from one status to another, and when.</summary>
public class TicketStatusChange
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public TicketStatus FromStatus { get; set; }
    public TicketStatus ToStatus { get; set; }

    public int ChangedById { get; set; }
    public User ChangedBy { get; set; } = null!;

    public DateTime ChangedAt { get; set; }
}
