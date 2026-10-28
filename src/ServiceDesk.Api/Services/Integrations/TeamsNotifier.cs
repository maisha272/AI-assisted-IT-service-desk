using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services.Integrations;

public record TeamsMessage(string Title, string Text, IReadOnlyList<(string Name, string Value)> Facts, string? LinkUrl, string Style = "default");

/// <summary>Sends messages to a Microsoft Teams channel through an incoming webhook.</summary>
public interface ITeamsNotifier
{
    bool IsConfigured { get; }

    /// <summary>Returns null on success, otherwise the reason it failed.</summary>
    Task<string?> SendAsync(TeamsMessage message, CancellationToken ct = default);
}

/// <summary>
/// Posts an Adaptive Card to a Teams "Workflows" webhook ("Post to a channel when a webhook request is
/// received"). Adaptive Cards are Microsoft's JSON format for rich cards in Teams/Outlook.
/// </summary>
public class TeamsWebhookNotifier(HttpClient http, IOptions<IntegrationOptions> options, ILogger<TeamsWebhookNotifier> logger)
    : ITeamsNotifier
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.TeamsWebhookUrl);

    public async Task<string?> SendAsync(TeamsMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured) return "Webhook not configured (logged only)";

        try
        {
            using var response = await http.PostAsJsonAsync(options.Value.TeamsWebhookUrl, BuildPayload(message), ct);
            if (response.IsSuccessStatusCode) return null;
            return $"Teams returned {(int)response.StatusCode}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Teams webhook call failed");
            return $"Teams webhook call failed: {ex.Message}";
        }
    }

    internal static JsonObject BuildPayload(TeamsMessage message)
    {
        var body = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "TextBlock", ["text"] = message.Title, ["weight"] = "Bolder", ["size"] = "Medium",
                ["wrap"] = true, ["color"] = message.Style == "attention" ? "Attention" : message.Style == "warning" ? "Warning" : "Default",
            },
            new JsonObject { ["type"] = "TextBlock", ["text"] = message.Text, ["wrap"] = true },
            new JsonObject
            {
                ["type"] = "FactSet",
                ["facts"] = new JsonArray(message.Facts
                    .Select(f => (JsonNode)new JsonObject { ["title"] = f.Name, ["value"] = f.Value }).ToArray()),
            },
        };

        var card = new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.4",
            ["body"] = body,
        };
        if (message.LinkUrl is not null)
            card["actions"] = new JsonArray(new JsonObject { ["type"] = "Action.OpenUrl", ["title"] = "Open ticket", ["url"] = message.LinkUrl });

        return new JsonObject
        {
            ["type"] = "message",
            ["attachments"] = new JsonArray(new JsonObject
            {
                ["contentType"] = "application/vnd.microsoft.card.adaptive",
                ["content"] = card,
            }),
        };
    }

    public static TeamsMessage ForTicket(Ticket ticket, NotificationEvent evt, string publicUrl, DateTime now)
    {
        var (title, style) = evt switch
        {
            NotificationEvent.SlaBreached => ($"SLA breached: #{ticket.Id} {ticket.Title}", "attention"),
            NotificationEvent.SlaAtRisk => ($"SLA at risk: #{ticket.Id} {ticket.Title}", "warning"),
            _ => ($"New ticket: #{ticket.Id} {ticket.Title}", "default"),
        };
        var due = ticket.FirstRespondedAt is null ? ticket.ResponseDueAt : ticket.ResolutionDueAt;
        var dueLabel = ticket.FirstRespondedAt is null ? "Response due" : "Resolution due";
        var minutes = (int)Math.Round((due - now).TotalMinutes);
        var dueText = minutes >= 0 ? $"in {minutes} min" : $"{-minutes} min overdue";

        var description = ticket.Description.Length > 300 ? ticket.Description[..297] + "…" : ticket.Description;
        return new TeamsMessage(title, description,
        [
            ("Client", ticket.Client?.Name ?? ""),
            ("Priority", ticket.Priority.ToString()),
            ("Status", ticket.Status.ToString()),
            ("Assignee", ticket.AssignedTo?.DisplayName ?? "Unassigned"),
            (dueLabel, $"{due:yyyy-MM-dd HH:mm} UTC ({dueText})"),
        ], $"{publicUrl.TrimEnd('/')}/tickets/{ticket.Id}", style);
    }
}
