namespace ServiceDesk.Api.Domain;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public UserRole Role { get; set; }

    /// <summary>Set for client staff; null for agents (agents belong to the MSP, not a client).</summary>
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
}
