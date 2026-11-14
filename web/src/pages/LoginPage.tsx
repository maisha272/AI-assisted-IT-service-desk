import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { IconClock, IconShieldCheck, IconSparkles } from '../components/Icons'

// Public demo accounts — these are seeded by the API and safe to show.
const DEMO_PASSWORD = 'Demo123!'
const DEMO_ACCOUNTS = [
  { label: 'Support agent', name: 'Sam Taylor', email: 'sam@servicedesk.test' },
  { label: 'Client staff', name: 'Olivia Brown, Northwind Dental', email: 'olivia@northwind.test' },
]

export default function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const redirectTo = (location.state as { from?: string } | null)?.from ?? '/'
  if (user) return <Navigate to={redirectTo} replace />

  async function signIn(e: string, p: string) {
    setBusy(true)
    setError(null)
    try {
      await login(e, p)
      navigate(redirectTo, { replace: true })
    } catch (err) {
      setError((err as Error).message)
      setBusy(false)
    }
  }

  function submit(e: FormEvent) {
    e.preventDefault()
    void signIn(email, password)
  }

  return (
    <div className="login-page">
      <div className="login-shell">
        <div className="login-Pitch">
          <div>
            <div className="brand">
              <span className="brand-mark">SD</span> Service Desk
            </div>
            <h2>Modern IT operations with human-verified AI triage.</h2>
            <p>AI-assisted IT service desk for managed service providers.</p>
            <ul className="login-feature-list">
              <li>
                <span className="login-feature-icon">
                  <IconSparkles size={14} />
                </span>
                AI-suggested category, priority &amp; runbook steps
              </li>
              <li>
                <span className="login-feature-icon">
                  <IconShieldCheck size={14} />
                </span>
                Human-in-the-loop approval before anything changes
              </li>
              <li>
                <span className="login-feature-icon">
                  <IconClock size={14} />
                </span>
                Real-time response &amp; resolution SLA tracking
              </li>
            </ul>
          </div>
          <p className="small">Built for high-velocity MSP support teams.</p>
        </div>

        <div className="login-card card">
          <div className="brand login-brand">Sign in to your workspace</div>
          <p className="muted" style={{ marginTop: 0, marginBottom: '1.25rem' }}>
            Enter your credentials or select a seeded demo account below.
          </p>

          <form className="form" onSubmit={submit}>
            <label>
              Email
              <input
                type="email"
                required
                placeholder="you@company.com"
                autoComplete="username"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </label>
            <label>
              Password
              <input
                type="password"
                required
                placeholder="••••••••"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </label>
            {error && <p className="error">{error}</p>}
            <button className="button primary full" disabled={busy}>
              {busy ? 'Signing in…' : 'Sign in'}
            </button>
          </form>

          <div className="demo-accounts">
            <h3>Try the demo</h3>
            {DEMO_ACCOUNTS.map((a) => (
              <button
                key={a.email}
                className="button full demo-button"
                disabled={busy}
                onClick={() => signIn(a.email, DEMO_PASSWORD)}
              >
                <strong>{a.label}</strong>
                <span className="muted">{a.name}</span>
              </button>
            ))}
            <p className="muted small" style={{ margin: '0.35rem 0 0' }}>
              Password for all demo accounts: <code>{DEMO_PASSWORD}</code>
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}
