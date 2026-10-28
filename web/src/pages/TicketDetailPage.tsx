import { useEffect, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api/client'
import {
  TICKET_CATEGORIES,
  TICKET_PRIORITIES,
  type Agent,
  type TicketCategory,
  type TicketDetail,
  type TicketPriority,
  type TicketStatus,
} from '../api/types'
import { PriorityBadge, StatusBadge } from '../components/Badges'
import { categoryLabel, formatDateTime, statusLabel } from '../utils/format'

// Placeholder until authentication is added: the first slice treats everyone as an agent.
const isAgent = true

export default function TicketDetailPage() {
  const id = Number(useParams().id)
  const [ticket, setTicket] = useState<TicketDetail | null>(null)
  const [agents, setAgents] = useState<Agent[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [commentBody, setCommentBody] = useState('')
  const [isInternal, setIsInternal] = useState(false)

  useEffect(() => {
    api.getTicket(id).then(setTicket).catch((e: Error) => setError(e.message))
    if (isAgent) api.listAgents().then(setAgents).catch(() => setAgents([]))
  }, [id])

  /** Runs an API call that returns the updated ticket, with shared busy/error handling. */
  async function run(action: () => Promise<TicketDetail>) {
    setBusy(true)
    setError(null)
    try {
      setTicket(await action())
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  async function submitComment(e: FormEvent) {
    e.preventDefault()
    if (!commentBody.trim()) return
    await run(() => api.addComment(id, commentBody, isInternal))
    setCommentBody('')
    setIsInternal(false)
  }

  if (!ticket) return error ? <p className="error">{error}</p> : <p className="muted">Loading ticket…</p>

  return (
    <section className="ticket-detail">
      <p>
        <Link to="/tickets">← All tickets</Link>
      </p>
      <div className="page-header">
        <h1>
          <span className="muted">#{ticket.id}</span> {ticket.title}
        </h1>
        <div className="badges">
          <PriorityBadge priority={ticket.priority} />
          <StatusBadge status={ticket.status} />
        </div>
      </div>
      {error && <p className="error">{error}</p>}

      <div className="detail-grid">
        <div className="detail-main">
          <div className="card">
            <p className="meta">
              Raised by <strong>{ticket.createdByName}</strong> ({ticket.clientName}) on{' '}
              {formatDateTime(ticket.createdAt)} via {ticket.source === 'Email' ? 'email' : 'the portal'}
            </p>
            <p className="description">{ticket.description}</p>
          </div>

          <h2>Conversation</h2>
          {ticket.comments.length === 0 && <p className="muted">No comments yet.</p>}
          <ol className="comments">
            {ticket.comments.map((c) => (
              <li key={c.id} className={`comment ${c.isInternal ? 'internal' : ''} ${c.authorRole === 'Agent' ? 'from-agent' : ''}`}>
                <div className="comment-meta">
                  <strong>{c.authorName}</strong>
                  {c.authorRole === 'Agent' && <span className="tag">Agent</span>}
                  {c.isInternal && <span className="tag internal-tag">Internal note</span>}
                  <span className="muted">{formatDateTime(c.createdAt)}</span>
                </div>
                <p>{c.body}</p>
              </li>
            ))}
          </ol>

          {ticket.status !== 'Closed' && (
            <form className="card comment-form" onSubmit={submitComment}>
              <label htmlFor="comment">Add a comment</label>
              <textarea
                id="comment"
                rows={3}
                value={commentBody}
                onChange={(e) => setCommentBody(e.target.value)}
                placeholder="Write a reply…"
              />
              <div className="form-row">
                {isAgent && (
                  <label className="checkbox">
                    <input type="checkbox" checked={isInternal} onChange={(e) => setIsInternal(e.target.checked)} />
                    Internal note (hidden from client)
                  </label>
                )}
                <button className="button primary" disabled={busy || !commentBody.trim()}>
                  Post comment
                </button>
              </div>
            </form>
          )}
        </div>

        <aside className="detail-side">
          <div className="card">
            <h3>Status</h3>
            {ticket.allowedNextStatuses.length === 0 ? (
              <p className="muted">No further actions.</p>
            ) : (
              <div className="button-stack">
                {ticket.allowedNextStatuses.map((s: TicketStatus) => (
                  <button key={s} className="button" disabled={busy} onClick={() => run(() => api.changeStatus(id, s))}>
                    Move to {statusLabel(s)}
                  </button>
                ))}
              </div>
            )}
          </div>

          {isAgent && (
            <div className="card">
              <h3>Details</h3>
              <label>
                Priority
                <select
                  value={ticket.priority}
                  disabled={busy}
                  onChange={(e) => run(() => api.updateTicket(id, { priority: e.target.value as TicketPriority }))}
                >
                  {TICKET_PRIORITIES.map((p) => (
                    <option key={p}>{p}</option>
                  ))}
                </select>
              </label>
              <label>
                Category
                <select
                  value={ticket.category}
                  disabled={busy}
                  onChange={(e) => run(() => api.updateTicket(id, { category: e.target.value as TicketCategory }))}
                >
                  {TICKET_CATEGORIES.map((c) => (
                    <option key={c} value={c}>
                      {categoryLabel(c)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Assignee
                <select
                  value={ticket.assignedToId ?? ''}
                  disabled={busy}
                  onChange={(e) => run(() => api.assignTicket(id, e.target.value ? Number(e.target.value) : null))}
                >
                  <option value="">Unassigned</option>
                  {agents.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.displayName}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          )}

          <div className="card">
            <h3>History</h3>
            <ol className="history">
              <li>
                Created <span className="muted">{formatDateTime(ticket.createdAt)}</span>
              </li>
              {ticket.statusChanges.map((s, i) => (
                <li key={i}>
                  {statusLabel(s.fromStatus)} → {statusLabel(s.toStatus)} by {s.changedByName}{' '}
                  <span className="muted">{formatDateTime(s.changedAt)}</span>
                </li>
              ))}
            </ol>
          </div>
        </aside>
      </div>
    </section>
  )
}
