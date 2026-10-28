using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Dtos;

namespace ServiceDesk.Tests.Api;

/// <summary>Ticket endpoints, exercised as a support agent.</summary>
public class TicketsApiTests(ServiceDeskApiFactory factory) : IClassFixture<ServiceDeskApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = ServiceDeskApiFactory.Json;
    private HttpClient _client = null!;

    public async Task InitializeAsync() => _client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.AgentEmail);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task List_returns_seeded_tickets()
    {
        var tickets = await _client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets", Json);
        Assert.NotNull(tickets);
        Assert.NotEmpty(tickets);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var tickets = await _client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets?status=Closed", Json);
        Assert.NotNull(tickets);
        Assert.NotEmpty(tickets);
        Assert.All(tickets, t => Assert.Equal(TicketStatus.Closed, t.Status));
    }

    [Fact]
    public async Task Create_then_get_ticket()
    {
        var clients = await _client.GetFromJsonAsync<List<JsonElement>>("/api/clients", Json);
        var clientId = clients![0].GetProperty("id").GetInt32();

        var response = await _client.PostAsJsonAsync("/api/tickets",
            new CreateTicketRequest("Printer offline in reception", "The reception printer shows offline for everyone.",
                TicketPriority.Medium, TicketCategory.Printing, clientId), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TicketDetailDto>(Json);
        Assert.Equal(TicketStatus.New, created!.Status);

        var fetched = await _client.GetFromJsonAsync<TicketDetailDto>($"/api/tickets/{created.Id}", Json);
        Assert.Equal("Printer offline in reception", fetched!.Title);
    }

    [Fact]
    public async Task Create_with_too_short_title_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/tickets",
            new CreateTicketRequest("Hi", "Description long enough", ClientId: 1), Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_status_transition_returns_409()
    {
        var newTicket = (await _client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets?status=New", Json))!.First();

        var response = await _client.PostAsJsonAsync($"/api/tickets/{newTicket.Id}/status",
            new ChangeStatusRequest(TicketStatus.Closed), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Valid_status_transition_is_recorded_in_history()
    {
        var newTicket = (await _client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets?status=New", Json))!.Last();

        var response = await _client.PostAsJsonAsync($"/api/tickets/{newTicket.Id}/status",
            new ChangeStatusRequest(TicketStatus.InProgress), Json);

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<TicketDetailDto>(Json);
        Assert.Equal(TicketStatus.InProgress, detail!.Status);
        Assert.Contains(detail.StatusChanges, s => s.FromStatus == TicketStatus.New && s.ToStatus == TicketStatus.InProgress);
    }

    [Fact]
    public async Task New_ticket_gets_sla_deadlines_from_its_priority()
    {
        var response = await _client.PostAsJsonAsync("/api/tickets",
            new CreateTicketRequest("Server room alarm", "Temperature alarm in the server room.",
                TicketPriority.Critical, TicketCategory.Hardware, ClientId: 1), Json);
        var ticket = await response.Content.ReadFromJsonAsync<TicketDetailDto>(Json);

        Assert.Equal(ticket!.CreatedAt.AddHours(1), ticket.Sla.ResponseDueAt);
        Assert.Equal(ticket.CreatedAt.AddHours(4), ticket.Sla.ResolutionDueAt);
        Assert.Equal(ServiceDesk.Api.Services.SlaState.OnTrack, ticket.Sla.Overall);
    }

    [Fact]
    public async Task List_filters_by_sla_state()
    {
        var breached = await _client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets?sla=Breached", Json);
        Assert.NotEmpty(breached!);
        Assert.All(breached!, t => Assert.Equal(ServiceDesk.Api.Services.SlaState.Breached, t.Sla.Overall));
    }

    [Fact]
    public async Task Open_filter_excludes_resolved_and_closed_tickets()
    {
        var all = await _client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets?sla=Breached", Json);
        var open = await _client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets?sla=Breached&open=true", Json);

        Assert.NotEmpty(open!);
        Assert.True(open!.Count < all!.Count); // seed data has tickets that were resolved late
        Assert.All(open, t => Assert.DoesNotContain(t.Status, new[] { TicketStatus.Resolved, TicketStatus.Closed }));
    }

    [Fact]
    public async Task Unknown_ticket_returns_404()
    {
        var response = await _client.GetAsync("/api/tickets/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
