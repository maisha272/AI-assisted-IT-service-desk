using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services.Triage;

/// <summary>
/// In-memory queue of ticket ids waiting for AI triage. Creating a ticket just enqueues its id and
/// returns immediately — the user never waits several seconds for an LLM call.
/// </summary>
public class TriageQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public void Enqueue(int ticketId) => _channel.Writer.TryWrite(ticketId);

    public IAsyncEnumerable<int> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>Background worker that processes the triage queue one ticket at a time.</summary>
public class TriageWorker(TriageQueue queue, IServiceScopeFactory scopes, IOptions<AiOptions> options, ILogger<TriageWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.TriageOpenTicketsOnStartup)
            await QueueUntriagedOpenTicketsAsync(stoppingToken);

        await foreach (var ticketId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                // DbContext is scoped (one per request); a long-running worker creates a scope per job.
                using var scope = scopes.CreateScope();
                var triage = scope.ServiceProvider.GetRequiredService<TriageService>();
                await triage.RunAsync(ticketId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // One failed AI call must not stop the worker. The agent can re-run triage from the UI.
                logger.LogWarning(ex, "AI triage failed for ticket {TicketId}", ticketId);
            }
        }
    }

    private async Task QueueUntriagedOpenTicketsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ids = await db.Tickets.Open()
                .Where(t => !db.TriageSuggestions.Any(s => s.TicketId == t.Id))
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => t.Id)
                .Take(25)
                .ToListAsync(ct);
            foreach (var id in ids) queue.Enqueue(id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not queue untriaged tickets on startup");
        }
    }
}
