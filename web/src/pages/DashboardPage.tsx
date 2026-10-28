import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../api/client'
import type { Dashboard } from '../api/types'
import { PriorityBadge, StatusBadge } from '../components/Badges'
import { SlaBadge } from '../components/SlaBadge'
import { StatTile } from '../components/StatTile'
import { formatDuration, statusLabel } from '../utils/format'
import { activeDeadline } from '../utils/sla'

// Single-series charts use one brand hue; the title names the series, so no legend is needed.
const CHART_COLOR = '#2357d9'

export default function DashboardPage() {
  const [data, setData] = useState<Dashboard | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.getDashboard().then(setData).catch((e: Error) => setError(e.message))
  }, [])

  if (error) return <p className="error">{error}</p>
  if (!data) return <p className="muted">Loading dashboard…</p>

  return (
    <section>
      <div className="page-header">
        <h1>Dashboard</h1>
      </div>

      <div className="stat-grid">
        <StatTile label="Open tickets" value={data.openTickets} hint={`${data.unassignedTickets} unassigned`} />
        <StatTile
          label="SLA breached"
          value={data.breachedTickets}
          tone={data.breachedTickets > 0 ? 'critical' : undefined}
          hint="Open tickets past a deadline"
          to="/tickets?sla=Breached&open=true"
        />
        <StatTile
          label="Near breach"
          value={data.atRiskTickets}
          tone={data.atRiskTickets > 0 ? 'warning' : undefined}
          hint="Less than 25% of time left"
          to="/tickets?sla=AtRisk&open=true"
        />
        <StatTile
          label="AI suggestions to review"
          value={data.pendingTriageSuggestions}
          hint="Awaiting agent approval"
        />
        <StatTile
          label="Avg resolution time"
          value={data.averageResolutionMinutesLast30Days != null ? formatDuration(data.averageResolutionMinutesLast30Days) : '—'}
          hint={`${data.resolvedLast30Days} resolved in last 30 days`}
        />
        <StatTile
          label="SLA compliance"
          value={data.slaCompliancePercentLast30Days != null ? `${data.slaCompliancePercentLast30Days}%` : '—'}
          hint="Resolved within deadline, last 30 days"
        />
      </div>

      <div className="dashboard-grid">
        <div className="card">
          <h3>Open tickets by priority</h3>
          <div className="chart" role="img" aria-label="Bar chart of open tickets by priority">
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={data.openByPriority} margin={{ top: 8, right: 8, bottom: 0, left: -20 }}>
                <CartesianGrid vertical={false} stroke="#eceef2" />
                <XAxis dataKey="priority" tickLine={false} axisLine={{ stroke: '#d5d9e0' }} tick={{ fill: '#6a7282', fontSize: 13 }} />
                <YAxis allowDecimals={false} tickLine={false} axisLine={false} tick={{ fill: '#6a7282', fontSize: 12 }} />
                <Tooltip cursor={{ fill: 'rgba(35, 87, 217, 0.06)' }} formatter={(v: number) => [v, 'Open tickets']} />
                <Bar dataKey="count" fill={CHART_COLOR} radius={[4, 4, 0, 0]} maxBarSize={48} isAnimationActive={false} />
              </BarChart>
            </ResponsiveContainer>
          </div>
          {/* Same numbers as text, so the chart is never the only way to read them. */}
          <p className="muted small chart-summary">
            {data.openByPriority.map((p) => `${p.priority}: ${p.count}`).join(' · ')}
          </p>
        </div>

        <div className="card">
          <h3>Open tickets by status</h3>
          <ul className="status-counts">
            {data.openByStatus.map((s) => (
              <li key={s.status}>
                <Link to={`/tickets?status=${s.status}`}>
                  <StatusBadge status={s.status} />
                </Link>
                <strong>{s.count}</strong>
                <span className="sr-only">{statusLabel(s.status)}</span>
              </li>
            ))}
          </ul>
        </div>
      </div>

      <h2>Needs attention</h2>
      {data.needsAttention.length === 0 ? (
        <p className="muted">No tickets are near or past their SLA. Nice work.</p>
      ) : (
        <div className="card table-card">
          <table className="table">
            <thead>
              <tr>
                <th>#</th>
                <th>Title</th>
                <th>Client</th>
                <th>Priority</th>
                <th>Status</th>
                <th>SLA</th>
                <th>Assignee</th>
              </tr>
            </thead>
            <tbody>
              {data.needsAttention.map((t) => (
                <tr key={t.id}>
                  <td className="muted">{t.id}</td>
                  <td>
                    <Link to={`/tickets/${t.id}`}>{t.title}</Link>
                  </td>
                  <td>{t.clientName}</td>
                  <td>
                    <PriorityBadge priority={t.priority} />
                  </td>
                  <td>
                    <StatusBadge status={t.status} />
                  </td>
                  <td>
                    <SlaBadge state={t.sla.overall} dueAt={activeDeadline(t.sla)} />
                  </td>
                  <td>{t.assignedToName ?? <span className="muted">Unassigned</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
