using Microsoft.EntityFrameworkCore;

namespace ServiceDesk.Api.Data;

public static class DatabaseSetup
{
    /// <summary>
    /// Registers EF Core with the provider chosen in configuration:
    /// <list type="bullet">
    /// <item><c>Sqlite</c> (default) — zero-setup local development and tests.</item>
    /// <item><c>SqlServer</c> — Azure SQL Database in production.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Database:Provider"] ?? "Sqlite";
        var connectionString = config.GetConnectionString("Default") ?? "Data Source=servicedesk.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            else
                options.UseSqlite(connectionString);
        });
        return services;
    }

    /// <summary>Creates/updates the schema and seeds demo data on startup.</summary>
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = app.Configuration;

        if (db.Database.IsSqlite())
        {
            // SQLite is only used for local dev/tests, so we skip migrations and build the schema
            // straight from the model. RecreateOnStartup gives a fresh demo dataset on every run.
            if (config.GetValue("Database:RecreateOnStartup", false))
                await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            await db.Database.MigrateAsync();
        }

        if (config.GetValue("Seed:Enabled", true) && !await db.Users.AnyAsync())
        {
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            SeedData.Populate(db, clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        }
    }
}
