import type { SlaState } from '../api/types'
import { slaTimeText } from '../utils/sla'

const LABELS: Record<SlaState, string> = {
  OnTrack: 'On track',
  AtRisk: 'At risk',
  Breached: 'Breached',
  Met: 'Met',
}

interface Props {
  state: SlaState
  /** Deadline for a still-running SLA; shown as "2h left" / "3h overdue". */
  dueAt?: string
  /** Injected in tests; defaults to the current time. */
  now?: Date
}

export function SlaBadge({ state, dueAt, now = new Date() }: Props) {
  const timeText = dueAt ? slaTimeText(state, dueAt, now) : null
  return (
    <span className={`badge sla-${state.toLowerCase()}`} title={`SLA: ${LABELS[state]}`}>
      {LABELS[state]}
      {timeText && <span className="sla-time"> · {timeText}</span>}
    </span>
  )
}
