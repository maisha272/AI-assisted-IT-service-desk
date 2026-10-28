using System.Text.RegularExpressions;

namespace ServiceDesk.Api.Services.Triage;

/// <summary>Turns text into a vector so that similar texts get similar vectors.</summary>
public interface IEmbeddingClient
{
    /// <summary>Identifies the vector space; vectors from different models can't be compared.</summary>
    string Model { get; }

    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}

public static class VectorMath
{
    /// <summary>Cosine similarity: 1 = same direction (very similar), 0 = unrelated.</summary>
    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Vectors must have the same length.");
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return normA == 0 || normB == 0 ? 0 : dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}

/// <summary>
/// Offline "mock" embeddings using the hashing trick: each word (and word pair) is hashed into one of
/// 512 buckets. Texts sharing vocabulary end up pointing in similar directions. It has no idea about
/// meaning ("laptop" and "notebook" are unrelated to it), but it is free, deterministic and good enough
/// to demo retrieval without an API key.
/// </summary>
public partial class HashingEmbeddingClient : IEmbeddingClient
{
    private const int Dimensions = 512;

    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "the", "and", "or", "but", "is", "are", "was", "were", "be", "been", "it", "its", "this",
        "that", "to", "of", "in", "on", "for", "with", "at", "by", "from", "as", "my", "our", "we", "i", "me",
        "you", "your", "they", "them", "can", "cant", "can't", "not", "no", "please", "have", "has", "had",
        "do", "does", "did", "since", "when", "just", "so", "some", "any", "all", "now", "then", "there",
        "here", "after", "before", "into", "out", "up", "down", "again", "very", "also", "get", "got", "will",
    ];

    public string Model => "hashing-512";

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<float[]>>(texts.Select(Embed).ToList());

    public static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        var tokens = Tokenize(text);

        for (var i = 0; i < tokens.Count; i++)
        {
            vector[Bucket(tokens[i])] += 1f;
            if (i > 0) vector[Bucket(tokens[i - 1] + " " + tokens[i])] += 0.5f; // word pairs add a bit of context
        }

        // Dampen repeated words, then scale to length 1 so long and short texts compare fairly.
        for (var i = 0; i < vector.Length; i++) vector[i] = MathF.Log(1 + vector[i]);
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm > 0) for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
        return vector;
    }

    internal static List<string> Tokenize(string text) =>
        WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => Stem(m.Value))
            .Where(w => w.Length > 1 && !StopWords.Contains(w))
            .ToList();

    // Very light stemming so "printers"/"printer" and "emails"/"email" match.
    private static string Stem(string word)
    {
        if (word.Length > 5 && word.EndsWith("ing")) return word[..^3];
        if (word.Length > 4 && word.EndsWith("ed")) return word[..^2];
        if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss")) return word[..^1];
        return word;
    }

    // FNV-1a: a simple, stable hash (string.GetHashCode is randomised per process, so can't be used).
    private static int Bucket(string token)
    {
        var hash = 2166136261u;
        foreach (var c in token)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return (int)(hash % Dimensions);
    }

    [GeneratedRegex("[a-z0-9']+")]
    private static partial Regex WordRegex();
}
