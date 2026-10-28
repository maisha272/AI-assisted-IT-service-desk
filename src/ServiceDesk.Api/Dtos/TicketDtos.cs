using System.ComponentModel.DataAnnotations;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Api.Dtos;

// DTOs (data transfer objects) are the shapes the API sends and receives. Keeping them separate from
// the EF entities means we never accidentally expose internal fields, and the database schema can
// change without breaking the front end.

public record TicketSummaryDto(
    int Id,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    TicketCategory Category,
    TicketSource Source,
    int ClientId,
    string ClientName,
    string CreatedByName,
    string? AssignedToName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    SlaDto Sla);

public record SlaDto(
    DateTime ResponseDueAt,
    DateTime ResolutionDueAt,
    DateTime? FirstRespondedAt,
    SlaState ResponseState,
    SlaState ResolutionState,
    SlaState Overall);

public record CommentDto(
    int Id,
    string AuthorName,
    UserRole AuthorRole,
    string Body,
    bool IsInternal,
    DateTime CreatedAt);

public record StatusChangeDto(
    TicketStatus FromStatus,
    TicketStatus ToStatus,
    string ChangedByName,
    DateTime ChangedAt);

public record TicketDetailDto(
    int Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    TicketCategory Category,
    TicketSource Source,
    int ClientId,
    string ClientName,
    string CreatedByName,
    int? AssignedToId,
    string? AssignedToName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    SlaDto Sla,
    IReadOnlyList<TicketStatus> AllowedNextStatuses,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<StatusChangeDto> StatusChanges);

public record CreateTicketRequest(
    [Required, StringLength(200, MinimumLength = 5)] string Title,
    [Required, StringLength(8000, MinimumLength = 10)] string Description,
    TicketPriority Priority = TicketPriority.Medium,
    TicketCategory Category = TicketCategory.Other,
    // Only used when an agent logs a ticket on a client's behalf; client staff always use their own client.
    int? ClientId = null);

public record AddCommentRequest(
    [Required, StringLength(8000, MinimumLength = 1)] string Body,
    bool IsInternal = false);

public record ChangeStatusRequest([Required] TicketStatus Status);

public record UpdateTicketRequest(
    TicketPriority? Priority,
    TicketCategory? Category);

public record AssignTicketRequest(int? AgentId);
