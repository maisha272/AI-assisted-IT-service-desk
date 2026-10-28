namespace ServiceDesk.Api.Domain;

/// <summary>
/// A past fix: what the problem looked like and how it was resolved. The AI triage agent retrieves
/// the most similar articles for each new ticket (RAG) and uses them to suggest solution steps.
/// </summary>
public class KnowledgeArticle
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public TicketCategory Category { get; set; }
    public string Problem { get; set; } = "";
    public string Resolution { get; set; } = "";

    /// <summary>The resolved ticket this article was written from, if any.</summary>
    public int? SourceTicketId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Embedding vector of Title + Problem, stored as a JSON array of floats.</summary>
    public string? EmbeddingJson { get; set; }

    /// <summary>Which embedding model produced the vector. A different model means it must be re-embedded.</summary>
    public string? EmbeddingModel { get; set; }

    public string TextForEmbedding() => $"{Title}\n{Problem}";
}
