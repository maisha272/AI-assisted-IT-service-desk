using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Dtos;

namespace ServiceDesk.Api.Services;

public record PriorityCount(TicketPriority Priority, int Count);
public record StatusCount(TicketStatus Status, int Count);

public record DashboardDto(
    int OpenTickets,
    int UnassignedTickets,
    int BreachedTickets,
    int AtRiskTickets,
    IReadOnlyList<PriorityCount> OpenByPriority,
    IReadOnlyList<StatusCount> OpenByStatus,
    int PendingTriageSuggestions,
    int ResolvedLast30Days,
    double? AverageResolutionMinutesLast30Days,
    double? SlaCompliancePercentLast30Days,
    IReadOnlyList<TicketSummaryDto> NeedsAttention);

/// <summary>Builds the agent dashboard: workload, SLA risk and recent performance.</summary>
public class DashboardService(AppDbContext db, TimeProvider clock)
{
    public const int NeedsAttentionLimit = 10;

    public async Task<DashboardDto> BuildAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var since = now.AddDays(-30);

        // Open tickets are a small working set, so we load them once and calculate in memory
        // (SLA state depends on "now" and isn't stored).
        var open = await db.Tickets.Open()
            .Include(t => t.Client).Include(t => t.CreatedBy).Include(t => t.AssignedTo)
            .AsNoTracking()
            .ToListAsync(ct);
        var openWithSla = open.Select(t => (Ticket: t, Sla: SlaPolicy.Evaluate(t, now))).ToList();

        var resolved = await db.Tickets
            .Where(t => t.ResolvedAt != null && t.ResolvedAt >= since)
            .Select(t => new { t.CreatedAt, ResolvedAt = t.ResolvedAt!.Value, t.ResolutionDueAt })
            .ToListAsync(ct);

        var needsAttention = openWithSla
            .Where(x => x.Sla.Overall is SlaState.Breached or SlaState.AtRisk)
            // Most urgent first: the deadline that's currently running, earliest first.
            .OrderBy(x => x.Ticket.FirstRespondedAt is null ? x.Ticket.ResponseDueAt : x.Ticket.ResolutionDueAt)
            .Take(NeedsAttentionLimit)
            .Select(x => x.Ticket.ToSummary(now))
            .ToList();

        return new DashboardDto(
            OpenTickets: open.Count,
            UnassignedTickets: open.Count(t => t.AssignedToId is null),
            BreachedTickets: openWithSla.Count(x => x.Sla.Overall == SlaState.Breached),
            AtRiskTickets: openWithSla.Count(x => x.Sla.Overall == SlaState.AtRisk),
            OpenByPriority: Enum.GetValues<TicketPriority>()
                .Select(p => new PriorityCount(p, open.Count(t => t.Priority == p))).ToList(),
            OpenByStatus: new[] { TicketStatus.New, TicketStatus.InProgress, TicketStatus.Waiting }
                .Select(s => new StatusCount(s, open.Count(t => t.Status == s))).ToList(),
            PendingTriageSuggestions: await db.TriageSuggestions.CountAsync(s => s.Status == TriageStatus.Pending, ct),
            ResolvedLast30Days: resolved.Count,
            AverageResolutionMinutesLast30Days: resolved.Count == 0
                ? null
                : Math.Round(resolved.Average(r => (r.ResolvedAt - r.CreatedAt).TotalMinutes)),
            SlaCompliancePercentLast30Days: resolved.Count == 0
                ? null
                : Math.Round(100.0 * resolved.Count(r => r.ResolvedAt <= r.ResolutionDueAt) / resolved.Count, 1),
            NeedsAttention: needsAttention);
    }
}
