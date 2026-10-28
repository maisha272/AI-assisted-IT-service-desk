using Microsoft.Extensions.Time.Testing;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Tests.Services;

public class DashboardServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestDb _t = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Now));

    private Ticket AddTicket(TicketPriority priority, DateTime createdAt, TicketStatus status = TicketStatus.New,
        DateTime? resolvedAt = null, bool assigned = false)
    {
        var ticket = new Ticket
        {
            Title = "T", Description = "D", Priority = priority, Status = status,
            Client = _t.Client, CreatedBy = _t.ClientUser, AssignedTo = assigned ? _t.Agent : null,
            CreatedAt = createdAt, UpdatedAt = createdAt, ResolvedAt = resolvedAt,
        };
        SlaPolicy.ApplyDeadlines(ticket);
        _t.Db.Tickets.Add(ticket);
        return ticket;
    }

    [Fact]
    public async Task Counts_open_tickets_by_priority_and_sla_state()
    {
        AddTicket(TicketPriority.Critical, Now.AddHours(-2));                 // response (1h) breached
        AddTicket(TicketPriority.High, Now.AddMinutes(-200), assigned: true); // response 4h: 40m left -> at risk
        AddTicket(TicketPriority.Low, Now.AddMinutes(-10), assigned: true);   // on track
        AddTicket(TicketPriority.Low, Now.AddDays(-2), TicketStatus.Resolved, resolvedAt: Now.AddDays(-1)); // not open
        await _t.Db.SaveChangesAsync();

        var dashboard = await new DashboardService(_t.Db, _clock).BuildAsync();

        Assert.Equal(3, dashboard.OpenTickets);
        Assert.Equal(1, dashboard.UnassignedTickets);
        Assert.Equal(1, dashboard.BreachedTickets);
        Assert.Equal(1, dashboard.AtRiskTickets);
        Assert.Equal(1, dashboard.OpenByPriority.Single(p => p.Priority == TicketPriority.Critical).Count);
        Assert.Equal(0, dashboard.OpenByPriority.Single(p => p.Priority == TicketPriority.Medium).Count);
        Assert.Equal(1, dashboard.OpenByPriority.Single(p => p.Priority == TicketPriority.Low).Count); // the resolved one is excluded
    }

    [Fact]
    public async Task Needs_attention_lists_breached_before_at_risk()
    {
        var atRisk = AddTicket(TicketPriority.High, Now.AddMinutes(-200));
        var breached = AddTicket(TicketPriority.Critical, Now.AddHours(-2));
        AddTicket(TicketPriority.Low, Now.AddMinutes(-10));
        await _t.Db.SaveChangesAsync();

        var dashboard = await new DashboardService(_t.Db, _clock).BuildAsync();

        Assert.Equal([breached.Id, atRisk.Id], dashboard.NeedsAttention.Select(t => t.Id));
    }

    [Fact]
    public async Task Average_resolution_and_compliance_cover_the_last_30_days()
    {
        // Medium = 72h resolution target.
        AddTicket(TicketPriority.Medium, Now.AddDays(-5), TicketStatus.Resolved, resolvedAt: Now.AddDays(-5).AddHours(10)); // met, 10h
        AddTicket(TicketPriority.Medium, Now.AddDays(-9), TicketStatus.Closed, resolvedAt: Now.AddDays(-9).AddHours(80));   // late, 80h
        AddTicket(TicketPriority.Medium, Now.AddDays(-60), TicketStatus.Closed, resolvedAt: Now.AddDays(-59));              // too old, ignored
        await _t.Db.SaveChangesAsync();

        var dashboard = await new DashboardService(_t.Db, _clock).BuildAsync();

        Assert.Equal(2, dashboard.ResolvedLast30Days);
        Assert.Equal(45 * 60, dashboard.AverageResolutionMinutesLast30Days); // (10h + 80h) / 2 = 45h
        Assert.Equal(50.0, dashboard.SlaCompliancePercentLast30Days);
    }

    [Fact]
    public async Task Empty_history_returns_null_averages_instead_of_dividing_by_zero()
    {
        var dashboard = await new DashboardService(_t.Db, _clock).BuildAsync();

        Assert.Equal(0, dashboard.OpenTickets);
        Assert.Null(dashboard.AverageResolutionMinutesLast30Days);
        Assert.Null(dashboard.SlaCompliancePercentLast30Days);
    }

    public void Dispose() => _t.Dispose();
}
