import { useEffect, useState } from 'react'
import {
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  LineChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { api } from '../api/client'
import type { Report } from '../api/types'
import { StatTile } from '../components/StatTile'
import { categoryLabel } from '../utils/format'
import { downloadCsv, toCsv } from '../utils/csv'

// Single-series charts: one brand hue, the card title names the series, no legend needed.
const CHART_COLOR = '#2357d9'
const AXIS_TICK = { fill: '#6a7282', fontSize: 12 }
const RANGES = [30, 90, 180] as const
const SLA_TARGET_PERCENT = 90

const weekLabel = (iso: string) =>
  new Date(`${iso}T00:00:00Z`).toLocaleDateString('en-AU', { day: 'numeric', month: 'short', timeZone: 'UTC' })

export default function ReportsPage() {
  const [days, setDays] = useState<number>(90)
  const [report, setReport] = useState<Report | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    api
      .getReport(days)
      .then((r) => !cancelled && setReport(r))
      .catch((e: Error) => !cancelled && setError(e.message))
    return () => {
      cancelled = true
    }
  }, [days])

  function exportClients() {
    if (!report) return
    const csv = toCsv(
      ['Client', 'Tickets', 'Open', 'Resolved', 'Resolved within SLA', 'SLA compliance %'],
      report.byClient.map((c) => [c.clientName, c.total, c.open, c.resolved, c.resolvedWithinSla, c.compliancePercent]),
    )
    downloadCsv(`tickets-by-client-last-${days}-days.csv`, csv)
  }

  const categories = report?.byCategory.map((c) => ({ ...c, label: categoryLabel(c.category) })) ?? []
  const weeks = report?.slaComplianceByWeek.map((w) => ({ ...w, label: weekLabel(w.weekStart) })) ?? []

  return (
    <section>
      <div className="page-header">
        <h1>Reports</h1>
        <div className="segmented" role="group" aria-label="Date range">
          {RANGES.map((r) => (
            <button key={r} className={`button small ${days === r ? 'primary' : ''}`} onClick={() => setDays(r)} aria-pressed={days === r}>
              Last {r} days
            </button>
          ))}
        </div>
      </div>
      {error && <p className="error">{error}</p>}
      {!report && !error && <p className="muted">Loading report…</p>}

      {report && (
        <>
          <div className="stat-grid">
            <StatTile label="Tickets raised" value={report.totalTickets} hint={`Last ${days} days`} />
            <StatTile
              label="Resolution SLA compliance"
              value={report.overallCompliancePercent != null ? `${report.overallCompliancePercent}%` : '—'}
              hint={`Target ${SLA_TARGET_PERCENT}%`}
              tone={report.overallCompliancePercent != null && report.overallCompliancePercent < SLA_TARGET_PERCENT ? 'warning' : undefined}
            />
            <StatTile label="Clients with tickets" value={report.byClient.length} />
          </div>

          <div className="card">
            <h3>Resolution SLA compliance by week</h3>
            <div role="img" aria-label="Line chart of weekly SLA compliance percentage">
              <ResponsiveContainer width="100%" height={240}>
                <LineChart data={weeks} margin={{ top: 8, right: 16, bottom: 0, left: -12 }}>
                  <CartesianGrid vertical={false} stroke="#eceef2" />
                  <XAxis dataKey="label" tickLine={false} axisLine={{ stroke: '#d5d9e0' }} tick={AXIS_TICK} minTickGap={16} />
                  <YAxis domain={[0, 100]} tickFormatter={(v: number) => `${v}%`} tickLine={false} axisLine={false} tick={AXIS_TICK} />
                  <ReferenceLine y={SLA_TARGET_PERCENT} stroke="#9aa1ad" strokeDasharray="4 4" label={{ value: 'Target', position: 'insideTopRight', fill: '#6a7282', fontSize: 11 }} />
                  <Tooltip
                    formatter={(value, _name, item) => [
                      value == null ? 'No tickets resolved' : `${value}% (${item.payload.resolvedWithinSla}/${item.payload.resolved})`,
                      'Within SLA',
                    ]}
                    labelFormatter={(l: string) => `Week of ${l}`}
                  />
                  <Line
                    type="linear"
                    dataKey="compliancePercent"
                    stroke={CHART_COLOR}
                    strokeWidth={2}
                    dot={{ r: 4, fill: CHART_COLOR, strokeWidth: 0 }}
                    activeDot={{ r: 6 }}
                    connectNulls
                    isAnimationActive={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="dashboard-grid">
            <div className="card table-card">
              <div className="table-card-header">
                <h3>Tickets by client</h3>
                <button className="button small" onClick={exportClients}>
                  Export CSV
                </button>
              </div>
              <table className="table">
                <thead>
                  <tr>
                    <th>Client</th>
                    <th className="num">Tickets</th>
                    <th className="num">Open</th>
                    <th className="num">Resolved</th>
                    <th className="num">SLA compliance</th>
                  </tr>
                </thead>
                <tbody>
                  {report.byClient.map((c) => (
                    <tr key={c.clientId}>
                      <td>{c.clientName}</td>
                      <td className="num">{c.total}</td>
                      <td className="num">{c.open}</td>
                      <td className="num">{c.resolved}</td>
                      <td className="num">{c.compliancePercent != null ? `${c.compliancePercent}%` : '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="card">
              <h3>Tickets by category</h3>
              <div role="img" aria-label="Bar chart of tickets by category">
                <ResponsiveContainer width="100%" height={Math.max(160, categories.length * 34)}>
                  <BarChart data={categories} layout="vertical" margin={{ top: 0, right: 24, bottom: 0, left: 8 }}>
                    <CartesianGrid horizontal={false} stroke="#eceef2" />
                    <XAxis type="number" allowDecimals={false} tickLine={false} axisLine={false} tick={AXIS_TICK} />
                    <YAxis type="category" dataKey="label" width={120} tickLine={false} axisLine={false} tick={AXIS_TICK} />
                    <Tooltip cursor={{ fill: 'rgba(35, 87, 217, 0.06)' }} formatter={(v: number) => [v, 'Tickets']} />
                    <Bar dataKey="count" fill={CHART_COLOR} radius={[0, 4, 4, 0]} maxBarSize={22} isAnimationActive={false} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>
          </div>
        </>
      )}
    </section>
  )
}
