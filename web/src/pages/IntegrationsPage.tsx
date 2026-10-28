import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { InboundEmailResult, IntegrationStatus, NotificationLogEntry } from '../api/types'
import { formatDateTime } from '../utils/format'

const EVENT_LABELS: Record<NotificationLogEntry['event'], string> = {
  NewTicket: 'New ticket',
  SlaAtRisk: 'SLA at risk',
  SlaBreached: 'SLA breached',
  Test: 'Test',
}

export default function IntegrationsPage() {
  const [status, setStatus] = useState<IntegrationStatus | null>(null)
  const [log, setLog] = useState<NotificationLogEntry[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const [from, setFrom] = useState('Olivia Brown <olivia@northwind.test>')
  const [subject, setSubject] = useState('Outlook keeps asking for my password')
  const [body, setBody] = useState('Since this morning Outlook pops up a password prompt every few minutes. I am typing the right password.')
  const [emailResult, setEmailResult] = useState<InboundEmailResult | null>(null)

  const refreshLog = useCallback(() => api.listNotifications().then(setLog), [])

  useEffect(() => {
    api.getIntegrationStatus().then(setStatus).catch((e: Error) => setError(e.message))
    refreshLog().catch((e: Error) => setError(e.message))
  }, [refreshLog])

  async function act(action: () => Promise<unknown>) {
    setBusy(true)
    setError(null)
    try {
      await action()
      await refreshLog()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  function simulateEmail(e: FormEvent) {
    e.preventDefault()
    setEmailResult(null)
    void act(async () => {
      try {
        setEmailResult(await api.simulateEmail({ from, subject, body }))
      } catch (err) {
        // 422 = rejected by the rules (e.g. unknown domain); show it as a result rather than an error.
        setEmailResult({ outcome: 'Rejected', ticketId: null, message: (err as Error).message })
      }
    })
  }

  return (
    <section>
      <div className="page-header">
        <div>
          <h1>Integrations</h1>
          <p className="muted">Microsoft Teams notifications and email-to-ticket.</p>
        </div>
      </div>
      {error && <p className="error">{error}</p>}

      <div className="dashboard-grid">
        <div className="card">
          <h3>Microsoft Teams</h3>
          <p>
            Status:{' '}
            {status ? (
              status.teamsConfigured ? (
                <span className="badge sla-ontrack">Webhook configured</span>
              ) : (
                <span className="badge sla-met">Not configured — notifications are logged only</span>
              )
            ) : (
              '…'
            )}
          </p>
          <p className="muted small">
            Posts an Adaptive Card to a Teams channel for every new ticket, and when a ticket becomes at risk of or
            breaches its SLA. Checked every {status?.dispatchIntervalSeconds ?? 60}s.
          </p>
          <div className="triage-actions">
            <button className="button" disabled={busy} onClick={() => act(api.dispatchNotifications)}>
              Check for notifications now
            </button>
            <button className="button" disabled={busy} onClick={() => act(api.sendTestNotification)}>
              Send test message
            </button>
          </div>
        </div>

        <form className="card form" onSubmit={simulateEmail}>
          <h3>Simulate an inbound email</h3>
          <p className="muted small">
            In production an email provider posts to <code>/api/integrations/email/inbound</code> with a shared secret
            {status && !status.inboundEmailEnabled && ' (currently disabled: no secret configured)'}. Use{' '}
            <code>[#123]</code> in the subject to reply to ticket 123.
          </p>
          <label>
            From
            <input value={from} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label>
            Subject
            <input value={subject} onChange={(e) => setSubject(e.target.value)} />
          </label>
          <label>
            Body
            <textarea rows={3} value={body} onChange={(e) => setBody(e.target.value)} />
          </label>
          <button className="button primary" disabled={busy}>
            Send email
          </button>
          {emailResult && (
            <p className={emailResult.outcome === 'Rejected' ? 'error' : 'success'}>
              {emailResult.message}{' '}
              {emailResult.ticketId && <Link to={`/tickets/${emailResult.ticketId}`}>Open ticket</Link>}
            </p>
          )}
        </form>
      </div>

      <h2>Notification log</h2>
      {log && log.length === 0 && <p className="muted">No notifications yet.</p>}
      {log && log.length > 0 && (
        <div className="card table-card">
          <table className="table">
            <thead>
              <tr>
                <th>When</th>
                <th>Event</th>
                <th>Message</th>
                <th>Result</th>
              </tr>
            </thead>
            <tbody>
              {log.map((n) => (
                <tr key={n.id}>
                  <td className="nowrap">{formatDateTime(n.createdAt)}</td>
                  <td>{EVENT_LABELS[n.event]}</td>
                  <td>{n.ticketId ? <Link to={`/tickets/${n.ticketId}`}>{n.message}</Link> : n.message}</td>
                  <td>
                    {n.delivered ? (
                      <span className="badge sla-ontrack">Delivered</span>
                    ) : (
                      <span className="muted small">{n.error}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
