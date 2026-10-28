namespace ServiceDesk.Api.Services.Triage;

public static class TriageSetup
{
    /// <summary>
    /// Registers the AI triage pipeline. The provider is chosen once at startup: a real LLM if one is
    /// configured with an API key, otherwise the offline mock — so the app always runs.
    /// </summary>
    public static IServiceCollection AddAiTriage(this IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection(AiOptions.SectionName);
        services.Configure<AiOptions>(section);
        var options = section.Get<AiOptions>() ?? new AiOptions();

        if (options.UsesRealProvider)
        {
            if (options.IsAzure && string.IsNullOrWhiteSpace(options.Endpoint))
                throw new InvalidOperationException("AI:Endpoint is required when AI:Provider is AzureOpenAI.");

            services.AddHttpClient<OpenAiClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
            services.AddScoped<IEmbeddingClient, OpenAiEmbeddingClient>();
            services.AddScoped<ITriageModel, OpenAiTriageModel>();
        }
        else
        {
            services.AddSingleton<IEmbeddingClient, HashingEmbeddingClient>();
            services.AddSingleton<ITriageModel, MockTriageModel>();
        }

        services.AddScoped<KnowledgeBase>();
        services.AddScoped<TriageService>();
        services.AddSingleton<TriageQueue>();
        services.AddHostedService<TriageWorker>();
        return services;
    }

    /// <summary>Human-readable mode for logs and the UI ("mock" or "openai:gpt-4o-mini").</summary>
    public static string DescribeMode(AiOptions options) => options.UsesRealProvider
        ? $"{(options.IsAzure ? "azure-openai" : "openai")}:{options.ChatModel}"
        : "mock";
}
