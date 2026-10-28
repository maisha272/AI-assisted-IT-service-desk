using System.Net;
using System.Net.Http.Json;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services;
using ServiceDesk.Tests.Api;

namespace ServiceDesk.Tests.Services;

public class ReportServiceTests
{
    // Wednesday 11 March 2026
    private static readonly DateTime To = new(2026, 3, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime From = To.AddDays(-28);

    private static ReportTicket Ticket(string client, TicketCategory category, DateTime created, double? resolvedAfterHours, double slaHours = 24)
    {
        var resolved = resolvedAfterHours is null ? (DateTime?)null : created.AddHours(resolvedAfterHours.Value);
        return new ReportTicket(client == "Acme" ? 1 : 2, client, category,
            resolved is null ? TicketStatus.InProgress : TicketStatus.Closed, created, resolved, created.AddHours(slaHours));
    }

    [Fact]
    public void Groups_by_client_with_compliance()
    {
        var rows = new[]
        {
            Ticket("Acme", TicketCategory.Email, To.AddDays(-3), 10),   // met
            Ticket("Acme", TicketCategory.Email, To.AddDays(-4), 30),   // late
            Ticket("Acme", TicketCategory.Network, To.AddDays(-1), null), // open
            Ticket("Bolt", TicketCategory.Email, To.AddDays(-2), 5),    // met
        };

        var report = ReportService.Build(rows, From, To);

        var acme = report.ByClient.Single(c => c.ClientName == "Acme");
        Assert.Equal(3, acme.Total);
        Assert.Equal(1, acme.Open);
        Assert.Equal(2, acme.Resolved);
        Assert.Equal(50.0, acme.CompliancePercent);
        Assert.Equal("Acme", report.ByClient[0].ClientName); // most tickets first
        Assert.Equal(100.0, report.ByClient.Single(c => c.ClientName == "Bolt").CompliancePercent);
        Assert.Equal(4, report.TotalTickets);
        Assert.Equal(66.7, report.OverallCompliancePercent);
    }

    [Fact]
    public void Counts_by_category_most_common_first()
    {
        var rows = new[]
        {
            Ticket("Acme", TicketCategory.Printing, To.AddDays(-1), null),
            Ticket("Acme", TicketCategory.Email, To.AddDays(-1), null),
            Ticket("Bolt", TicketCategory.Email, To.AddDays(-1), null),
        };

        var report = ReportService.Build(rows, From, To);

        Assert.Equal([(TicketCategory.Email, 2), (TicketCategory.Printing, 1)],
            report.ByCategory.Select(c => (c.Category, c.Count)));
    }

    [Fact]
    public void Weekly_trend_has_a_row_for_every_week_including_empty_ones()
    {
        var rows = new[] { Ticket("Acme", TicketCategory.Email, To.AddDays(-2), 1) };

        var report = ReportService.Build(rows, From, To);

        Assert.Equal(5, report.SlaComplianceByWeek.Count); // 28 days spanning 5 Monday-based weeks
        Assert.All(report.SlaComplianceByWeek, w => Assert.Equal(DayOfWeek.Monday, w.WeekStart.DayOfWeek));
        Assert.Null(report.SlaComplianceByWeek[0].CompliancePercent); // no data → null, not 0%
        Assert.Equal(100.0, report.SlaComplianceByWeek[^1].CompliancePercent);
    }

    [Fact]
    public void Tickets_outside_the_range_are_ignored()
    {
        var rows = new[] { Ticket("Acme", TicketCategory.Email, From.AddDays(-30), 1) };

        var report = ReportService.Build(rows, From, To);

        Assert.Equal(0, report.TotalTickets);
        Assert.Empty(report.ByClient);
        Assert.Null(report.OverallCompliancePercent);
    }

    [Theory]
    [InlineData("2026-03-09", "2026-03-09")] // Monday
    [InlineData("2026-03-11", "2026-03-09")] // Wednesday
    [InlineData("2026-03-15", "2026-03-09")] // Sunday belongs to the week that started Monday
    public void Week_start_is_monday(string date, string expectedMonday)
    {
        var utc = DateTime.SpecifyKind(DateTime.Parse(date), DateTimeKind.Utc);
        Assert.Equal(DateOnly.Parse(expectedMonday), ReportService.WeekStart(utc));
    }
}

public class ReportsApiTests(ServiceDeskApiFactory factory) : IClassFixture<ServiceDeskApiFactory>
{
    [Fact]
    public async Task Agent_gets_report_built_from_seed_data()
    {
        var agent = await factory.CreateClientAsAsync(ServiceDeskApiFactory.AgentEmail);

        var report = await agent.GetFromJsonAsync<ReportDto>("/api/reports?days=90", ServiceDeskApiFactory.Json);

        Assert.True(report!.TotalTickets > 0);
        Assert.Equal(report.TotalTickets, report.ByClient.Sum(c => c.Total));
        Assert.Equal(report.TotalTickets, report.ByCategory.Sum(c => c.Count));
        Assert.True(report.SlaComplianceByWeek.Count >= 13);
    }

    [Fact]
    public async Task Client_staff_cannot_see_reports()
    {
        var client = await factory.CreateClientAsAsync(ServiceDeskApiFactory.ClientEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/reports")).StatusCode);
    }
}
