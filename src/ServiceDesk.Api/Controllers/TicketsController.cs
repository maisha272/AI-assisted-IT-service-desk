using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Dtos;
using ServiceDesk.Api.Services;
using ServiceDesk.Api.Services.Auth;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public class TicketsController(AppDbContext db, ICurrentUser currentUser, TimeProvider clock) : ControllerBase
{
    private const int MaxResults = 500;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TicketSummaryDto>>> List(
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] string? search,
        [FromQuery] SlaState? sla,
        CancellationToken ct)
    {
        var user = await currentUser.GetAsync(ct);
        var query = db.Tickets.VisibleTo(user);

        if (status is not null) query = query.Where(t => t.Status == status);
        if (priority is not null) query = query.Where(t => t.Priority == priority);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t => t.Title.Contains(term) || t.Description.Contains(term));
        }

        var tickets = await query
            .Include(t => t.Client).Include(t => t.CreatedBy).Include(t => t.AssignedTo)
            .OrderByDescending(t => t.CreatedAt)
            .Take(MaxResults)
            .ToListAsync(ct);

        var now = Now;
        var results = tickets.Select(t => t.ToSummary(now));
        // SLA state depends on "now", so it is filtered after loading rather than in SQL.
        // Fine at this scale (results are capped at MaxResults).
        if (sla is not null) results = results.Where(t => t.Sla.Overall == sla);
        return results.ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDetailDto>> Get(int id, CancellationToken ct)
    {
        var user = await currentUser.GetAsync(ct);
        var ticket = await LoadVisibleTicket(id, user, ct);
        return ticket is null ? NotFound() : ticket.ToDetail(user, Now);
    }

    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var user = await currentUser.GetAsync(ct);

        int clientId;
        if (user.Role == UserRole.ClientStaff)
        {
            clientId = user.ClientId!.Value;
        }
        else
        {
            if (request.ClientId is null || !await db.Clients.AnyAsync(c => c.Id == request.ClientId, ct))
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Agents must choose a valid client when logging a ticket."],
                }));
            clientId = request.ClientId.Value;
        }

        var now = Now;
        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            Category = request.Category,
            Source = TicketSource.Portal,
            ClientId = clientId,
            CreatedById = user.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        SlaPolicy.ApplyDeadlines(ticket);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        var created = (await LoadVisibleTicket(ticket.Id, user, ct))!;
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, created.ToDetail(user, now));
    }

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<TicketDetailDto>> AddComment(int id, AddCommentRequest request, CancellationToken ct)
    {
        var user = await currentUser.GetAsync(ct);
        var ticket = await LoadVisibleTicket(id, user, ct);
        if (ticket is null) return NotFound();
        if (ticket.Status == TicketStatus.Closed)
            return Problem("Closed tickets can't receive new comments.", statusCode: StatusCodes.Status409Conflict);
        if (request.IsInternal && user.Role != UserRole.Agent)
            return Forbid();

        var now = Now;
        ticket.Comments.Add(new Comment
        {
            AuthorId = user.Id,
            Author = user,
            Body = request.Body.Trim(),
            IsInternal = request.IsInternal,
            CreatedAt = now,
        });
        ticket.UpdatedAt = now;
        if (user.Role == UserRole.Agent && !request.IsInternal)
            SlaPolicy.RecordAgentResponse(ticket, now);

        // A client replying to a ticket that was waiting on them puts it back in the agents' queue.
        if (user.Role == UserRole.ClientStaff && ticket.Status == TicketStatus.Waiting)
            TicketWorkflow.Transition(ticket, TicketStatus.InProgress, user, now);

        await db.SaveChangesAsync(ct);
        return ticket.ToDetail(user, now);
    }

    [HttpPost("{id:int}/status")]
    public async Task<ActionResult<TicketDetailDto>> ChangeStatus(int id, ChangeStatusRequest request, CancellationToken ct)
    {
        var user = await currentUser.GetAsync(ct);
        var ticket = await LoadVisibleTicket(id, user, ct);
        if (ticket is null) return NotFound();

        try
        {
            TicketWorkflow.Transition(ticket, request.Status, user, Now);
        }
        catch (InvalidTransitionException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync(ct);
        return ticket.ToDetail(user, Now);
    }

    [HttpPatch("{id:int}")]
    [Authorize(Roles = Roles.Agent)]
    public async Task<ActionResult<TicketDetailDto>> Update(int id, UpdateTicketRequest request, CancellationToken ct)
    {
        var user = await currentUser.GetAsync(ct);
        var ticket = await LoadVisibleTicket(id, user, ct);
        if (ticket is null) return NotFound();

        if (request.Priority is not null && request.Priority != ticket.Priority)
        {
            ticket.Priority = request.Priority.Value;
            // New priority, new deadlines (still measured from when the ticket was raised).
            SlaPolicy.ApplyDeadlines(ticket);
        }
        if (request.Category is not null) ticket.Category = request.Category.Value;
        ticket.UpdatedAt = Now;

        await db.SaveChangesAsync(ct);
        return ticket.ToDetail(user, Now);
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = Roles.Agent)]
    public async Task<ActionResult<TicketDetailDto>> Assign(int id, AssignTicketRequest request, CancellationToken ct)
    {
        var user = await currentUser.GetAsync(ct);
        var ticket = await LoadVisibleTicket(id, user, ct);
        if (ticket is null) return NotFound();

        if (request.AgentId is not null)
        {
            var agent = await db.Users.FirstOrDefaultAsync(u => u.Id == request.AgentId && u.Role == UserRole.Agent, ct);
            if (agent is null)
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["agentId"] = ["Tickets can only be assigned to agents."],
                }));
            ticket.AssignedTo = agent;
        }
        ticket.AssignedToId = request.AgentId;
        ticket.UpdatedAt = Now;

        await db.SaveChangesAsync(ct);
        return ticket.ToDetail(user, Now);
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private Task<Ticket?> LoadVisibleTicket(int id, User user, CancellationToken ct) =>
        db.Tickets.VisibleTo(user)
            .Include(t => t.Client)
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.Comments).ThenInclude(c => c.Author)
            .Include(t => t.StatusChanges).ThenInclude(s => s.ChangedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
}
