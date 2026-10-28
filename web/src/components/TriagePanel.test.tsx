import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import type { TriageSuggestion } from '../api/types'
import TriagePanel from './TriagePanel'

const pending: TriageSuggestion = {
  id: 7,
  ticketId: 42,
  status: 'Pending',
  suggestedCategory: 'Printing',
  suggestedPriority: 'Medium',
  summary: 'Printer offline for level 2.',
  steps: ['Check printer IP', 'Update print queue port'],
  rationale: 'Mentions printer.',
  confidence: 'high',
  relatedArticles: [{ id: 3, title: 'Printer showing offline', score: 0.62 }],
  provider: 'mock',
  createdAt: '2026-03-02T09:00:00Z',
  reviewedByName: null,
  reviewedAt: null,
  finalCategory: null,
  finalPriority: null,
  finalSummary: null,
  finalSteps: null,
  wasEdited: false,
  rejectionReason: null,
}

describe('TriagePanel', () => {
  afterEach(() => vi.restoreAllMocks())

  it('shows the suggestion with its sources', async () => {
    vi.spyOn(api, 'getTriage').mockResolvedValue(pending)
    render(<TriagePanel ticketId={42} onApproved={() => {}} />)

    expect(await screen.findByDisplayValue('Printer offline for level 2.')).toBeInTheDocument()
    expect(screen.getByText(/Printer showing offline/)).toBeInTheDocument()
    expect(screen.getByText('(62% match)')).toBeInTheDocument()
  })

  it('sends the agent’s edited values when approving', async () => {
    vi.spyOn(api, 'getTriage').mockResolvedValue(pending)
    const approve = vi.spyOn(api, 'approveTriage').mockResolvedValue({ ...pending, status: 'Approved', wasEdited: true })
    const onApproved = vi.fn()
    render(<TriagePanel ticketId={42} onApproved={onApproved} />)

    await userEvent.selectOptions(await screen.findByLabelText('Priority'), 'High')
    const summary = screen.getByLabelText('Summary')
    await userEvent.clear(summary)
    await userEvent.type(summary, 'Level 2 printer offline')
    await userEvent.click(screen.getByRole('button', { name: 'Approve' }))

    expect(approve).toHaveBeenCalledWith(7, {
      category: 'Printing',
      priority: 'High',
      summary: 'Level 2 printer offline',
      steps: ['Check printer IP', 'Update print queue port'],
    })
    expect(onApproved).toHaveBeenCalled()
    expect(await screen.findByText(/with edits/)).toBeInTheDocument()
  })
})
