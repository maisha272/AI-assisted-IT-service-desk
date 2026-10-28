import type { Sla, SlaState, TicketStatus } from '../api/types'
import { formatDuration } from './format'

export const isOpen = (status: TicketStatus) => status !== 'Resolved' && status !== 'Closed'

/** The deadline that matters right now: first response until an agent responds, then resolution. */
export const activeDeadline = (sla: Sla) => (sla.firstRespondedAt ? sla.resolutionDueAt : sla.responseDueAt)

/** "2h 30m left" / "3h overdue"; null once the target has been met. */
export function slaTimeText(state: SlaState, dueAt: string, now: Date): string | null {
  if (state === 'Met') return null
  const minutes = (new Date(dueAt).getTime() - now.getTime()) / 60000
  return minutes >= 0 ? `${formatDuration(minutes)} left` : `${formatDuration(minutes)} overdue`
}
