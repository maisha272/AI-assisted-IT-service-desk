using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services.Triage;

/// <summary>A knowledge article retrieved for a ticket, with its similarity score.</summary>
public record RetrievedArticle(int Id, string Title, TicketCategory Category, string Problem, string Resolution, double Score);

/// <summary>Everything the model is given: the ticket and the retrieved past fixes.</summary>
public record TriageInput(string Title, string Description, string ClientName, IReadOnlyList<RetrievedArticle> Articles);

/// <summary>
/// The model's answer exactly as returned — strings, not enums. It is untrusted until
/// <see cref="TriageService"/> validates it (an LLM can return a category that doesn't exist,
/// cite an article it wasn't given, or produce 40 steps).
/// </summary>
public record RawTriage(
    string Category,
    string Priority,
    string Summary,
    IReadOnlyList<string> Steps,
    IReadOnlyList<int> RelatedArticleIds,
    string Confidence,
    string Rationale);

/// <summary>Produces a triage suggestion for a ticket. Implementations: OpenAI/Azure OpenAI, or mock rules.</summary>
public interface ITriageModel
{
    /// <summary>Recorded on each suggestion, e.g. "openai:gpt-4o-mini" or "mock".</summary>
    string Name { get; }

    Task<RawTriage> SuggestAsync(TriageInput input, CancellationToken ct = default);
}
