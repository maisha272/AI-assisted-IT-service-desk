import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import {
  TICKET_CATEGORIES,
  TICKET_PRIORITIES,
  type Client,
  type TicketCategory,
  type TicketPriority,
} from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { categoryLabel } from '../utils/format'

export default function NewTicketPage() {
  const navigate = useNavigate()
  const { isAgent } = useAuth()
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [priority, setPriority] = useState<TicketPriority>('Medium')
  const [category, setCategory] = useState<TicketCategory>('Other')
  const [clients, setClients] = useState<Client[]>([])
  const [clientId, setClientId] = useState<number | ''>('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (isAgent) api.listClients().then(setClients).catch(() => setClients([]))
  }, [isAgent])

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const ticket = await api.createTicket({
        title,
        description,
        priority,
        category,
        clientId: clientId === '' ? undefined : clientId,
      })
      navigate(`/tickets/${ticket.id}`)
    } catch (err) {
      setError((err as Error).message)
      setBusy(false)
    }
  }

  return (
    <section className="narrow">
      <div>
        <Link className="back-link" to="/tickets">
          ← All tickets
        </Link>
      </div>
      <div className="page-header">
        <div>
          <h1>New ticket</h1>
          <p className="muted">
            Log an IT issue or service request. AI triage will automatically suggest classification and troubleshooting
            steps for agent review.
          </p>
        </div>
      </div>

      <form className="card form" onSubmit={submit}>
        {isAgent && (
          <label>
            Client
            <select required value={clientId} onChange={(e) => setClientId(e.target.value ? Number(e.target.value) : '')}>
              <option value="">Choose a client…</option>
              {clients.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>
        )}
        <label>
          Title
          <input
            required
            minLength={5}
            maxLength={200}
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Short summary, e.g. “Outlook keeps asking for my password”"
          />
        </label>
        <label>
          Description
          <textarea
            required
            minLength={10}
            rows={6}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="What happened, when it started, who is affected, any error messages…"
          />
        </label>
        <div className="form-row">
          <label>
            Priority
            <select value={priority} onChange={(e) => setPriority(e.target.value as TicketPriority)}>
              {TICKET_PRIORITIES.map((p) => (
                <option key={p}>{p}</option>
              ))}
            </select>
          </label>
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
        </div>
        {error && <p className="error">{error}</p>}
        <div className="triage-actions">
          <button className="button primary" disabled={busy}>
            {busy ? 'Submitting…' : 'Submit ticket'}
          </button>
          <Link className="button" to="/tickets">
            Cancel
          </Link>
        </div>
      </form>
    </section>
  )
}
