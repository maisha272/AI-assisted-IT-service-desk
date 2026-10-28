using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Api.Dtos;

/// <summary>Hand-written entity → DTO mapping. Explicit and easy to follow (no AutoMapper magic).</summary>
public static class TicketMapping
{
    public static TicketSummaryDto ToSummary(this Ticket t) => new(
        t.Id, t.Title, t.Status, t.Priority, t.Category, t.Source,
        t.ClientId, t.Client.Name, t.CreatedBy.DisplayName, t.AssignedTo?.DisplayName,
        t.CreatedAt, t.UpdatedAt);

    public static TicketDetailDto ToDetail(this Ticket t, User viewer) => new(
        t.Id, t.Title, t.Description, t.Status, t.Priority, t.Category, t.Source,
        t.ClientId, t.Client.Name, t.CreatedBy.DisplayName, t.AssignedToId, t.AssignedTo?.DisplayName,
        t.CreatedAt, t.UpdatedAt, t.ResolvedAt, t.ClosedAt,
        TicketWorkflow.AllowedNextStatuses(t.Status, viewer.Role),
        t.Comments
            .Where(c => viewer.Role == UserRole.Agent || !c.IsInternal)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(c.Id, c.Author.DisplayName, c.Author.Role, c.Body, c.IsInternal, c.CreatedAt))
            .ToList(),
        t.StatusChanges
            .OrderBy(s => s.ChangedAt)
            .Select(s => new StatusChangeDto(s.FromStatus, s.ToStatus, s.ChangedBy.DisplayName, s.ChangedAt))
            .ToList());
}
