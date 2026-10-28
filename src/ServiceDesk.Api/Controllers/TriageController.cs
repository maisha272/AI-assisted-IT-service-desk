using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services;
using ServiceDesk.Api.Services.Auth;
using ServiceDesk.Api.Services.Triage;

namespace ServiceDesk.Api.Controllers;

public record TriageSuggestionDto(
    int Id,
    int TicketId,
    TriageStatus Status,
    TicketCategory SuggestedCategory,
    TicketPriority SuggestedPriority,
    string Summary,
    IReadOnlyList<string> Steps,
    string Rationale,
    string Confidence,
    IReadOnlyList<RelatedArticleRef> RelatedArticles,
    string Provider,
    DateTime CreatedAt,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    TicketCategory? FinalCategory,
    TicketPriority? FinalPriority,
    string? FinalSummary,
    IReadOnlyList<string>? FinalSteps,
    bool WasEdited,
    string? RejectionReason);

public record ApproveTriageRequest(
    [Required] TicketCategory Category,
    [Required] TicketPriority Priority,
    [Required, StringLength(300, MinimumLength = 1)] string Summary,
    [MaxLength(TriageService.MaxSteps)] List<string> Steps);

public record RejectTriageRequest([StringLength(500)] string? Reason);

public record TriageStatsDto(string Mode, int Pending, int Approved, int ApprovedWithEdits, int Rejected);

/// <summary>AI triage endpoints. Agents only: client staff never see AI suggestions.</summary>
[ApiController]
[Route("api")]
[Authorize(Roles = Roles.Agent)]
public class TriageController(
    AppDbContext db,
    TriageService triage,
    ICurrentUser currentUser,
    IOptions<AiOptions> aiOptions) : ControllerBase
{
    /// <summary>The most recent suggestion for a ticket (pending or decided), or 204 if none yet.</summary>
    [HttpGet("tickets/{ticketId:int}/triage")]
    public async Task<ActionResult<TriageSuggestionDto>> GetForTicket(int ticketId, CancellationToken ct)
    {
        var suggestion = await db.TriageSuggestions.Include(s => s.ReviewedBy)
            .Where(s => s.TicketId == ticketId)
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);
        return suggestion is null ? NoContent() : ToDto(suggestion);
    }

    /// <summary>Runs triage now (synchronously), e.g. after the ticket was edited or a previous run failed.</summary>
    [HttpPost("tickets/{ticketId:int}/triage")]
    public async Task<ActionResult<TriageSuggestionDto>> Run(int ticketId, CancellationToken ct)
    {
        try
        {
            var suggestion = await triage.RunAsync(ticketId, ct);
            return suggestion is null
                ? Problem("Ticket not found or already resolved.", statusCode: StatusCodes.Status404NotFound)
                : ToDto(suggestion);
        }
        catch (HttpRequestException ex)
        {
            return Problem($"The AI provider is unavailable: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpPost("triage/{id:int}/approve")]
    public async Task<ActionResult<TriageSuggestionDto>> Approve(int id, ApproveTriageRequest request, CancellationToken ct)
    {
        var agent = await currentUser.GetAsync(ct);
        return await Decide(() => triage.ApproveAsync(id, agent,
            new TriageDecision(request.Category, request.Priority, request.Summary, request.Steps ?? []), ct));
    }

    [HttpPost("triage/{id:int}/reject")]
    public async Task<ActionResult<TriageSuggestionDto>> Reject(int id, RejectTriageRequest request, CancellationToken ct)
    {
        var agent = await currentUser.GetAsync(ct);
        return await Decide(() => triage.RejectAsync(id, agent, request.Reason, ct));
    }

    /// <summary>How the AI is doing: how many suggestions were accepted as-is, edited, or rejected.</summary>
    [HttpGet("triage/stats")]
    public async Task<TriageStatsDto> Stats(CancellationToken ct)
    {
        var counts = await db.TriageSuggestions
            .GroupBy(s => new { s.Status, s.WasEdited })
            .Select(g => new { g.Key.Status, g.Key.WasEdited, Count = g.Count() })
            .ToListAsync(ct);
        return new TriageStatsDto(
            TriageSetup.DescribeMode(aiOptions.Value),
            counts.Where(c => c.Status == TriageStatus.Pending).Sum(c => c.Count),
            counts.Where(c => c.Status == TriageStatus.Approved).Sum(c => c.Count),
            counts.Where(c => c.Status == TriageStatus.Approved && c.WasEdited).Sum(c => c.Count),
            counts.Where(c => c.Status == TriageStatus.Rejected).Sum(c => c.Count));
    }

    private async Task<ActionResult<TriageSuggestionDto>> Decide(Func<Task<TriageSuggestion>> action)
    {
        try
        {
            var suggestion = await action();
            await db.Entry(suggestion).Reference(s => s.ReviewedBy).LoadAsync();
            return ToDto(suggestion);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (TriageException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static TriageSuggestionDto ToDto(TriageSuggestion s) => new(
        s.Id, s.TicketId, s.Status, s.SuggestedCategory, s.SuggestedPriority, s.Summary,
        SplitLines(s.Steps)!, s.Rationale, s.Confidence,
        JsonSerializer.Deserialize<List<RelatedArticleRef>>(s.RelatedArticlesJson, JsonSerializerOptions.Web) ?? [],
        s.Provider, s.CreatedAt, s.ReviewedBy?.DisplayName, s.ReviewedAt,
        s.FinalCategory, s.FinalPriority, s.FinalSummary, SplitLines(s.FinalSteps), s.WasEdited, s.RejectionReason);

    private static List<string>? SplitLines(string? text) =>
        text is null ? null : text.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
}
