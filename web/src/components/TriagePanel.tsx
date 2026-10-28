import { useEffect, useState } from 'react'
import { api } from '../api/client'
import {
  TICKET_CATEGORIES,
  TICKET_PRIORITIES,
  type TicketCategory,
  type TicketPriority,
  type TriageSuggestion,
} from '../api/types'
import { categoryLabel, formatDateTime } from '../utils/format'

interface Props {
  ticketId: number
  /** Called after approval so the page can reload the ticket (category, priority, SLA, notes changed). */
  onApproved: () => void
}

/**
 * Human-in-the-loop review of the AI triage suggestion. The agent can approve as-is, edit any field
 * before approving, or reject. Nothing is applied to the ticket until they approve.
 */
export default function TriagePanel({ ticketId, onApproved }: Props) {
  const [suggestion, setSuggestion] = useState<TriageSuggestion | null | undefined>(undefined)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  // Editable copy of the suggestion.
  const [category, setCategory] = useState<TicketCategory>('Other')
  const [priority, setPriority] = useState<TicketPriority>('Medium')
  const [summary, setSummary] = useState('')
  const [steps, setSteps] = useState('')

  function load(s: TriageSuggestion | null) {
    setSuggestion(s)
    if (s) {
      setCategory(s.suggestedCategory)
      setPriority(s.suggestedPriority)
      setSummary(s.summary)
      setSteps(s.steps.join('\n'))
    }
  }

  useEffect(() => {
    let cancelled = false
    let attempts = 0
    // Triage runs in the background after a ticket is created, so poll briefly until it appears.
    async function poll() {
      try {
        const s = await api.getTriage(ticketId)
        if (cancelled) return
        if (s || attempts >= 5) load(s ?? null)
        else {
          attempts++
          setTimeout(poll, 1500)
        }
      } catch (e) {
        if (!cancelled) setError((e as Error).message)
      }
    }
    void poll()
    return () => {
      cancelled = true
    }
  }, [ticketId])

  async function run(action: () => Promise<TriageSuggestion>, after?: () => void) {
    setBusy(true)
    setError(null)
    try {
      load(await action())
      after?.()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const approve = () =>
    run(
      () =>
        api.approveTriage(suggestion!.id, {
          category,
          priority,
          summary,
          steps: steps.split('\n').map((s) => s.trim()).filter(Boolean),
        }),
      onApproved,
    )

  const reject = () => {
    const reason = window.prompt('Why are you rejecting this suggestion? (optional)') ?? undefined
    void run(() => api.rejectTriage(suggestion!.id, reason))
  }

  const rerun = () => run(() => api.runTriage(ticketId))

  return (
    <section className="card triage-panel" aria-label="AI triage suggestion">
      <div className="triage-header">
        <h3>
          <span className="ai-mark" aria-hidden>
            ✦
          </span>{' '}
          AI triage
        </h3>
        {suggestion && <span className="tag">{suggestion.provider}</span>}
      </div>
      {error && <p className="error">{error}</p>}

      {suggestion === undefined && <p className="muted">Looking for a suggestion…</p>}

      {suggestion === null && (
        <div>
          <p className="muted">No suggestion yet.</p>
          <button className="button" disabled={busy} onClick={rerun}>
            Run triage now
          </button>
        </div>
      )}

      {suggestion && suggestion.status === 'Pending' && (
        <>
          <p className="muted small">
            Suggested {formatDateTime(suggestion.createdAt)} · confidence <strong>{suggestion.confidence}</strong>. Review and edit
            before approving — nothing changes on the ticket until you do.
          </p>
          {suggestion.rationale && <p className="rationale">“{suggestion.rationale}”</p>}

          <div className="form-row">
            <label>
              Category
              <select value={category} onChange={(e) => setCategory(e.target.value as TicketCategory)}>
                {TICKET_CATEGORIES.map((c) => (
                  <option key={c} value={c}>
                    {categoryLabel(c)}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Priority
              <select value={priority} onChange={(e) => setPriority(e.target.value as TicketPriority)}>
                {TICKET_PRIORITIES.map((p) => (
                  <option key={p}>{p}</option>
                ))}
              </select>
            </label>
          </div>
          <label>
            Summary
            <input value={summary} maxLength={300} onChange={(e) => setSummary(e.target.value)} />
          </label>
          <label>
            Suggested steps (one per line)
            <textarea rows={Math.min(8, Math.max(3, steps.split('\n').length))} value={steps} onChange={(e) => setSteps(e.target.value)} />
          </label>

          {suggestion.relatedArticles.length > 0 && (
            <div className="related">
              <div className="muted small">Based on similar past fixes:</div>
              <ul>
                {suggestion.relatedArticles.map((a) => (
                  <li key={a.id}>
                    {a.title} <span className="muted small">({Math.round(a.score * 100)}% match)</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          <div className="triage-actions">
            <button className="button primary" disabled={busy || !summary.trim()} onClick={approve}>
              Approve
            </button>
            <button className="button danger" disabled={busy} onClick={reject}>
              Reject
            </button>
            <button className="button" disabled={busy} onClick={rerun}>
              Re-run
            </button>
          </div>
        </>
      )}

      {suggestion && suggestion.status !== 'Pending' && (
        <div>
          <p>
            <span className={`badge ${suggestion.status === 'Approved' ? 'sla-met' : 'sla-breached'}`}>{suggestion.status}</span>{' '}
            by {suggestion.reviewedByName} {suggestion.reviewedAt && formatDateTime(suggestion.reviewedAt)}
            {suggestion.status === 'Approved' && (suggestion.wasEdited ? ' (with edits)' : ' (as suggested)')}
          </p>
          {suggestion.rejectionReason && <p className="muted">Reason: {suggestion.rejectionReason}</p>}
          <button className="button" disabled={busy} onClick={rerun}>
            Run triage again
          </button>
        </div>
      )}
    </section>
  )
}
