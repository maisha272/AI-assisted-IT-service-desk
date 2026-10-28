import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../auth/AuthContext'
import LoginPage from './LoginPage'

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/" element={<p>Home page</p>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

const jsonResponse = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

describe('LoginPage', () => {
  beforeEach(() => localStorage.clear())
  afterEach(() => vi.restoreAllMocks())

  it('signs in with a demo account and redirects', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      jsonResponse(200, {
        token: 'test-token',
        expiresAt: '2030-01-01T00:00:00Z',
        user: { id: 1, email: 'sam@servicedesk.test', displayName: 'Sam Taylor', role: 'Agent', clientId: null, clientName: null },
      }),
    )
    renderLogin()

    await userEvent.click(screen.getByRole('button', { name: /support agent/i }))

    expect(await screen.findByText('Home page')).toBeInTheDocument()
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/auth/login')
    expect(JSON.parse(init!.body as string)).toEqual({ email: 'sam@servicedesk.test', password: 'Demo123!' })
    expect(localStorage.getItem('servicedesk.session')).toContain('test-token')
  })

  it('shows the API error message when login fails', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      jsonResponse(401, { title: 'Unauthorized', detail: 'Invalid email or password.' }),
    )
    renderLogin()

    await userEvent.type(screen.getByLabelText('Email'), 'someone@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'nope')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Invalid email or password.')).toBeInTheDocument()
    expect(screen.queryByText('Home page')).not.toBeInTheDocument()
  })
})
