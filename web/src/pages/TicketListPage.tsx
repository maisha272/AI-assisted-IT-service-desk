import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api, type TicketFilters } from '../api/client'
import { TICKET_PRIORITIES, TICKET_STATUSES, type TicketSummary } from '../api/types'
import { SlaBadge } from '../components/SlaBadge'
import { activeDeadline, isOpen } from '../utils/sla'
import { useAuth } from '../auth/AuthContext'
import { PriorityBadge, StatusBadge } from '../components/Badges'
import { categoryLabel, formatDateTime, statusLabel } from '../utils/format'

export default function TicketListPage() {
  const { isAgent } = useAuth()
  // Initial filters can come from the URL, e.g. /tickets?sla=Breached (linked from the dashboard).
  const [searchParams] = useSearchParams()
  const [filters, setFilters] = useState<TicketFilters>(() => ({
    status: (searchParams.get('status') ?? undefined) as TicketFilters['status'],
    open: searchParams.get('open') === 'true' || undefined,
    priority: (searchParams.get('priority') ?? undefined) as TicketFilters['priority'],
    sla: (searchParams.get('sla') ?? undefined) as TicketFilters['sla'],
  }))
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
        <h1>{isAgent ? 'All tickets' : 'My tickets'}</h1>
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
          value={filters.open ? 'open' : (filters.status ?? '')}
          onChange={(e) => {
            const value = e.target.value
            setFilters((f) => ({
              ...f,
              open: value === 'open' || undefined,
              status: value && value !== 'open' ? (value as TicketFilters['status']) : undefined,
            }))
          }}
        >
          <option value="">All statuses</option>
          <option value="open">All open</option>
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
        <select
          aria-label="Filter by SLA"
          value={filters.sla ?? ''}
          onChange={(e) => setFilters((f) => ({ ...f, sla: (e.target.value || undefined) as TicketFilters['sla'] }))}
        >
          <option value="">Any SLA</option>
          <option value="Breached">Breached</option>
          <option value="AtRisk">At risk</option>
          <option value="OnTrack">On track</option>
          <option value="Met">Met</option>
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
                {isAgent && <th>Client</th>}
                <th>Category</th>
                <th>Priority</th>
                <th>Status</th>
                <th>SLA</th>
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
                  {isAgent && <td>{t.clientName}</td>}
                  <td>{categoryLabel(t.category)}</td>
                  <td>
                    <PriorityBadge priority={t.priority} />
                  </td>
                  <td>
                    <StatusBadge status={t.status} />
                  </td>
                  <td>
                    <SlaBadge state={t.sla.overall} dueAt={isOpen(t.status) ? activeDeadline(t.sla) : undefined} />
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
