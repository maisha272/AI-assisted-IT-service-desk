using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services;

public static class TicketQueries
{
    /// <summary>
    /// Row-level access control in one place: agents see every ticket, client staff only the tickets
    /// they raised. Every ticket query in the API starts from this, so the rule can't be forgotten.
    /// </summary>
    public static IQueryable<Ticket> VisibleTo(this IQueryable<Ticket> tickets, User user) =>
        user.Role == UserRole.Agent
            ? tickets
            : tickets.Where(t => t.CreatedById == user.Id);

    public static IQueryable<Ticket> Open(this IQueryable<Ticket> tickets) =>
        tickets.Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);
}
