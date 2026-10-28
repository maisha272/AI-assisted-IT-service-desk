using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services.Triage;

namespace ServiceDesk.Tests.Triage;

/// <summary>Captures the outgoing HTTP request and returns a canned response — no real network calls.</summary>
internal class FakeHandler(HttpStatusCode status, string responseJson) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }
    public JsonObject? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Request = request;
        Body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsObject();
        return new HttpResponseMessage(status) { Content = new StringContent(responseJson) };
    }
}

public class OpenAiClientTests
{
    private const string ChatResponse = """
        {"choices":[{"message":{"content":"{\"category\":\"Email\",\"priority\":\"High\",\"summary\":\"No external mail.\",\"steps\":[\"Check transport rules\"],\"relatedArticleIds\":[3],\"confidence\":\"high\",\"rationale\":\"Email keywords.\"}"}}]}
        """;

    private static readonly TriageInput Input = new("No external email", "Not receiving external emails since 9am.", "Acme",
        [new RetrievedArticle(3, "Not receiving external emails", TicketCategory.Email, "p", "Fixed transport rule.", 0.8)]);

    private static (OpenAiTriageModel Model, FakeHandler Handler) Create(AiOptions options, HttpStatusCode status = HttpStatusCode.OK, string response = ChatResponse)
    {
        var handler = new FakeHandler(status, response);
        var client = new OpenAiClient(new HttpClient(handler), Options.Create(options));
        return (new OpenAiTriageModel(client, Options.Create(options)), handler);
    }

    [Fact]
    public async Task OpenAI_request_uses_bearer_auth_model_name_and_strict_json_schema()
    {
        var (model, handler) = Create(new AiOptions { Provider = "OpenAI", ApiKey = "sk-test", ChatModel = "gpt-4o-mini" });

        var result = await model.SuggestAsync(Input);

        Assert.Equal("https://api.openai.com/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test", handler.Request.Headers.Authorization.Parameter);
        Assert.Equal("gpt-4o-mini", handler.Body!["model"]!.GetValue<string>());
        Assert.Equal("json_schema", handler.Body["response_format"]!["type"]!.GetValue<string>());
        Assert.True(handler.Body["response_format"]!["json_schema"]!["strict"]!.GetValue<bool>());

        Assert.Equal("Email", result.Category);
        Assert.Equal([3], result.RelatedArticleIds);
    }

    [Fact]
    public async Task Azure_request_uses_deployment_url_and_api_key_header()
    {
        var (model, handler) = Create(new AiOptions
        {
            Provider = "AzureOpenAI", ApiKey = "azure-key", ChatModel = "triage-deployment",
            Endpoint = "https://my-resource.openai.azure.com/", ApiVersion = "2024-10-21",
        });

        await model.SuggestAsync(Input);

        Assert.Equal("https://my-resource.openai.azure.com/openai/deployments/triage-deployment/chat/completions?api-version=2024-10-21",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal("azure-key", handler.Request.Headers.GetValues("api-key").Single());
        Assert.Null(handler.Request.Headers.Authorization);
        Assert.False(handler.Body!.ContainsKey("model")); // Azure picks the model from the deployment
    }

    [Fact]
    public async Task Ticket_text_is_sent_as_data_and_the_system_prompt_warns_about_injection()
    {
        var (model, handler) = Create(new AiOptions { Provider = "OpenAI", ApiKey = "k" });

        await model.SuggestAsync(Input);

        var messages = handler.Body!["messages"]!.AsArray();
        Assert.Contains("untrusted DATA", messages[0]!["content"]!.GetValue<string>());
        var userMessage = messages[1]!["content"]!.GetValue<string>();
        Assert.Contains("\"description\": \"Not receiving external emails since 9am.\"", userMessage);
        Assert.Contains("\"id\": 3", userMessage);
    }

    [Fact]
    public async Task Model_can_be_called_repeatedly()
    {
        // Regression test: a shared JSON schema node used to throw "node already has a parent" on the 2nd call.
        var (model, _) = Create(new AiOptions { Provider = "OpenAI", ApiKey = "k" });

        await model.SuggestAsync(Input);
        var second = await model.SuggestAsync(Input);

        Assert.Equal("High", second.Priority);
    }

    [Fact]
    public async Task Provider_errors_surface_as_HttpRequestException()
    {
        var (model, _) = Create(new AiOptions { Provider = "OpenAI", ApiKey = "bad" },
            HttpStatusCode.Unauthorized, """{"error":{"message":"Incorrect API key"}}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => model.SuggestAsync(Input));
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task Embeddings_are_returned_in_input_order()
    {
        var handler = new FakeHandler(HttpStatusCode.OK,
            """{"data":[{"index":1,"embedding":[0.0,1.0]},{"index":0,"embedding":[1.0,0.0]}]}""");
        var options = Options.Create(new AiOptions { Provider = "OpenAI", ApiKey = "k" });
        var client = new OpenAiClient(new HttpClient(handler), options);

        var vectors = await client.EmbedAsync(["first", "second"], CancellationToken.None);

        Assert.Equal("https://api.openai.com/v1/embeddings", handler.Request!.RequestUri!.ToString());
        Assert.Equal([1f, 0f], vectors[0]);
        Assert.Equal([0f, 1f], vectors[1]);
    }

    [Theory]
    [InlineData("Mock", "sk-x", false)]
    [InlineData("OpenAI", null, false)]   // no key: fall back to mock so the app still runs
    [InlineData("OpenAI", "", false)]
    [InlineData("OpenAI", "sk-x", true)]
    [InlineData("AzureOpenAI", "key", true)]
    public void Real_provider_is_used_only_when_configured_with_a_key(string provider, string? key, bool expected)
    {
        Assert.Equal(expected, new AiOptions { Provider = provider, ApiKey = key }.UsesRealProvider);
    }
}
