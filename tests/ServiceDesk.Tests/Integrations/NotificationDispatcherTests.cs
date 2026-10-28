using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services;
using ServiceDesk.Api.Services.Integrations;

namespace ServiceDesk.Tests.Integrations;

internal class FakeTeams(bool configured = true, string? failWith = null) : ITeamsNotifier
{
    public List<TeamsMessage> Sent { get; } = [];
    public bool IsConfigured => configured;

    public Task<string?> SendAsync(TeamsMessage message, CancellationToken ct = default)
    {
        Sent.Add(message);
        return Task.FromResult(configured ? failWith : "Webhook not configured (logged only)");
    }
}

public class NotificationDispatcherTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    private readonly TestDb _t = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Start));
    private readonly FakeTeams _teams = new();

    private NotificationDispatcher Dispatcher() => new(_t.Db, _teams,
        Options.Create(new IntegrationOptions { PublicUrl = "https://desk.example" }), _clock, NullLogger<NotificationDispatcher>.Instance);

    private Ticket AddCriticalTicket()
    {
        var ticket = new Ticket
        {
            Title = "Server down", Description = "Main server is down.", Priority = TicketPriority.Critical,
            Client = _t.Client, CreatedBy = _t.ClientUser, CreatedAt = Start, UpdatedAt = Start,
        };
        SlaPolicy.ApplyDeadlines(ticket); // response due in 1h, at risk from 45 min
        _t.Db.Tickets.Add(ticket);
        _t.Db.SaveChanges();
        return ticket;
    }

    [Fact]
    public async Task New_ticket_is_announced_exactly_once()
    {
        var ticket = AddCriticalTicket();

        await Dispatcher().RunOnceAsync();
        await Dispatcher().RunOnceAsync();

        var message = Assert.Single(_teams.Sent);
        Assert.StartsWith($"New ticket: #{ticket.Id}", message.Title);
        Assert.Equal($"https://desk.example/tickets/{ticket.Id}", message.LinkUrl);
        Assert.NotNull(ticket.NewTicketNotifiedAt);
    }

    [Fact]
    public async Task At_risk_then_breach_are_each_announced_once_as_time_passes()
    {
        AddCriticalTicket();
        await Dispatcher().RunOnceAsync(); // "new ticket"

        _clock.Advance(TimeSpan.FromMinutes(50)); // 10 min left of 60 → at risk
        await Dispatcher().RunOnceAsync();
        await Dispatcher().RunOnceAsync();

        _clock.Advance(TimeSpan.FromMinutes(20)); // past the deadline → breached
        await Dispatcher().RunOnceAsync();
        await Dispatcher().RunOnceAsync();

        Assert.Collection(_teams.Sent,
            m => Assert.StartsWith("New ticket", m.Title),
            m => Assert.StartsWith("SLA at risk", m.Title),
            m => Assert.StartsWith("SLA breached", m.Title));
    }

    [Fact]
    public async Task Every_attempt_is_logged_including_when_teams_is_not_configured()
    {
        var notConfigured = new FakeTeams(configured: false);
        AddCriticalTicket();

        await new NotificationDispatcher(_t.Db, notConfigured, Options.Create(new IntegrationOptions()), _clock,
            NullLogger<NotificationDispatcher>.Instance).RunOnceAsync();

        var log = await _t.Db.NotificationLogs.SingleAsync();
        Assert.False(log.Delivered);
        Assert.Contains("not configured", log.Error);
        Assert.Equal(NotificationEvent.NewTicket, log.Event);
    }

    [Fact]
    public async Task Resolved_tickets_are_never_announced()
    {
        var ticket = AddCriticalTicket();
        ticket.Status = TicketStatus.Resolved;
        ticket.ResolvedAt = Start;
        _t.Db.SaveChanges();

        _clock.Advance(TimeSpan.FromHours(10));
        Assert.Equal(0, await Dispatcher().RunOnceAsync());
    }

    [Fact]
    public async Task Changing_priority_re_arms_sla_alerts()
    {
        var ticket = AddCriticalTicket();
        _clock.Advance(TimeSpan.FromHours(2));
        await Dispatcher().RunOnceAsync(); // new + breached
        Assert.NotNull(ticket.BreachNotifiedAt);

        ticket.Priority = TicketPriority.Low;
        SlaPolicy.ApplyDeadlines(ticket);

        Assert.Null(ticket.BreachNotifiedAt);
        Assert.Null(ticket.AtRiskNotifiedAt);
    }

    public void Dispose() => _t.Dispose();
}

public class TeamsPayloadTests
{
    [Fact]
    public void Payload_is_an_adaptive_card_message_with_facts_and_link()
    {
        var payload = TeamsWebhookNotifier.BuildPayload(new TeamsMessage(
            "SLA breached: #5 Printer offline", "The printer is offline.", [("Client", "Acme"), ("Priority", "High")],
            "https://desk.example/tickets/5", "attention"));

        Assert.Equal("message", payload["type"]!.GetValue<string>());
        var attachment = payload["attachments"]![0]!;
        Assert.Equal("application/vnd.microsoft.card.adaptive", attachment["contentType"]!.GetValue<string>());
        var card = attachment["content"]!;
        Assert.Equal("AdaptiveCard", card["type"]!.GetValue<string>());
        Assert.Equal("Attention", card["body"]![0]!["color"]!.GetValue<string>());
        Assert.Equal("Acme", card["body"]![2]!["facts"]![0]!["value"]!.GetValue<string>());
        Assert.Equal("https://desk.example/tickets/5", card["actions"]![0]!["url"]!.GetValue<string>());
    }
}
