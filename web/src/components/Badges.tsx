import type { TicketPriority, TicketStatus } from '../api/types'
import { statusLabel } from '../utils/format'

export function StatusBadge({ status }: { status: TicketStatus }) {
  return <span className={`badge status-${status.toLowerCase()}`}>{statusLabel(status)}</span>
}

export function PriorityBadge({ priority }: { priority: TicketPriority }) {
  return <span className={`badge priority-${priority.toLowerCase()}`}>{priority}</span>
}
