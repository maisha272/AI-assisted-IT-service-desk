using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services;

public enum SlaState
{
    /// <summary>Still running, plenty of time left.</summary>
    OnTrack,

    /// <summary>Still running, less than 25% of the time window left.</summary>
    AtRisk,

    /// <summary>Deadline passed without the target being met (still open, or completed late).</summary>
    Breached,

    /// <summary>Target met before the deadline.</summary>
    Met
}

public record SlaTargets(TimeSpan Response, TimeSpan Resolution);

/// <summary>SLA status of one ticket at a point in time.</summary>
public record TicketSla(
    DateTime ResponseDueAt,
    DateTime ResolutionDueAt,
    DateTime? FirstRespondedAt,
    SlaState ResponseState,
    SlaState ResolutionState)
{
    /// <summary>The single state to show in lists: the worse of response and resolution.</summary>
    public SlaState Overall => (ResponseState, ResolutionState) switch
    {
        _ when ResponseState == SlaState.Breached || ResolutionState == SlaState.Breached => SlaState.Breached,
        _ when ResponseState == SlaState.AtRisk || ResolutionState == SlaState.AtRisk => SlaState.AtRisk,
        _ when ResolutionState == SlaState.Met => SlaState.Met,
        _ => SlaState.OnTrack,
    };
}

/// <summary>
/// Service level agreement rules. Each priority has two targets, measured from ticket creation:
/// <list type="bullet">
/// <item><b>Response</b> — an agent first acts on the ticket (public reply or status change).</item>
/// <item><b>Resolution</b> — the ticket reaches Resolved.</item>
/// </list>
/// Pure functions only, so the rules are easy to unit test with fixed times.
/// </summary>
public static class SlaPolicy
{
    /// <summary>A running SLA is "at risk" once less than this fraction of its window remains.</summary>
    public const double AtRiskRemainingFraction = 0.25;

    private static readonly Dictionary<TicketPriority, SlaTargets> Targets = new()
    {
        [TicketPriority.Critical] = new(TimeSpan.FromHours(1), TimeSpan.FromHours(4)),
        [TicketPriority.High] = new(TimeSpan.FromHours(4), TimeSpan.FromHours(24)),
        [TicketPriority.Medium] = new(TimeSpan.FromHours(8), TimeSpan.FromHours(72)),
        [TicketPriority.Low] = new(TimeSpan.FromHours(24), TimeSpan.FromHours(120)),
    };

    public static SlaTargets For(TicketPriority priority) => Targets[priority];

    public static IReadOnlyDictionary<TicketPriority, SlaTargets> AllTargets => Targets;

    /// <summary>
    /// Sets the ticket's due dates from its priority. Called on creation and whenever the priority
    /// changes; deadlines are always measured from when the ticket was raised.
    /// </summary>
    public static void ApplyDeadlines(Ticket ticket)
    {
        var targets = For(ticket.Priority);
        ticket.ResponseDueAt = ticket.CreatedAt + targets.Response;
        ticket.ResolutionDueAt = ticket.CreatedAt + targets.Resolution;
        // New deadlines: re-arm the at-risk/breach alerts so they fire against the new times.
        ticket.AtRiskNotifiedAt = null;
        ticket.BreachNotifiedAt = null;
    }

    /// <summary>Records the first agent action, which stops the response clock.</summary>
    public static void RecordAgentResponse(Ticket ticket, DateTime now) => ticket.FirstRespondedAt ??= now;

    public static SlaState Evaluate(DateTime start, DateTime due, DateTime? completedAt, DateTime now)
    {
        if (completedAt is not null)
            return completedAt <= due ? SlaState.Met : SlaState.Breached;

        if (now > due)
            return SlaState.Breached;

        var window = due - start;
        var remaining = due - now;
        return remaining.Ticks <= window.Ticks * AtRiskRemainingFraction ? SlaState.AtRisk : SlaState.OnTrack;
    }

    public static TicketSla Evaluate(Ticket ticket, DateTime now)
    {
        // A closed ticket with no resolution time (shouldn't happen) is treated as resolved when closed.
        var resolvedAt = ticket.ResolvedAt ?? ticket.ClosedAt;
        // If a ticket was resolved without any recorded agent response, the resolution counts as the response.
        var respondedAt = ticket.FirstRespondedAt ?? resolvedAt;

        return new TicketSla(
            ticket.ResponseDueAt,
            ticket.ResolutionDueAt,
            ticket.FirstRespondedAt,
            Evaluate(ticket.CreatedAt, ticket.ResponseDueAt, respondedAt, now),
            Evaluate(ticket.CreatedAt, ticket.ResolutionDueAt, resolvedAt, now));
    }
}
