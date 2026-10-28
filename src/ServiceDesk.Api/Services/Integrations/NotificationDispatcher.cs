using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services.Integrations;

/// <summary>
/// Finds tickets that need a Teams notification (new, SLA at risk, SLA breached) and sends each one
/// exactly once, using the <c>...NotifiedAt</c> timestamps on the ticket as "already sent" markers.
/// <para>
/// This is a simple form of the <b>outbox pattern</b>: creating a ticket never calls Teams directly.
/// The ticket row itself records what still needs announcing, and this dispatcher catches up — so a
/// slow or failing webhook can't slow down or break ticket creation, and nothing is lost on restart.
/// </para>
/// </summary>
public class NotificationDispatcher(
    AppDbContext db,
    ITeamsNotifier teams,
    IOptions<IntegrationOptions> options,
    TimeProvider clock,
    ILogger<NotificationDispatcher> logger)
{
    /// <summary>Returns how many notifications were attempted.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var candidates = await db.Tickets.Open()
            .Where(t => t.NewTicketNotifiedAt == null || t.BreachNotifiedAt == null)
            .Include(t => t.Client).Include(t => t.AssignedTo)
            .ToListAsync(ct);

        var attempted = 0;
        foreach (var ticket in candidates)
        {
            if (ticket.NewTicketNotifiedAt is null)
            {
                await SendAsync(ticket, NotificationEvent.NewTicket, now, ct);
                ticket.NewTicketNotifiedAt = now;
                attempted++;
            }

            var state = SlaPolicy.Evaluate(ticket, now).Overall;
            if (state == SlaState.Breached && ticket.BreachNotifiedAt is null)
            {
                await SendAsync(ticket, NotificationEvent.SlaBreached, now, ct);
                ticket.BreachNotifiedAt = now;
                ticket.AtRiskNotifiedAt ??= now; // a breach supersedes the "at risk" warning
                attempted++;
            }
            else if (state == SlaState.AtRisk && ticket.AtRiskNotifiedAt is null)
            {
                await SendAsync(ticket, NotificationEvent.SlaAtRisk, now, ct);
                ticket.AtRiskNotifiedAt = now;
                attempted++;
            }
        }

        if (attempted > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Dispatched {Count} notifications", attempted);
        }
        return attempted;
    }

    public async Task<NotificationLog> SendTestAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var message = new TeamsMessage("Service Desk test message",
            "If you can see this, the Teams integration is working.", [("Sent at", $"{now:yyyy-MM-dd HH:mm} UTC")], null);
        var log = await LogAsync(message, NotificationEvent.Test, null, now, ct);
        await db.SaveChangesAsync(ct);
        return log;
    }

    private async Task SendAsync(Ticket ticket, NotificationEvent evt, DateTime now, CancellationToken ct) =>
        await LogAsync(TeamsWebhookNotifier.ForTicket(ticket, evt, options.Value.PublicUrl, now), evt, ticket.Id, now, ct);

    private async Task<NotificationLog> LogAsync(TeamsMessage message, NotificationEvent evt, int? ticketId, DateTime now, CancellationToken ct)
    {
        // Marked as handled even if delivery fails, so a broken webhook doesn't cause an endless retry storm.
        // The failure is visible in the log on the Integrations page.
        var error = await teams.SendAsync(message, ct);
        var log = new NotificationLog
        {
            Channel = "Teams",
            Event = evt,
            TicketId = ticketId,
            Message = message.Title,
            Delivered = error is null,
            Error = error,
            CreatedAt = now,
        };
        db.NotificationLogs.Add(log);
        return log;
    }
}

/// <summary>Runs the dispatcher on a timer.</summary>
public class NotificationWorker(IServiceScopeFactory scopes, IOptions<IntegrationOptions> options, ILogger<NotificationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, options.Value.DispatchIntervalSeconds)));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Notification dispatch failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
