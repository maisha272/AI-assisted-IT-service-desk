using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services.Auth;
using ServiceDesk.Api.Services.Triage;

namespace ServiceDesk.Api.Controllers;

public record KnowledgeArticleDto(int Id, string Title, TicketCategory Category, string Problem, string Resolution, int? SourceTicketId, DateTime CreatedAt);

public record CreateKnowledgeArticleRequest(
    [Required, StringLength(200, MinimumLength = 5)] string Title,
    [Required] TicketCategory Category,
    [Required, StringLength(4000, MinimumLength = 10)] string Problem,
    [Required, StringLength(4000, MinimumLength = 10)] string Resolution,
    int? SourceTicketId);

public record KnowledgeSearchResultDto(int Id, string Title, TicketCategory Category, double Score);

/// <summary>The knowledge base of past fixes that AI triage retrieves from. Agents only.</summary>
[ApiController]
[Route("api/knowledge")]
[Authorize(Roles = Roles.Agent)]
public class KnowledgeController(AppDbContext db, KnowledgeBase knowledgeBase, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<KnowledgeArticleDto>> List(CancellationToken ct) =>
        await db.KnowledgeArticles
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new KnowledgeArticleDto(a.Id, a.Title, a.Category, a.Problem, a.Resolution, a.SourceTicketId, a.CreatedAt))
            .ToListAsync(ct);

    /// <summary>Semantic search — the same retrieval step the triage agent uses. Handy for checking relevance.</summary>
    [HttpGet("search")]
    public async Task<IEnumerable<KnowledgeSearchResultDto>> Search([FromQuery, Required] string q, CancellationToken ct) =>
        (await knowledgeBase.SearchAsync(q, ct))
            .Select(r => new KnowledgeSearchResultDto(r.Id, r.Title, r.Category, Math.Round(r.Score, 3)));

    /// <summary>Adds a past fix (typically from a resolved ticket), so future tickets can benefit from it.</summary>
    [HttpPost]
    public async Task<ActionResult<KnowledgeArticleDto>> Create(CreateKnowledgeArticleRequest request, CancellationToken ct)
    {
        var article = new KnowledgeArticle
        {
            Title = request.Title.Trim(),
            Category = request.Category,
            Problem = request.Problem.Trim(),
            Resolution = request.Resolution.Trim(),
            SourceTicketId = request.SourceTicketId,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.KnowledgeArticles.Add(article);
        await db.SaveChangesAsync(ct);
        await knowledgeBase.EnsureIndexedAsync(ct); // embed it straight away

        return Created($"/api/knowledge/{article.Id}", new KnowledgeArticleDto(article.Id, article.Title, article.Category,
            article.Problem, article.Resolution, article.SourceTicketId, article.CreatedAt));
    }
}
