import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { slaTimeText } from '../utils/sla'
import { SlaBadge } from './SlaBadge'

const now = new Date('2026-03-02T09:00:00Z')

describe('slaTimeText', () => {
  it('shows time remaining before the deadline', () => {
    expect(slaTimeText('OnTrack', '2026-03-02T11:30:00Z', now)).toBe('2h 30m left')
  })

  it('shows how long ago the deadline passed', () => {
    expect(slaTimeText('Breached', '2026-03-02T06:00:00Z', now)).toBe('3h overdue')
  })

  it('shows nothing once the target has been met', () => {
    expect(slaTimeText('Met', '2026-03-02T06:00:00Z', now)).toBeNull()
  })
})

describe('SlaBadge', () => {
  it('renders the state with a styling class', () => {
    render(<SlaBadge state="AtRisk" dueAt="2026-03-02T09:45:00Z" now={now} />)
    const badge = screen.getByText(/At risk/)
    expect(badge).toHaveClass('sla-atrisk')
    expect(badge).toHaveTextContent('At risk · 45m left')
  })
})
