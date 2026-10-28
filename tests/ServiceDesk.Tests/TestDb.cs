using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Tests;

/// <summary>
/// An empty in-memory SQLite database for service-level tests where each test builds exactly the data
/// it needs. The connection must stay open: an in-memory SQLite DB disappears when it closes.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public AppDbContext Db { get; }
    public Client Client { get; }
    public User Agent { get; }
    public User ClientUser { get; }

    public TestDb()
    {
        _connection.Open();
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        Db.Database.EnsureCreated();

        Client = new Client { Name = "Acme", EmailDomain = "acme.test" };
        Agent = new User { Email = "agent@desk.test", DisplayName = "Agent A", Role = UserRole.Agent };
        ClientUser = new User { Email = "user@acme.test", DisplayName = "User U", Role = UserRole.ClientStaff, Client = Client };
        Db.AddRange(Client, Agent, ClientUser);
        Db.SaveChanges();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
