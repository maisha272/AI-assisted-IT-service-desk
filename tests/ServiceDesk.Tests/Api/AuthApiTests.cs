using System.Net;
using System.Net.Http.Json;
using ServiceDesk.Api.Controllers;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Dtos;

namespace ServiceDesk.Tests.Api;

/// <summary>Authentication and role-based access rules.</summary>
public class AuthApiTests(ServiceDeskApiFactory factory) : IClassFixture<ServiceDeskApiFactory>
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = ServiceDeskApiFactory.Json;

    [Fact]
    public async Task Login_with_valid_credentials_returns_token_and_user()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ServiceDeskApiFactory.AgentEmail, "Demo123!"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        Assert.False(string.IsNullOrEmpty(body!.Token));
        Assert.Equal(UserRole.Agent, body.User.Role);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ServiceDeskApiFactory.AgentEmail, "wrong-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/tickets")]
    [InlineData("/api/tickets/1")]
    [InlineData("/api/auth/me")]
    public async Task Endpoints_require_authentication(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Client_only_sees_their_own_tickets()
    {
        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);
        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me", Json);

        var tickets = await client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets", Json);

        Assert.NotEmpty(tickets!);
        Assert.All(tickets!, t => Assert.Equal(me!.DisplayName, t.CreatedByName));
    }

    [Fact]
    public async Task Client_gets_404_for_another_clients_ticket()
    {
        var other = await factory.CreateClientAsAsync(ServiceDeskApiFactory.OtherClientEmail);
        var othersTicket = (await other.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets", Json))!.First();

        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);
        var response = await client.GetAsync($"/api/tickets/{othersTicket.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Client_cannot_see_internal_notes()
    {
        var agent = await factory.CreateClientAsAsync(ServiceDeskApiFactory.AgentEmail);
        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);
        var ticket = (await client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets?status=InProgress", Json))!.First();

        await agent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new AddCommentRequest("Secret agent note", true), Json);
        await agent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new AddCommentRequest("Public reply", false), Json);

        var asClient = await client.GetFromJsonAsync<TicketDetailDto>($"/api/tickets/{ticket.Id}", Json);
        var asAgent = await agent.GetFromJsonAsync<TicketDetailDto>($"/api/tickets/{ticket.Id}", Json);

        Assert.DoesNotContain(asClient!.Comments, c => c.Body == "Secret agent note");
        Assert.Contains(asClient.Comments, c => c.Body == "Public reply");
        Assert.Contains(asAgent!.Comments, c => c.Body == "Secret agent note");
    }

    [Fact]
    public async Task Client_cannot_post_internal_notes()
    {
        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);
        var ticket = (await client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets?status=InProgress", Json))!.First();

        var response = await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments",
            new AddCommentRequest("Trying to be sneaky", true), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Client_cannot_use_agent_only_endpoints()
    {
        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);
        var ticket = (await client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets", Json))!.First();

        var patch = await client.PatchAsJsonAsync($"/api/tickets/{ticket.Id}",
            new UpdateTicketRequest(TicketPriority.Critical, null), Json);
        var agents = await client.GetAsync("/api/agents");

        Assert.Equal(HttpStatusCode.Forbidden, patch.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, agents.StatusCode);
    }

    [Fact]
    public async Task Client_ticket_is_created_for_their_own_company()
    {
        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);
        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me", Json);

        // Even if a client tries to pass another company's id, it is ignored.
        var response = await client.PostAsJsonAsync("/api/tickets",
            new CreateTicketRequest("Phone not ringing", "Desk phone doesn't ring for incoming calls.", ClientId: 999), Json);

        response.EnsureSuccessStatusCode();
        var ticket = await response.Content.ReadFromJsonAsync<TicketDetailDto>(Json);
        Assert.Equal(me!.ClientId, ticket!.ClientId);
    }

    [Fact]
    public async Task Client_reply_moves_waiting_ticket_back_to_in_progress()
    {
        var agent = await factory.CreateClientAsAsync(ServiceDeskApiFactory.AgentEmail);
        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);
        var created = await (await client.PostAsJsonAsync("/api/tickets",
                new CreateTicketRequest("Headset not working", "My headset microphone is not detected.")))
            .Content.ReadFromJsonAsync<TicketDetailDto>(Json);

        await agent.PostAsJsonAsync($"/api/tickets/{created!.Id}/status", new ChangeStatusRequest(TicketStatus.InProgress), Json);
        await agent.PostAsJsonAsync($"/api/tickets/{created.Id}/status", new ChangeStatusRequest(TicketStatus.Waiting), Json);

        var reply = await client.PostAsJsonAsync($"/api/tickets/{created.Id}/comments",
            new AddCommentRequest("It's a Jabra headset, plugged in via USB."), Json);

        var updated = await reply.Content.ReadFromJsonAsync<TicketDetailDto>(Json);
        Assert.Equal(TicketStatus.InProgress, updated!.Status);
    }
}
