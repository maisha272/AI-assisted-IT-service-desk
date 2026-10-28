import { describe, expect, it } from 'vitest'
import { categoryLabel, formatDuration, statusLabel } from './format'

describe('formatDuration', () => {
  it.each([
    [0, '0m'],
    [45, '45m'],
    [60, '1h'],
    [200, '3h 20m'],
    [1440, '1d'],
    [1440 * 2 + 240, '2d 4h'],
  ])('formats %i minutes as %s', (minutes, expected) => {
    expect(formatDuration(minutes)).toBe(expected)
  })

  it('uses the absolute value so overdue amounts read naturally', () => {
    expect(formatDuration(-90)).toBe('1h 30m')
  })
})

describe('labels', () => {
  it('humanises enum values', () => {
    expect(statusLabel('InProgress')).toBe('In progress')
    expect(categoryLabel('AccountAccess')).toBe('Account & access')
  })
})
