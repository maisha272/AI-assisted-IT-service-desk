using System.Net;
using System.Net.Http.Json;
using ServiceDesk.Api.Controllers;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Dtos;

namespace ServiceDesk.Tests.Api;

/// <summary>The full AI triage flow through the API, using the offline mock model.</summary>
public class TriageApiTests(ServiceDeskApiFactory factory) : IClassFixture<ServiceDeskApiFactory>
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = ServiceDeskApiFactory.Json;

    private static async Task<TriageSuggestionDto> WaitForSuggestion(HttpClient agent, int ticketId)
    {
        // Triage runs on a background worker, so poll briefly for the result.
        for (var i = 0; i < 50; i++)
        {
            var response = await agent.GetAsync($"/api/tickets/{ticketId}/triage");
            if (response.StatusCode == HttpStatusCode.OK)
                return (await response.Content.ReadFromJsonAsync<TriageSuggestionDto>(Json))!;
            await Task.Delay(100);
        }
        throw new TimeoutException("No triage suggestion was produced.");
    }

    [Fact]
    public async Task New_ticket_is_triaged_in_the_background_and_approval_updates_the_ticket()
    {
        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);
        var agent = await factory.CreateClientAsAsync(ServiceDeskApiFactory.AgentEmail);

        var created = await (await client.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(
                "Printer offline", "The printer on level 2 is showing offline for everyone, we can't print invoices.")))
            .Content.ReadFromJsonAsync<TicketDetailDto>(Json);

        var suggestion = await WaitForSuggestion(agent, created!.Id);
        Assert.Equal(TriageStatus.Pending, suggestion.Status);
        Assert.Equal(TicketCategory.Printing, suggestion.SuggestedCategory);
        Assert.NotEmpty(suggestion.RelatedArticles);

        var approve = await agent.PostAsJsonAsync($"/api/triage/{suggestion.Id}/approve",
            new ApproveTriageRequest(suggestion.SuggestedCategory, TicketPriority.High, suggestion.Summary, [.. suggestion.Steps]), Json);
        approve.EnsureSuccessStatusCode();
        var decided = await approve.Content.ReadFromJsonAsync<TriageSuggestionDto>(Json);
        Assert.Equal(TriageStatus.Approved, decided!.Status);

        var ticket = await agent.GetFromJsonAsync<TicketDetailDto>($"/api/tickets/{created.Id}", Json);
        Assert.Equal(TicketCategory.Printing, ticket!.Category);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(suggestion.Summary, ticket.AiSummary);

        // Deciding twice is a conflict.
        var again = await agent.PostAsJsonAsync($"/api/triage/{suggestion.Id}/reject", new RejectTriageRequest("x"), Json);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Client_staff_cannot_see_or_approve_ai_suggestions()
    {
        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);
        var ticket = (await client.GetFromJsonAsync<List<TicketSummaryDto>>("/api/tickets", Json))!.First();

        var get = await client.GetAsync($"/api/tickets/{ticket.Id}/triage");
        var approve = await client.PostAsJsonAsync("/api/triage/1/approve",
            new ApproveTriageRequest(TicketCategory.Other, TicketPriority.Low, "x", []), Json);

        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
    }

    [Fact]
    public async Task Knowledge_search_finds_relevant_past_fixes()
    {
        var agent = await factory.CreateClientAsAsync(ServiceDeskApiFactory.AgentEmail);

        var results = await agent.GetFromJsonAsync<List<KnowledgeSearchResultDto>>(
            "/api/knowledge/search?q=outlook%20keeps%20asking%20for%20password", Json);

        Assert.Equal("Outlook keeps asking for my password", results![0].Title);
    }

    [Fact]
    public async Task New_knowledge_article_is_immediately_searchable()
    {
        var agent = await factory.CreateClientAsAsync(ServiceDeskApiFactory.AgentEmail);

        var create = await agent.PostAsJsonAsync("/api/knowledge", new CreateKnowledgeArticleRequest(
            "Label printer prints blank labels", TicketCategory.Printing,
            "The Zebra label printer feeds labels but prints nothing on them.",
            "Thermal ribbon was installed inside out. Reinstalled the ribbon and calibrated the media.", null), Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var results = await agent.GetFromJsonAsync<List<KnowledgeSearchResultDto>>(
            "/api/knowledge/search?q=zebra%20label%20printer%20blank%20labels", Json);
        Assert.Equal("Label printer prints blank labels", results![0].Title);
    }
}
