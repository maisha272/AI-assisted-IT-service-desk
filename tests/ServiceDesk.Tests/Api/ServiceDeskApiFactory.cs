using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using ServiceDesk.Api.Controllers;
using ServiceDesk.Api.Data;

namespace ServiceDesk.Tests.Api;

/// <summary>
/// Boots the real API in memory (the full ASP.NET Core pipeline, controllers, EF Core) against a
/// throwaway SQLite database file, seeded with the demo data. Each test class gets a fresh database.
/// </summary>
public class ServiceDeskApiFactory : WebApplicationFactory<Program>
{
    public const string AgentEmail = SeedData.DemoAgentEmail;
    public const string ClientEmail = SeedData.DemoClientEmail;
    public const string OtherClientEmail = "mia@harbourlogistics.test";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"servicedesk-test-{Guid.NewGuid():N}.db");

    // Tokens are cached so tests don't hit the login rate limit.
    private readonly ConcurrentDictionary<string, string> _tokens = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting (rather than ConfigureAppConfiguration) so values are visible while Program.cs
        // is still registering services.
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:RecreateOnStartup", "true");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
    }

    /// <summary>An HttpClient already signed in as the given demo user.</summary>
    public async Task<HttpClient> CreateClientAsAsync(string email)
    {
        var client = CreateClient();
        if (!_tokens.TryGetValue(email, out var token))
        {
            var response = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(email, SeedData.DefaultDemoPassword));
            response.EnsureSuccessStatusCode();
            token = (await response.Content.ReadFromJsonAsync<LoginResponse>(Json))!.Token;
            _tokens[email] = token;
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
