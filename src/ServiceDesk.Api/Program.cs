using System.Text.Json.Serialization;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Services;
using ServiceDesk.Api.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    // Send enums as strings ("InProgress") rather than numbers, so the API is self-describing.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

// TimeProvider is .NET's injectable clock. Tests swap in a fake clock to check time-based rules.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddServiceDeskAuth(builder.Configuration, builder.Environment);
builder.Services.AddScoped<DashboardService>();

var app = builder.Build();

await app.InitialiseDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();

// Exposes the auto-generated Program class to the test project (WebApplicationFactory<Program>).
public partial class Program;
