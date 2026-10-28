namespace ServiceDesk.Api.Domain;

public class Comment
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public string Body { get; set; } = "";

    /// <summary>Internal notes are visible to agents only, never to client staff.</summary>
    public bool IsInternal { get; set; }

    public DateTime CreatedAt { get; set; }
}
