using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services;
using ServiceDesk.Api.Services.Triage;

namespace ServiceDesk.Tests.Triage;

/// <summary>A model that returns whatever the test tells it to — including bad output.</summary>
internal class FakeTriageModel(RawTriage result) : ITriageModel
{
    public string Name => "fake";
    public TriageInput? LastInput { get; private set; }

    public Task<RawTriage> SuggestAsync(TriageInput input, CancellationToken ct = default)
    {
        LastInput = input;
        return Task.FromResult(result);
    }
}

public class TriageServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    private readonly TestDb _t = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Now));

    private static readonly RawTriage GoodAnswer = new(
        "Printing", "High", "Printer offline for the whole floor.",
        ["Check the printer's IP address.", "Update the print queue port."], [], "medium", "Printing keywords.");

    private Ticket AddTicket(TicketStatus status = TicketStatus.New)
    {
        var ticket = new Ticket
        {
            Title = "Printer offline", Description = "The level 2 printer shows offline for everyone.",
            Priority = TicketPriority.Low, Category = TicketCategory.Other, Status = status,
            Client = _t.Client, CreatedBy = _t.ClientUser, CreatedAt = Now.AddHours(-1), UpdatedAt = Now.AddHours(-1),
        };
        SlaPolicy.ApplyDeadlines(ticket);
        _t.Db.Tickets.Add(ticket);
        _t.Db.KnowledgeArticles.Add(new KnowledgeArticle
        {
            Title = "Printer showing offline", Category = TicketCategory.Printing,
            Problem = "Printer shows offline for everyone.", Resolution = "Set a DHCP reservation.", CreatedAt = Now,
        });
        _t.Db.SaveChanges();
        return ticket;
    }

    private TriageService Service(ITriageModel model)
    {
        var options = Options.Create(new AiOptions());
        var kb = new KnowledgeBase(_t.Db, new HashingEmbeddingClient(), options, NullLogger<KnowledgeBase>.Instance);
        return new TriageService(_t.Db, kb, model, _clock, NullLogger<TriageService>.Instance);
    }

    // ---------- Validation of model output ----------

    [Fact]
    public void Invalid_enum_values_fall_back_to_the_tickets_current_values()
    {
        var ticket = new Ticket { Category = TicketCategory.Email, Priority = TicketPriority.Medium };
        var raw = GoodAnswer with { Category = "Plumbing", Priority = "SuperUrgent" };

        var s = TriageService.Validate(raw, [], ticket);

        Assert.Equal(TicketCategory.Email, s.SuggestedCategory);
        Assert.Equal(TicketPriority.Medium, s.SuggestedPriority);
    }

    [Fact]
    public void Numeric_enum_strings_are_not_accepted()
    {
        // Enum.TryParse would happily accept "99"; IsDefined guards against it.
        var s = TriageService.Validate(GoodAnswer with { Priority = "99" }, [], new Ticket { Priority = TicketPriority.Low });
        Assert.Equal(TicketPriority.Low, s.SuggestedPriority);
    }

    [Fact]
    public void Citations_of_articles_that_were_not_retrieved_are_dropped()
    {
        var retrieved = new List<RetrievedArticle> { new(5, "Real article", TicketCategory.Printing, "p", "r", 0.7) };

        var s = TriageService.Validate(GoodAnswer with { RelatedArticleIds = [5, 404] }, retrieved, new Ticket());

        Assert.Contains("\"id\":5", s.RelatedArticlesJson.Replace(" ", "").ToLowerInvariant());
        Assert.DoesNotContain("404", s.RelatedArticlesJson);
    }

    [Fact]
    public void Steps_are_capped_trimmed_and_blank_ones_removed()
    {
        var manySteps = Enumerable.Range(1, 20).Select(i => $"  Step {i}  ").Append("   ").ToList();

        var s = TriageService.Validate(GoodAnswer with { Steps = manySteps }, [], new Ticket());

        var lines = s.Steps.Split('\n');
        Assert.Equal(TriageService.MaxSteps, lines.Length);
        Assert.Equal("Step 1", lines[0]);
    }

    [Fact]
    public void Overlong_summary_is_truncated_and_unknown_confidence_becomes_low()
    {
        var s = TriageService.Validate(GoodAnswer with { Summary = new string('x', 1000), Confidence = "certain" }, [], new Ticket());

        Assert.Equal(TriageService.MaxSummaryLength, s.Summary.Length);
        Assert.Equal("low", s.Confidence);
    }

    // ---------- Running triage ----------

    [Fact]
    public async Task Run_creates_a_pending_suggestion_without_changing_the_ticket()
    {
        var ticket = AddTicket();
        var model = new FakeTriageModel(GoodAnswer);

        var suggestion = await Service(model).RunAsync(ticket.Id);

        Assert.NotNull(suggestion);
        Assert.Equal(TriageStatus.Pending, suggestion.Status);
        Assert.Equal(TicketCategory.Printing, suggestion.SuggestedCategory);
        Assert.Equal("fake", suggestion.Provider);
        // Human in the loop: nothing is applied until approval.
        Assert.Equal(TicketCategory.Other, ticket.Category);
        Assert.Equal(TicketPriority.Low, ticket.Priority);
        // RAG: the model was given the matching past fix.
        Assert.Contains(model.LastInput!.Articles, a => a.Title == "Printer showing offline");
    }

    [Fact]
    public async Task Rerunning_replaces_the_previous_pending_suggestion()
    {
        var ticket = AddTicket();
        var service = Service(new FakeTriageModel(GoodAnswer));

        await service.RunAsync(ticket.Id);
        await service.RunAsync(ticket.Id);

        Assert.Equal(1, await _t.Db.TriageSuggestions.CountAsync(s => s.TicketId == ticket.Id));
    }

    [Fact]
    public async Task Resolved_tickets_are_not_triaged()
    {
        var ticket = AddTicket(TicketStatus.Resolved);
        Assert.Null(await Service(new FakeTriageModel(GoodAnswer)).RunAsync(ticket.Id));
    }

    // ---------- Human approval ----------

    [Fact]
    public async Task Approving_unchanged_applies_suggestion_and_recalculates_sla()
    {
        var ticket = AddTicket();
        var service = Service(new FakeTriageModel(GoodAnswer));
        var s = (await service.RunAsync(ticket.Id))!;

        var approved = await service.ApproveAsync(s.Id, _t.Agent,
            new TriageDecision(s.SuggestedCategory, s.SuggestedPriority, s.Summary, s.Steps.Split('\n')));

        Assert.Equal(TriageStatus.Approved, approved.Status);
        Assert.False(approved.WasEdited);
        Assert.Equal(_t.Agent.Id, approved.ReviewedById);
        Assert.Equal(TicketCategory.Printing, ticket.Category);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(ticket.CreatedAt.AddHours(24), ticket.ResolutionDueAt); // High = 24h
        Assert.Equal("Printer offline for the whole floor.", ticket.AiSummary);
        Assert.Contains(ticket.Comments, c => c.IsInternal && c.Body.Contains("1. Check the printer's IP address."));
    }

    [Fact]
    public async Task Approving_with_edits_records_the_agents_values()
    {
        var ticket = AddTicket();
        var service = Service(new FakeTriageModel(GoodAnswer));
        var s = (await service.RunAsync(ticket.Id))!;

        var approved = await service.ApproveAsync(s.Id, _t.Agent,
            new TriageDecision(TicketCategory.Network, TicketPriority.Medium, "Agent's own summary", ["Ping the printer."]));

        Assert.True(approved.WasEdited);
        Assert.Equal(TicketCategory.Printing, approved.SuggestedCategory); // the original is kept for comparison
        Assert.Equal(TicketCategory.Network, approved.FinalCategory);
        Assert.Equal(TicketCategory.Network, ticket.Category);
        Assert.Equal(TicketPriority.Medium, ticket.Priority);
    }

    [Fact]
    public async Task A_suggestion_can_only_be_decided_once()
    {
        var ticket = AddTicket();
        var service = Service(new FakeTriageModel(GoodAnswer));
        var s = (await service.RunAsync(ticket.Id))!;
        await service.RejectAsync(s.Id, _t.Agent, "Not relevant");

        await Assert.ThrowsAsync<TriageException>(() => service.ApproveAsync(s.Id, _t.Agent,
            new TriageDecision(TicketCategory.Printing, TicketPriority.High, "x", [])));
    }

    [Fact]
    public async Task Rejecting_leaves_the_ticket_unchanged()
    {
        var ticket = AddTicket();
        var service = Service(new FakeTriageModel(GoodAnswer));
        var s = (await service.RunAsync(ticket.Id))!;

        var rejected = await service.RejectAsync(s.Id, _t.Agent, "Wrong category");

        Assert.Equal(TriageStatus.Rejected, rejected.Status);
        Assert.Equal("Wrong category", rejected.RejectionReason);
        Assert.Equal(TicketCategory.Other, ticket.Category);
        Assert.Equal(TicketPriority.Low, ticket.Priority);
        Assert.Null(ticket.AiSummary);
    }

    public void Dispose() => _t.Dispose();
}
