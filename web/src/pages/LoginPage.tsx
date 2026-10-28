import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

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
      <div className="login-card card">
        <div className="brand login-brand">
          <span className="brand-mark">SD</span> Service Desk
        </div>
        <p className="muted">AI-assisted IT service desk for managed service providers.</p>

        <form className="form" onSubmit={submit}>
          <label>
            Email
            <input type="email" required autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} />
          </label>
          <label>
            Password
            <input
              type="password"
              required
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
            <button key={a.email} className="button full demo-button" disabled={busy} onClick={() => signIn(a.email, DEMO_PASSWORD)}>
              <strong>{a.label}</strong>
              <span className="muted">{a.name}</span>
            </button>
          ))}
          <p className="muted small">
            Password for all demo accounts: <code>{DEMO_PASSWORD}</code>
          </p>
        </div>
      </div>
    </div>
  )
}
