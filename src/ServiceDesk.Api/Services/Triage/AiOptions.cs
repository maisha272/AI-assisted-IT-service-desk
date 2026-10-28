namespace ServiceDesk.Api.Services.Triage;

/// <summary>
/// Configuration section "AI". The API key is read from configuration/environment
/// (<c>AI__ApiKey</c>) and is never committed. With no key the app runs in mock mode.
/// </summary>
public class AiOptions
{
    public const string SectionName = "AI";

    /// <summary>"Mock" (default), "OpenAI" or "AzureOpenAI".</summary>
    public string Provider { get; set; } = "Mock";

    public string? ApiKey { get; set; }

    /// <summary>OpenAI model name, or the Azure OpenAI <i>deployment</i> name for chat.</summary>
    public string ChatModel { get; set; } = "gpt-4o-mini";

    /// <summary>OpenAI model name, or the Azure OpenAI deployment name for embeddings.</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>Azure OpenAI only, e.g. https://my-resource.openai.azure.com</summary>
    public string? Endpoint { get; set; }

    /// <summary>Azure OpenAI REST API version.</summary>
    public string ApiVersion { get; set; } = "2024-10-21";

    /// <summary>How many knowledge articles to retrieve per ticket.</summary>
    public int TopK { get; set; } = 3;

    /// <summary>Articles less similar than this (cosine, 0–1) are ignored as irrelevant.</summary>
    public double MinRelevance { get; set; } = 0.25;

    /// <summary>On startup, queue open tickets that have never been triaged (useful for demo data).</summary>
    public bool TriageOpenTicketsOnStartup { get; set; } = true;

    public bool IsAzure => Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when a real LLM provider is configured with a key; otherwise we fall back to mock.</summary>
    public bool UsesRealProvider =>
        !Provider.Equals("Mock", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(ApiKey);
}
