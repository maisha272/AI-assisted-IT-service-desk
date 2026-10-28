import type { TicketCategory, TicketStatus } from '../api/types'

const STATUS_LABELS: Record<TicketStatus, string> = {
  New: 'New',
  InProgress: 'In progress',
  Waiting: 'Waiting',
  Resolved: 'Resolved',
  Closed: 'Closed',
}

const CATEGORY_LABELS: Record<TicketCategory, string> = {
  Hardware: 'Hardware',
  Software: 'Software',
  Network: 'Network',
  AccountAccess: 'Account & access',
  Email: 'Email',
  Printing: 'Printing',
  Security: 'Security',
  Other: 'Other',
}

export const statusLabel = (s: TicketStatus) => STATUS_LABELS[s]
export const categoryLabel = (c: TicketCategory) => CATEGORY_LABELS[c]

const dateTimeFormat = new Intl.DateTimeFormat('en-AU', {
  day: 'numeric',
  month: 'short',
  hour: 'numeric',
  minute: '2-digit',
})

export const formatDateTime = (iso: string) => dateTimeFormat.format(new Date(iso))

/** "3h 20m", "2d 4h", "45m" — compact durations for SLA timers and averages. */
export function formatDuration(totalMinutes: number): string {
  const minutes = Math.round(Math.abs(totalMinutes))
  const days = Math.floor(minutes / 1440)
  const hours = Math.floor((minutes % 1440) / 60)
  const mins = minutes % 60
  if (days > 0) return hours > 0 ? `${days}d ${hours}h` : `${days}d`
  if (hours > 0) return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`
  return `${mins}m`
}
