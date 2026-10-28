using System.Text.RegularExpressions;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services.Triage;

/// <summary>
/// Keyword rules that imitate an LLM's output shape, so the whole triage flow (retrieval, suggestion,
/// human approval) works with no API key. Deterministic, which also makes it handy in tests.
/// </summary>
public class MockTriageModel : ITriageModel
{
    public string Name => "mock";

    private static readonly Dictionary<TicketCategory, string[]> CategoryKeywords = new()
    {
        [TicketCategory.Security] = ["phishing", "suspicious", "virus", "malware", "ransomware", "hacked", "clicked a link", "antivirus", "defender", "threat", "scam", "strange extension", "asking for payment"],
        [TicketCategory.AccountAccess] = ["locked out", "password reset", "mfa", "authenticator", "new starter", "offboard", "access to", "permission", "account", "login", "sign in", "log in"],
        [TicketCategory.Email] = ["email", "outlook", "mailbox", "inbox", "bounce", "shared mailbox", "calendar"],
        [TicketCategory.Network] = ["internet", "wifi", "wi-fi", "vpn", "network", "shared drive", "router", "connection", "offline for everyone", "slow connection"],
        [TicketCategory.Printing] = ["printer", "print", "scan", "copier", "toner", "paper jam", "jam"],
        [TicketCategory.Hardware] = ["laptop", "monitor", "keyboard", "mouse", "battery", "screen", "dock", "power on", "headset", "phone", "hardware"],
        [TicketCategory.Software] = ["excel", "word", "teams", "install", "licence", "license", "crash", "freez", "software", "app", "update", "sync"],
    };

    private static readonly string[] CriticalSignals =
        ["ransomware", "everyone", "whole office", "all users", "no one", "nobody", "entire", "outage", "clicked a link", "entered my password", "asking for payment", "phones are also down"];

    private static readonly string[] HighSignals =
        ["locked out", "can't log in", "cannot log in", "can't work", "cannot work", "urgent", "asap", "deadline", "client meeting", "meetings this afternoon", "month end", "not receiving", "won't turn on", "won't power", "phishing", "suspicious", "vpn", "no licences", "threat"];

    private static readonly string[] LowSignals =
        ["request", "question", "when you have time", "no rush", "install", "new phone", "checklist", "how do i", "approved"];

    public Task<RawTriage> SuggestAsync(TriageInput input, CancellationToken ct = default)
    {
        var text = $"{input.Title} {input.Description}".ToLowerInvariant();
        var best = input.Articles.FirstOrDefault();

        var (category, categoryReason) = PickCategory(text, best);
        var (priority, priorityReason) = PickPriority(text);

        var steps = best is not null
            ? StepsFrom(best)
            : GenericSteps(category);

        var summary = $"{Label(category)} issue for {input.ClientName}: {FirstSentence(input.Description)}";
        var confidence = best is { Score: >= 0.45 } ? "high" : best is not null ? "medium" : "low";

        return Task.FromResult(new RawTriage(
            category.ToString(),
            priority.ToString(),
            summary,
            steps,
            best is null ? [] : [best.Id],
            confidence,
            $"{categoryReason} {priorityReason}".Trim()));
    }

    private static (TicketCategory, string) PickCategory(string text, RetrievedArticle? best)
    {
        var scores = CategoryKeywords
            .Select(kv => (Category: kv.Key, Hits: kv.Value.Where(k => Mentions(text, k)).ToList()))
            .Where(x => x.Hits.Count > 0)
            .OrderByDescending(x => x.Hits.Count)
            .ToList();

        if (scores.Count > 0)
            return (scores[0].Category, $"Mentions {string.Join(", ", scores[0].Hits.Take(3).Select(h => $"\"{h}\""))}.");
        if (best is not null)
            return (best.Category, $"Similar to past fix \"{best.Title}\".");
        return (TicketCategory.Other, "No clear category signals.");
    }

    private static (TicketPriority, string) PickPriority(string text)
    {
        var critical = CriticalSignals.FirstOrDefault(k => Mentions(text, k));
        if (critical is not null) return (TicketPriority.Critical, $"Possible wide impact or security incident (\"{critical}\").");

        var high = HighSignals.FirstOrDefault(k => Mentions(text, k));
        if (high is not null) return (TicketPriority.High, $"User appears blocked or time-sensitive (\"{high}\").");

        var low = LowSignals.FirstOrDefault(k => Mentions(text, k));
        if (low is not null) return (TicketPriority.Low, $"Looks like a request or question (\"{low}\").");

        return (TicketPriority.Medium, "Single user affected, no urgency signals.");
    }

    private static List<string> StepsFrom(RetrievedArticle article)
    {
        var steps = new List<string>
        {
            "Confirm the symptoms and how many users are affected.",
            $"Check whether the past fix \"{article.Title}\" applies. Last time:",
        };
        steps.AddRange(article.Resolution
            .Split(". ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.TrimEnd('.') + "."));
        steps.Add("Confirm with the user that the issue is resolved before closing.");
        return steps;
    }

    private static List<string> GenericSteps(TicketCategory category) => category switch
    {
        TicketCategory.Security =>
        [
            "Treat as a potential security incident; do not let the user continue using the affected device or account.",
            "Reset credentials and revoke active sessions if any password may have been exposed.",
            "Check sign-in logs and mailbox rules for suspicious activity.",
            "Escalate to the security lead if there is evidence of compromise.",
        ],
        TicketCategory.Network =>
        [
            "Check whether other users or sites are affected.",
            "Check router/firewall status and the ISP status page.",
            "Restart affected network equipment if safe to do so.",
        ],
        _ =>
        [
            "Contact the user to confirm symptoms and when the issue started.",
            "Connect remotely to reproduce the issue and capture any error messages.",
            "Search the knowledge base and vendor documentation for the error.",
        ],
    };

    private static string FirstSentence(string text)
    {
        var trimmed = text.Trim();
        var end = trimmed.IndexOfAny(['.', '!', '?', '\n']);
        var sentence = end > 0 ? trimmed[..(end + 1)] : trimmed;
        return sentence.Length > 160 ? sentence[..157] + "…" : sentence;
    }

    /// <summary>
    /// Keyword match at the start of a word, so "app" matches "app"/"application" but not "happening",
    /// and "freez" matches "freezes"/"freezing".
    /// </summary>
    internal static bool Mentions(string text, string keyword) =>
        Regex.IsMatch(text, @"\b" + Regex.Escape(keyword), RegexOptions.CultureInvariant);

    private static string Label(TicketCategory c) => c == TicketCategory.AccountAccess ? "Account & access" : c.ToString();
}
