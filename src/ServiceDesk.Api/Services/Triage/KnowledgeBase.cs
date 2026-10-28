using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Api.Data;

namespace ServiceDesk.Api.Services.Triage;

/// <summary>
/// The "retrieval" half of RAG: embeds knowledge articles and finds the ones most similar to a ticket.
/// </summary>
public class KnowledgeBase(AppDbContext db, IEmbeddingClient embeddings, IOptions<AiOptions> options, ILogger<KnowledgeBase> logger)
{
    /// <summary>
    /// Embeds any article that has no vector yet, or whose vector came from a different model
    /// (e.g. after switching from mock to OpenAI). Done lazily before each search; cheap when up to date.
    /// </summary>
    public async Task EnsureIndexedAsync(CancellationToken ct = default)
    {
        var stale = await db.KnowledgeArticles
            .Where(a => a.EmbeddingJson == null || a.EmbeddingModel != embeddings.Model)
            .ToListAsync(ct);
        if (stale.Count == 0) return;

        logger.LogInformation("Embedding {Count} knowledge articles with {Model}", stale.Count, embeddings.Model);
        foreach (var batch in stale.Chunk(64))
        {
            var vectors = await embeddings.EmbedAsync(batch.Select(a => a.TextForEmbedding()).ToList(), ct);
            for (var i = 0; i < batch.Length; i++)
            {
                batch[i].EmbeddingJson = JsonSerializer.Serialize(vectors[i]);
                batch[i].EmbeddingModel = embeddings.Model;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Returns the top-K most similar articles above the relevance threshold, best first.</summary>
    public async Task<IReadOnlyList<RetrievedArticle>> SearchAsync(string query, CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct);

        var queryVector = (await embeddings.EmbedAsync([query], ct))[0];

        // Brute-force similarity over every article. Perfectly fine for hundreds or a few thousand articles;
        // beyond that you'd use a vector index (Azure AI Search, pgvector, SQL Server's vector type).
        var articles = await db.KnowledgeArticles.AsNoTracking()
            .Where(a => a.EmbeddingModel == embeddings.Model)
            .ToListAsync(ct);

        return articles
            .Select(a => new RetrievedArticle(a.Id, a.Title, a.Category, a.Problem, a.Resolution,
                VectorMath.Cosine(queryVector, JsonSerializer.Deserialize<float[]>(a.EmbeddingJson!)!)))
            .Where(r => r.Score >= options.Value.MinRelevance)
            .OrderByDescending(r => r.Score)
            .Take(options.Value.TopK)
            .ToList();
    }
}
