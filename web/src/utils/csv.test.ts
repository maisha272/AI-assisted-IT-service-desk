import { describe, expect, it } from 'vitest'
import { csvField, toCsv } from './csv'

describe('csv', () => {
  it('leaves plain values alone', () => {
    expect(csvField('Acme')).toBe('Acme')
    expect(csvField(42)).toBe('42')
    expect(csvField(null)).toBe('')
  })

  it('quotes values containing commas, quotes or newlines', () => {
    expect(csvField('Smith, Jones & Co')).toBe('"Smith, Jones & Co"')
    expect(csvField('The "best" client')).toBe('"The ""best"" client"')
    expect(csvField('line1\nline2')).toBe('"line1\nline2"')
  })

  it('builds rows with CRLF line endings', () => {
    expect(toCsv(['Client', 'Tickets'], [['Acme, Inc', 3], ['Bolt', 1]])).toBe('Client,Tickets\r\n"Acme, Inc",3\r\nBolt,1')
  })
})
