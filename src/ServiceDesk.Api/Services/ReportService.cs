using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services;

public record ClientReportRow(int ClientId, string ClientName, int Total, int Open, int Resolved, int ResolvedWithinSla, double? CompliancePercent);
public record CategoryReportRow(TicketCategory Category, int Count);
public record WeeklyComplianceRow(DateOnly WeekStart, int Resolved, int ResolvedWithinSla, double? CompliancePercent);

public record ReportDto(
    DateTime From,
    DateTime To,
    int TotalTickets,
    double? OverallCompliancePercent,
    IReadOnlyList<ClientReportRow> ByClient,
    IReadOnlyList<CategoryReportRow> ByCategory,
    IReadOnlyList<WeeklyComplianceRow> SlaComplianceByWeek);

/// <summary>Flat row used for reporting maths — only the columns the reports need.</summary>
public record ReportTicket(
    int ClientId,
    string ClientName,
    TicketCategory Category,
    TicketStatus Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    DateTime ResolutionDueAt);

/// <summary>
/// Reporting for MSP account managers: volume per client and category, and resolution SLA compliance
/// over time. Querying is separated from the maths (static <c>Build</c>) so the maths is unit-testable.
/// </summary>
public class ReportService(AppDbContext db, TimeProvider clock)
{
    public const int MaxDays = 366;

    public async Task<ReportDto> GetAsync(int days, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 7, MaxDays);
        var to = clock.GetUtcNow().UtcDateTime;
        var from = to.AddDays(-days);

        // Tickets created in the period, or resolved in it (for the compliance trend).
        var rows = await db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= from || (t.ResolvedAt != null && t.ResolvedAt >= from))
            .Select(t => new ReportTicket(t.ClientId, t.Client.Name, t.Category, t.Status, t.CreatedAt, t.ResolvedAt, t.ResolutionDueAt))
            .ToListAsync(ct);

        return Build(rows, from, to);
    }

    public static ReportDto Build(IReadOnlyList<ReportTicket> rows, DateTime from, DateTime to)
    {
        var created = rows.Where(t => t.CreatedAt >= from && t.CreatedAt <= to).ToList();
        var resolved = rows.Where(t => t.ResolvedAt >= from && t.ResolvedAt <= to).ToList();

        var byClient = created
            .GroupBy(t => (t.ClientId, t.ClientName))
            .Select(g =>
            {
                var done = g.Where(t => t.ResolvedAt is not null).ToList();
                var met = done.Count(MetSla);
                return new ClientReportRow(g.Key.ClientId, g.Key.ClientName, g.Count(),
                    g.Count(t => t.Status is not (TicketStatus.Resolved or TicketStatus.Closed)),
                    done.Count, met, Percent(met, done.Count));
            })
            .OrderByDescending(r => r.Total).ThenBy(r => r.ClientName)
            .ToList();

        var byCategory = created
            .GroupBy(t => t.Category)
            .Select(g => new CategoryReportRow(g.Key, g.Count()))
            .OrderByDescending(r => r.Count)
            .ToList();

        // One row per week in the range (including empty weeks, so the chart's x-axis has no gaps).
        var weeks = new List<WeeklyComplianceRow>();
        for (var week = WeekStart(from); week <= WeekStart(to); week = week.AddDays(7))
        {
            var inWeek = resolved.Where(t => WeekStart(t.ResolvedAt!.Value) == week).ToList();
            var met = inWeek.Count(MetSla);
            weeks.Add(new WeeklyComplianceRow(week, inWeek.Count, met, Percent(met, inWeek.Count)));
        }

        var totalMet = resolved.Count(MetSla);
        return new ReportDto(from, to, created.Count, Percent(totalMet, resolved.Count), byClient, byCategory, weeks);
    }

    private static bool MetSla(ReportTicket t) => t.ResolvedAt <= t.ResolutionDueAt;

    private static double? Percent(int part, int whole) => whole == 0 ? null : Math.Round(100.0 * part / whole, 1);

    /// <summary>The Monday (UTC) of the week containing this time.</summary>
    internal static DateOnly WeekStart(DateTime utc)
    {
        var date = DateOnly.FromDateTime(utc);
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
