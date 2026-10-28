import { Link } from 'react-router-dom'

interface Props {
  label: string
  value: string | number
  hint?: string
  tone?: 'warning' | 'critical'
  /** Makes the tile a link to the matching ticket list. */
  to?: string
}

export function StatTile({ label, value, hint, tone, to }: Props) {
  const body = (
    <>
      <div className="stat-label">{label}</div>
      <div className="stat-value">{value}</div>
      {hint && <div className="stat-hint">{hint}</div>}
    </>
  )
  const className = `card stat-tile ${tone ? `stat-${tone}` : ''}`
  return to ? (
    <Link to={to} className={`${className} stat-link`}>
      {body}
    </Link>
  ) : (
    <div className={className}>{body}</div>
  )
}
