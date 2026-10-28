using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace ServiceDesk.Tests.Api;

/// <summary>
/// Boots the real API in memory (the full ASP.NET Core pipeline, controllers, EF Core) against a
/// throwaway SQLite database file, seeded with the demo data. Each test class gets a fresh database.
/// </summary>
public class ServiceDeskApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"servicedesk-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting (rather than ConfigureAppConfiguration) so values are visible while Program.cs
        // is still registering services.
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:RecreateOnStartup", "true");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
