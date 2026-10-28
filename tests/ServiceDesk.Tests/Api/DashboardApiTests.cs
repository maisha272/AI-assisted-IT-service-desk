using System.Net;
using System.Net.Http.Json;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Tests.Api;

public class DashboardApiTests(ServiceDeskApiFactory factory) : IClassFixture<ServiceDeskApiFactory>
{
    [Fact]
    public async Task Agent_gets_consistent_dashboard_figures()
    {
        var agent = await factory.CreateClientAsAsync(ServiceDeskApiFactory.AgentEmail);

        var dashboard = await agent.GetFromJsonAsync<DashboardDto>("/api/dashboard", ServiceDeskApiFactory.Json);

        Assert.NotNull(dashboard);
        Assert.True(dashboard.OpenTickets > 0);
        Assert.Equal(dashboard.OpenTickets, dashboard.OpenByPriority.Sum(p => p.Count));
        Assert.Equal(dashboard.OpenTickets, dashboard.OpenByStatus.Sum(s => s.Count));
        Assert.All(dashboard.NeedsAttention, t => Assert.Contains(t.Sla.Overall, new[] { SlaState.Breached, SlaState.AtRisk }));
        Assert.InRange(dashboard.SlaCompliancePercentLast30Days!.Value, 0, 100);
    }

    [Fact]
    public async Task Client_staff_cannot_see_dashboard()
    {
        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);
        var response = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
