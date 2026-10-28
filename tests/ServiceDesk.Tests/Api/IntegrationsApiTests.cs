using System.Net;
using System.Net.Http.Json;
using ServiceDesk.Api.Controllers;
using ServiceDesk.Api.Services.Integrations;

namespace ServiceDesk.Tests.Api;

public class IntegrationsApiTests(ServiceDeskApiFactory factory) : IClassFixture<ServiceDeskApiFactory>
{
    private static readonly InboundEmailRequest Email =
        new("Olivia Brown <olivia@northwind.test>", "Can't open shared calendar", "The shared reception calendar gives an error.");

    private HttpRequestMessage InboundRequest(string? secret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/email/inbound") { Content = JsonContent.Create(Email) };
        if (secret is not null) request.Headers.Add(IntegrationsController.SecretHeader, secret);
        return request;
    }

    [Fact]
    public async Task Inbound_email_with_correct_secret_creates_a_ticket()
    {
        var response = await factory.CreateClient().SendAsync(InboundRequest(ServiceDeskApiFactory.InboundEmailSecret));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<InboundEmailResult>(ServiceDeskApiFactory.Json);
        Assert.Equal(InboundEmailOutcome.TicketCreated, result!.Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-secret")]
    public async Task Inbound_email_without_valid_secret_is_rejected(string? secret)
    {
        var response = await factory.CreateClient().SendAsync(InboundRequest(secret));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Email_from_unknown_domain_returns_422()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/email/inbound")
        {
            Content = JsonContent.Create(Email with { From = "someone@gmail.test" }),
        };
        request.Headers.Add(IntegrationsController.SecretHeader, ServiceDeskApiFactory.InboundEmailSecret);

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Agent_can_dispatch_and_read_notification_log()
    {
        var agent = await factory.CreateClientAsAsync(ServiceDeskApiFactory.AgentEmail);

        (await agent.PostAsync("/api/integrations/notifications/dispatch", null)).EnsureSuccessStatusCode();
        var test = await agent.PostAsync("/api/integrations/notifications/test", null);
        var log = await agent.GetFromJsonAsync<List<NotificationLogDto>>("/api/integrations/notifications", ServiceDeskApiFactory.Json);

        test.EnsureSuccessStatusCode();
        Assert.NotEmpty(log!);
        Assert.All(log!, n => Assert.False(n.Delivered)); // no webhook configured in tests → logged only
    }

    [Fact]
    public async Task Client_staff_cannot_use_integration_admin_endpoints()
    {
        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/integrations/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/integrations/email/simulate", Email)).StatusCode);
    }
}
