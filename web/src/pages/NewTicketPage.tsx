import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import {
  TICKET_CATEGORIES,
  TICKET_PRIORITIES,
  type Client,
  type TicketCategory,
  type TicketPriority,
} from '../api/types'
import { categoryLabel } from '../utils/format'

// Placeholder until authentication is added: the first slice treats everyone as an agent.
const isAgent = true

export default function NewTicketPage() {
  const navigate = useNavigate()
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
  }, [])

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
      <h1>New ticket</h1>
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
        <button className="button primary" disabled={busy}>
          {busy ? 'Submitting…' : 'Submit ticket'}
        </button>
      </form>
    </section>
  )
}
