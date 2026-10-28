using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services.Triage;

public class TriageException(string message) : Exception(message);

/// <summary>The agent's (possibly edited) version of a suggestion, sent when approving.</summary>
public record TriageDecision(TicketCategory Category, TicketPriority Priority, string Summary, IReadOnlyList<string> Steps);

/// <summary>
/// Orchestrates AI triage: retrieve similar past fixes → ask the model → validate → save as a
/// <b>pending</b> suggestion. Nothing touches the ticket until an agent approves (human in the loop).
/// </summary>
public class TriageService(
    AppDbContext db,
    KnowledgeBase knowledgeBase,
    ITriageModel model,
    TimeProvider clock,
    ILogger<TriageService> logger)
{
    public const int MaxSteps = 8;
    public const int MaxStepLength = 300;
    public const int MaxSummaryLength = 300;

    /// <summary>
    /// Creates a new pending suggestion for the ticket, replacing any older pending one.
    /// Returns null if the ticket doesn't exist or is already resolved/closed.
    /// </summary>
    public async Task<TriageSuggestion?> RunAsync(int ticketId, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.Include(t => t.Client).FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || ticket.Status is TicketStatus.Resolved or TicketStatus.Closed) return null;

        var articles = await knowledgeBase.SearchAsync($"{ticket.Title}\n{ticket.Description}", ct);
        var raw = await model.SuggestAsync(new TriageInput(ticket.Title, ticket.Description, ticket.Client.Name, articles), ct);
        var suggestion = Validate(raw, articles, ticket);
        suggestion.Provider = model.Name;
        suggestion.CreatedAt = clock.GetUtcNow().UtcDateTime;

        // Only one suggestion should be waiting for review at a time.
        var oldPending = await db.TriageSuggestions
            .Where(s => s.TicketId == ticketId && s.Status == TriageStatus.Pending)
            .ToListAsync(ct);
        db.TriageSuggestions.RemoveRange(oldPending);

        db.TriageSuggestions.Add(suggestion);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Triage suggestion {SuggestionId} created for ticket {TicketId} by {Provider}",
            suggestion.Id, ticketId, model.Name);
        return suggestion;
    }

    /// <summary>
    /// Turns the model's raw output into a safe suggestion. LLM output is treated like user input:
    /// unknown enum values fall back to the ticket's current values, citations must be articles we
    /// actually retrieved, and text is length-limited.
    /// </summary>
    internal static TriageSuggestion Validate(RawTriage raw, IReadOnlyList<RetrievedArticle> retrieved, Ticket ticket)
    {
        var category = Enum.TryParse<TicketCategory>(raw.Category, ignoreCase: true, out var c) && Enum.IsDefined(c)
            ? c : ticket.Category;
        var priority = Enum.TryParse<TicketPriority>(raw.Priority, ignoreCase: true, out var p) && Enum.IsDefined(p)
            ? p : ticket.Priority;

        var steps = raw.Steps
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Take(MaxSteps)
            .Select(s => Truncate(s, MaxStepLength))
            .ToList();

        var cited = retrieved.Where(a => raw.RelatedArticleIds.Contains(a.Id)).ToList();

        var confidence = raw.Confidence?.ToLowerInvariant() is "low" or "medium" or "high" ? raw.Confidence.ToLowerInvariant() : "low";

        return new TriageSuggestion
        {
            TicketId = ticket.Id,
            SuggestedCategory = category,
            SuggestedPriority = priority,
            Summary = Truncate(string.IsNullOrWhiteSpace(raw.Summary) ? ticket.Title : raw.Summary.Trim(), MaxSummaryLength),
            Steps = string.Join('\n', steps),
            Rationale = Truncate(raw.Rationale?.Trim() ?? "", 500),
            Confidence = confidence,
            RelatedArticlesJson = JsonSerializer.Serialize(
                cited.Select(a => new RelatedArticleRef(a.Id, a.Title, Math.Round(a.Score, 3)))),
        };
    }

    /// <summary>
    /// Human approval: applies the agent's final values to the ticket, records whether they edited
    /// the AI's suggestion, and adds the steps as an internal note for whoever works the ticket.
    /// </summary>
    public async Task<TriageSuggestion> ApproveAsync(int suggestionId, User agent, TriageDecision decision, CancellationToken ct = default)
    {
        var suggestion = await LoadPendingAsync(suggestionId, ct);
        var ticket = suggestion.Ticket;
        var now = clock.GetUtcNow().UtcDateTime;

        var finalSteps = string.Join('\n', decision.Steps.Select(s => s.Trim()).Where(s => s.Length > 0));
        var finalSummary = decision.Summary.Trim();

        suggestion.Status = TriageStatus.Approved;
        suggestion.ReviewedById = agent.Id;
        suggestion.ReviewedAt = now;
        suggestion.FinalCategory = decision.Category;
        suggestion.FinalPriority = decision.Priority;
        suggestion.FinalSummary = finalSummary;
        suggestion.FinalSteps = finalSteps;
        suggestion.WasEdited = decision.Category != suggestion.SuggestedCategory
            || decision.Priority != suggestion.SuggestedPriority
            || finalSummary != suggestion.Summary
            || finalSteps != suggestion.Steps;

        ticket.Category = decision.Category;
        ticket.AiSummary = finalSummary;
        if (ticket.Priority != decision.Priority)
        {
            ticket.Priority = decision.Priority;
            SlaPolicy.ApplyDeadlines(ticket);
        }
        ticket.UpdatedAt = now;

        if (finalSteps.Length > 0)
        {
            var numbered = finalSteps.Split('\n').Select((s, i) => $"{i + 1}. {s}");
            ticket.Comments.Add(new Comment
            {
                AuthorId = agent.Id,
                Body = $"Triage (AI-assisted, approved by {agent.DisplayName}) - suggested steps:\n{string.Join('\n', numbered)}",
                IsInternal = true,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        return suggestion;
    }

    public async Task<TriageSuggestion> RejectAsync(int suggestionId, User agent, string? reason, CancellationToken ct = default)
    {
        var suggestion = await LoadPendingAsync(suggestionId, ct);
        suggestion.Status = TriageStatus.Rejected;
        suggestion.ReviewedById = agent.Id;
        suggestion.ReviewedAt = clock.GetUtcNow().UtcDateTime;
        suggestion.RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : Truncate(reason.Trim(), 500);
        await db.SaveChangesAsync(ct);
        return suggestion;
    }

    private async Task<TriageSuggestion> LoadPendingAsync(int suggestionId, CancellationToken ct)
    {
        var suggestion = await db.TriageSuggestions.Include(s => s.Ticket)
            .FirstOrDefaultAsync(s => s.Id == suggestionId, ct)
            ?? throw new KeyNotFoundException($"Triage suggestion {suggestionId} not found.");
        if (suggestion.Status != TriageStatus.Pending)
            throw new TriageException($"This suggestion has already been {suggestion.Status.ToString().ToLowerInvariant()}.");
        return suggestion;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}

public record RelatedArticleRef(int Id, string Title, double Score);
