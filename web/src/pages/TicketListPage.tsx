import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type TicketFilters } from '../api/client'
import { TICKET_PRIORITIES, TICKET_STATUSES, type TicketSummary } from '../api/types'
import { PriorityBadge, StatusBadge } from '../components/Badges'
import { categoryLabel, formatDateTime, statusLabel } from '../utils/format'

export default function TicketListPage() {
  const [filters, setFilters] = useState<TicketFilters>({})
  const [searchInput, setSearchInput] = useState('')
  const [tickets, setTickets] = useState<TicketSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    // `cancelled` stops a slow, older request from overwriting the results of a newer one.
    let cancelled = false
    api
      .listTickets(filters)
      .then((data) => !cancelled && setTickets(data))
      .catch((e: Error) => !cancelled && setError(e.message))
    return () => {
      cancelled = true
    }
  }, [filters])

  // Debounce the search box so we don't call the API on every keystroke.
  useEffect(() => {
    const handle = setTimeout(() => setFilters((f) => ({ ...f, search: searchInput || undefined })), 300)
    return () => clearTimeout(handle)
  }, [searchInput])

  return (
    <section>
      <div className="page-header">
        <h1>Tickets</h1>
        <Link className="button primary" to="/tickets/new">
          New ticket
        </Link>
      </div>

      <div className="filters">
        <input
          type="search"
          placeholder="Search title or description"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          aria-label="Search tickets"
        />
        <select
          aria-label="Filter by status"
          value={filters.status ?? ''}
          onChange={(e) => setFilters((f) => ({ ...f, status: (e.target.value || undefined) as TicketFilters['status'] }))}
        >
          <option value="">All statuses</option>
          {TICKET_STATUSES.map((s) => (
            <option key={s} value={s}>
              {statusLabel(s)}
            </option>
          ))}
        </select>
        <select
          aria-label="Filter by priority"
          value={filters.priority ?? ''}
          onChange={(e) =>
            setFilters((f) => ({ ...f, priority: (e.target.value || undefined) as TicketFilters['priority'] }))
          }
        >
          <option value="">All priorities</option>
          {TICKET_PRIORITIES.map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </select>
      </div>

      {error && <p className="error">{error}</p>}
      {!tickets && !error && <p className="muted">Loading tickets…</p>}
      {tickets && tickets.length === 0 && <p className="muted">No tickets match these filters.</p>}

      {tickets && tickets.length > 0 && (
        <div className="card table-card">
          <table className="table">
            <thead>
              <tr>
                <th>#</th>
                <th>Title</th>
                <th>Client</th>
                <th>Category</th>
                <th>Priority</th>
                <th>Status</th>
                <th>Assignee</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {tickets.map((t) => (
                <tr key={t.id}>
                  <td className="muted">{t.id}</td>
                  <td>
                    <Link to={`/tickets/${t.id}`}>{t.title}</Link>
                  </td>
                  <td>{t.clientName}</td>
                  <td>{categoryLabel(t.category)}</td>
                  <td>
                    <PriorityBadge priority={t.priority} />
                  </td>
                  <td>
                    <StatusBadge status={t.status} />
                  </td>
                  <td>{t.assignedToName ?? <span className="muted">Unassigned</span>}</td>
                  <td className="nowrap">{formatDateTime(t.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
