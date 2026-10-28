using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services.Auth;
using ServiceDesk.Api.Services.Integrations;

namespace ServiceDesk.Api.Controllers;

public record InboundEmailRequest(
    [Required, StringLength(320)] string From,
    [StringLength(500)] string? Subject,
    [StringLength(20000)] string? Body);

public record IntegrationStatusDto(bool TeamsConfigured, bool InboundEmailEnabled, int DispatchIntervalSeconds);

public record NotificationLogDto(int Id, string Channel, NotificationEvent Event, int? TicketId, string Message, bool Delivered, string? Error, DateTime CreatedAt);

[ApiController]
[Route("api/integrations")]
public class IntegrationsController(
    AppDbContext db,
    InboundEmailService inboundEmail,
    NotificationDispatcher dispatcher,
    ITeamsNotifier teams,
    IOptions<IntegrationOptions> options) : ControllerBase
{
    public const string SecretHeader = "X-Webhook-Secret";

    /// <summary>
    /// Public webhook for an email provider (e.g. SendGrid Inbound Parse, Mailgun routes, or a Power Automate
    /// flow on a Microsoft 365 shared mailbox) to post emails sent to the support address.
    /// Authenticated by a shared secret header instead of a user login.
    /// </summary>
    [HttpPost("email/inbound")]
    [AllowAnonymous]
    public async Task<ActionResult<InboundEmailResult>> InboundEmail(InboundEmailRequest request, CancellationToken ct)
    {
        var expected = options.Value.InboundEmailSecret;
        if (string.IsNullOrWhiteSpace(expected))
            return Problem("Inbound email is not enabled.", statusCode: StatusCodes.Status404NotFound);
        if (!SecretMatches(Request.Headers[SecretHeader].ToString(), expected))
            return Problem("Invalid webhook secret.", statusCode: StatusCodes.Status401Unauthorized);

        return ToResult(await inboundEmail.ProcessAsync(new InboundEmail(request.From, request.Subject ?? "", request.Body ?? ""), ct));
    }

    /// <summary>Agent-only demo helper: runs the same inbound email pipeline without the shared secret.</summary>
    [HttpPost("email/simulate")]
    [Authorize(Roles = Roles.Agent)]
    public async Task<ActionResult<InboundEmailResult>> SimulateEmail(InboundEmailRequest request, CancellationToken ct) =>
        ToResult(await inboundEmail.ProcessAsync(new InboundEmail(request.From, request.Subject ?? "", request.Body ?? ""), ct));

    [HttpGet("status")]
    [Authorize(Roles = Roles.Agent)]
    public IntegrationStatusDto Status() => new(
        teams.IsConfigured,
        !string.IsNullOrWhiteSpace(options.Value.InboundEmailSecret),
        options.Value.DispatchIntervalSeconds);

    [HttpGet("notifications")]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IEnumerable<NotificationLogDto>> Notifications(CancellationToken ct) =>
        await db.NotificationLogs
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(50)
            .Select(n => new NotificationLogDto(n.Id, n.Channel, n.Event, n.TicketId, n.Message, n.Delivered, n.Error, n.CreatedAt))
            .ToListAsync(ct);

    /// <summary>Runs the notification dispatcher now instead of waiting for the next timer tick.</summary>
    [HttpPost("notifications/dispatch")]
    [Authorize(Roles = Roles.Agent)]
    public async Task<object> Dispatch(CancellationToken ct) => new { attempted = await dispatcher.RunOnceAsync(ct) };

    [HttpPost("notifications/test")]
    [Authorize(Roles = Roles.Agent)]
    public async Task<NotificationLogDto> SendTest(CancellationToken ct)
    {
        var n = await dispatcher.SendTestAsync(ct);
        return new NotificationLogDto(n.Id, n.Channel, n.Event, n.TicketId, n.Message, n.Delivered, n.Error, n.CreatedAt);
    }

    private ActionResult<InboundEmailResult> ToResult(InboundEmailResult result) =>
        result.Outcome == InboundEmailOutcome.Rejected
            ? UnprocessableEntity(result)
            : Ok(result);

    /// <summary>Constant-time comparison, so response timing doesn't leak how much of the secret was right.</summary>
    private static bool SecretMatches(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
}
