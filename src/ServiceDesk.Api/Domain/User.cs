namespace ServiceDesk.Api.Domain;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public UserRole Role { get; set; }

    /// <summary>Salted PBKDF2 hash produced by ASP.NET Core's PasswordHasher. Never the password itself.</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>Set for client staff; null for agents (agents belong to the MSP, not a client).</summary>
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
}
