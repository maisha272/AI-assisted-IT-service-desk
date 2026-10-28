namespace ServiceDesk.Api.Domain;

/// <summary>A customer company whose IT the MSP manages.</summary>
public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Email domain (e.g. "northwind.test") used to match inbound emails to a client.</summary>
    public string EmailDomain { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public List<User> Users { get; set; } = [];
    public List<Ticket> Tickets { get; set; } = [];
}
