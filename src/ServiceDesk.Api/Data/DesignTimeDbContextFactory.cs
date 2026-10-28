using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ServiceDesk.Api.Data;

/// <summary>
/// Used only by the <c>dotnet ef</c> CLI when creating migrations. Migrations are generated for SQL Server
/// (the production database); the connection string is a placeholder because generating a migration
/// doesn't connect to a database.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ServiceDesk;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options);
    }
}
