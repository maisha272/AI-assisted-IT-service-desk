using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services.Triage;

namespace ServiceDesk.Tests.Triage;

public class HashingEmbeddingTests
{
    [Fact]
    public void Similar_texts_score_higher_than_unrelated_texts()
    {
        var printer = HashingEmbeddingClient.Embed("Printer on level 2 showing offline, nobody can print");
        var similar = HashingEmbeddingClient.Embed("The printer is offline for everyone");
        var unrelated = HashingEmbeddingClient.Embed("Locked out of my account after too many password attempts");

        Assert.True(VectorMath.Cosine(printer, similar) > VectorMath.Cosine(printer, unrelated));
    }

    [Fact]
    public void Embeddings_are_deterministic_and_unit_length()
    {
        var a = HashingEmbeddingClient.Embed("VPN keeps disconnecting");
        var b = HashingEmbeddingClient.Embed("VPN keeps disconnecting");

        Assert.Equal(a, b);
        Assert.Equal(1.0, Math.Sqrt(a.Sum(v => (double)v * v)), precision: 5);
        Assert.Equal(1.0, VectorMath.Cosine(a, b), precision: 5);
    }

    [Fact]
    public void Cosine_of_empty_vector_is_zero_not_NaN()
    {
        Assert.Equal(0, VectorMath.Cosine(new float[3], [1, 0, 0]));
    }
}

public class MockTriageModelTests
{
    private static readonly MockTriageModel Model = new();

    private static RetrievedArticle Article(int id, TicketCategory category, double score) =>
        new(id, "Past fix", category, "Problem text", "Did the first thing. Then did the second thing.", score);

    [Fact]
    public async Task Ransomware_is_a_critical_security_incident()
    {
        var result = await Model.SuggestAsync(new TriageInput(
            "Files renamed", "All files on the shared drive have a strange extension and there is a note asking for payment.", "Acme", []));

        Assert.Equal("Security", result.Category);
        Assert.Equal("Critical", result.Priority);
    }

    [Fact]
    public async Task Software_install_request_is_low_priority()
    {
        var result = await Model.SuggestAsync(new TriageInput(
            "Install request", "Please install Adobe Acrobat on my laptop, my manager approved it.", "Acme", []));

        Assert.Equal("Low", result.Priority);
    }

    [Fact]
    public async Task Uses_the_best_retrieved_article_for_steps_and_citation()
    {
        var result = await Model.SuggestAsync(new TriageInput(
            "Printer offline", "Printer shows offline.", "Acme",
            [Article(7, TicketCategory.Printing, 0.6), Article(9, TicketCategory.Printing, 0.3)]));

        Assert.Equal([7], result.RelatedArticleIds);
        Assert.Contains("Did the first thing.", result.Steps);
        Assert.Equal("high", result.Confidence);
    }

    [Fact]
    public async Task Without_articles_gives_generic_steps_and_low_confidence()
    {
        var result = await Model.SuggestAsync(new TriageInput("Weird issue", "Something odd is happening.", "Acme", []));

        Assert.Equal("Other", result.Category);
        Assert.Empty(result.RelatedArticleIds);
        Assert.NotEmpty(result.Steps);
        Assert.Equal("low", result.Confidence);
    }
}
