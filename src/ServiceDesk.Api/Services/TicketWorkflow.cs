using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services;

public class InvalidTransitionException(TicketStatus from, TicketStatus to)
    : Exception($"A ticket cannot move from {from} to {to}.");

/// <summary>
/// The ticket status state machine. Pure logic (no database), so it is easy to unit test.
/// <code>
///   New ──► InProgress ◄──► Waiting
///               │  ▲           │
///               ▼  │ (reopen)  ▼
///             Resolved ◄───────┘
///               │
///               ▼
///             Closed (final)
/// </code>
/// </summary>
public static class TicketWorkflow
{
    private static readonly Dictionary<TicketStatus, TicketStatus[]> AgentTransitions = new()
    {
        [TicketStatus.New] = [TicketStatus.InProgress],
        [TicketStatus.InProgress] = [TicketStatus.Waiting, TicketStatus.Resolved],
        [TicketStatus.Waiting] = [TicketStatus.InProgress, TicketStatus.Resolved],
        [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
        [TicketStatus.Closed] = [],
    };

    // Client staff may only: reply to a ticket waiting on them (back to InProgress), confirm a fix
    // (close), or say it isn't fixed (reopen).
    private static readonly Dictionary<TicketStatus, TicketStatus[]> ClientTransitions = new()
    {
        [TicketStatus.Waiting] = [TicketStatus.InProgress],
        [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
    };

    public static IReadOnlyList<TicketStatus> AllowedNextStatuses(TicketStatus current, UserRole role)
    {
        var map = role == UserRole.Agent ? AgentTransitions : ClientTransitions;
        return map.TryGetValue(current, out var next) ? next : [];
    }

    public static bool CanTransition(TicketStatus from, TicketStatus to, UserRole role) =>
        AllowedNextStatuses(from, role).Contains(to);

    /// <summary>Moves the ticket to a new status and records an audit entry on the ticket.</summary>
    /// <exception cref="InvalidTransitionException">If the move is not allowed for this role.</exception>
    public static TicketStatusChange Transition(Ticket ticket, TicketStatus to, User actor, DateTime now)
    {
        var from = ticket.Status;
        if (!CanTransition(from, to, actor.Role))
            throw new InvalidTransitionException(from, to);

        ticket.Status = to;
        ticket.UpdatedAt = now;
        if (actor.Role == UserRole.Agent)
            SlaPolicy.RecordAgentResponse(ticket, now);

        switch (to)
        {
            case TicketStatus.Resolved:
                ticket.ResolvedAt = now;
                break;
            case TicketStatus.Closed:
                ticket.ClosedAt = now;
                break;
            case TicketStatus.InProgress when from == TicketStatus.Resolved:
                // Reopened: the earlier resolution didn't stick, so it no longer counts.
                ticket.ResolvedAt = null;
                break;
        }

        var change = new TicketStatusChange
        {
            Ticket = ticket,
            FromStatus = from,
            ToStatus = to,
            ChangedById = actor.Id,
            ChangedBy = actor,
            ChangedAt = now,
        };
        ticket.StatusChanges.Add(change);
        return change;
    }
}
