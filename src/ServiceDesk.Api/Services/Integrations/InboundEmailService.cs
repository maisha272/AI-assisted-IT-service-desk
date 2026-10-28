using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services.Triage;

namespace ServiceDesk.Api.Services.Integrations;

/// <summary>An email as posted by the mail provider's inbound webhook (already parsed to plain text).</summary>
public record InboundEmail(string From, string Subject, string Body);

public enum InboundEmailOutcome
{
    TicketCreated,
    CommentAdded,
    Rejected
}

public record InboundEmailResult(InboundEmailOutcome Outcome, int? TicketId, string Message);

/// <summary>
/// Turns emails sent to the support mailbox into tickets:
/// <list type="bullet">
/// <item>Sender matched to a known user, or a client by email domain (new contacts are added automatically).</item>
/// <item>Unknown domains are rejected — the desk only supports its clients.</item>
/// <item>A subject containing "[#123]" from that ticket's raiser is treated as a reply and added as a comment.</item>
/// </list>
/// </summary>
public partial class InboundEmailService(AppDbContext db, TriageQueue triageQueue, TimeProvider clock, ILogger<InboundEmailService> logger)
{
    public async Task<InboundEmailResult> ProcessAsync(InboundEmail email, CancellationToken ct = default)
    {
        var from = ExtractAddress(email.From);
        if (from is null)
            return Reject($"Could not read a sender address from \"{email.From}\".");

        var domain = from[(from.IndexOf('@') + 1)..];
        var client = await db.Clients.FirstOrDefaultAsync(c => c.EmailDomain == domain, ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == from, ct);

        if (user?.Role == UserRole.Agent)
            return Reject("Emails from agents are not turned into tickets.");
        if (client is null)
            return Reject($"Sender domain \"{domain}\" doesn't belong to a supported client.");

        var now = clock.GetUtcNow().UtcDateTime;
        var subject = string.IsNullOrWhiteSpace(email.Subject) ? "(no subject)" : email.Subject.Trim();
        var body = StripQuotedReply(email.Body);
        if (body.Length == 0) body = subject;

        if (user is null)
        {
            // New contact at a known client: create a user record so the ticket has an owner. They have no
            // password, so they can't sign in until an admin invites them.
            user = new User { Email = from, DisplayName = from[..from.IndexOf('@')], Role = UserRole.ClientStaff, ClientId = client.Id };
            db.Users.Add(user);
        }

        // Reply to an existing ticket? e.g. "RE: [#42] Printer offline"
        var match = TicketRefRegex().Match(subject);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var ticketId))
        {
            var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.CreatedById == user.Id, ct);
            if (ticket is not null && ticket.Status != TicketStatus.Closed)
            {
                ticket.Comments.Add(new Comment { Author = user, Body = body, CreatedAt = now });
                ticket.UpdatedAt = now;
                if (ticket.Status == TicketStatus.Waiting)
                    TicketWorkflow.Transition(ticket, TicketStatus.InProgress, user, now);
                await db.SaveChangesAsync(ct);
                return new InboundEmailResult(InboundEmailOutcome.CommentAdded, ticket.Id, $"Added reply to ticket #{ticket.Id}.");
            }
        }

        var newTicket = new Ticket
        {
            Title = Truncate(subject, 200),
            Description = Truncate(body, 8000),
            Source = TicketSource.Email,
            Priority = TicketPriority.Medium, // AI triage will suggest a better one for an agent to approve
            Category = TicketCategory.Other,
            ClientId = client.Id,
            CreatedBy = user,
            CreatedAt = now,
            UpdatedAt = now,
        };
        SlaPolicy.ApplyDeadlines(newTicket);
        db.Tickets.Add(newTicket);
        await db.SaveChangesAsync(ct);
        triageQueue.Enqueue(newTicket.Id);

        logger.LogInformation("Created ticket {TicketId} from email by {From}", newTicket.Id, from);
        return new InboundEmailResult(InboundEmailOutcome.TicketCreated, newTicket.Id, $"Created ticket #{newTicket.Id}.");
    }

    private InboundEmailResult Reject(string reason)
    {
        logger.LogInformation("Inbound email rejected: {Reason}", reason);
        return new InboundEmailResult(InboundEmailOutcome.Rejected, null, reason);
    }

    /// <summary>"Olivia Brown &lt;Olivia@Northwind.test&gt;" → "olivia@northwind.test".</summary>
    internal static string? ExtractAddress(string from)
    {
        var m = EmailRegex().Match(from ?? "");
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    /// <summary>Drops the quoted earlier conversation from replies ("On ... wrote:" and "&gt;" lines).</summary>
    internal static string StripQuotedReply(string body)
    {
        var lines = (body ?? "").Replace("\r\n", "\n").Split('\n');
        var kept = new List<string>();
        foreach (var line in lines)
        {
            if (QuoteHeaderRegex().IsMatch(line) || line.StartsWith("-----Original Message", StringComparison.OrdinalIgnoreCase))
                break;
            if (!line.TrimStart().StartsWith('>'))
                kept.Add(line);
        }
        return string.Join('\n', kept).Trim();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\[#(\d+)\]")]
    private static partial Regex TicketRefRegex();

    [GeneratedRegex(@"^On .+wrote:\s*$")]
    private static partial Regex QuoteHeaderRegex();
}
