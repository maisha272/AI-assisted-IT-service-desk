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
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

// TimeProvider is .NET's injectable clock. Tests swap in a fake clock to check time-based rules.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddServiceDeskAuth(builder.Configuration, builder.Environment);
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();
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
else
{
    // Unhandled exceptions become a generic RFC 9457 problem response — no stack traces leak to clients.
    app.UseExceptionHandler();
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    // Basic security headers for the API and the single-page app.
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

// In production the built React app is copied into wwwroot and served by this same app.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// Unknown /api/* routes are real 404s; any other unknown path is a client-side route (e.g. /tickets/42),
// so it gets the React app's index.html and React Router takes over.
app.MapFallback("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

// Exposes the auto-generated Program class to the test project (WebApplicationFactory<Program>).
public partial class Program;
