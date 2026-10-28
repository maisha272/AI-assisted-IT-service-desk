using System.Net;

namespace ServiceDesk.Tests.Api;

/// <summary>Cross-cutting behaviour: health checks, routing fallbacks and security headers.</summary>
public class InfrastructureApiTests(ServiceDeskApiFactory factory) : IClassFixture<ServiceDeskApiFactory>
{
    [Fact]
    public async Task Health_endpoint_is_public_and_checks_the_database()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_api_route_is_404_not_the_spa()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Responses_include_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }
}
