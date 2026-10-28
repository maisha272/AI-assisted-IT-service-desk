using System.Text.Json.Serialization;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Services;
using ServiceDesk.Api.Services.Auth;
using ServiceDesk.Api.Services.Integrations;
using ServiceDesk.Api.Services.Triage;

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
builder.Services.AddAiTriage(builder.Configuration);
builder.Services.AddIntegrations(builder.Configuration);

var app = builder.Build();

await app.InitialiseDatabaseAsync();

var aiOptions = app.Configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
app.Logger.LogInformation("AI triage mode: {Mode}", TriageSetup.DescribeMode(aiOptions));

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
