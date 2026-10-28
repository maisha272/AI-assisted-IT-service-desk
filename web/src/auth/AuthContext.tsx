import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, setAuthToken, setUnauthorizedHandler } from '../api/client'
import type { CurrentUser } from '../api/types'

const STORAGE_KEY = 'servicedesk.session'

interface Session {
  token: string
  user: CurrentUser
}

interface AuthContextValue {
  user: CurrentUser | null
  isAgent: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

function loadSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as Session) : null
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => {
    const saved = loadSession()
    // Set the token synchronously so the very first API calls are authenticated.
    setAuthToken(saved?.token ?? null)
    return saved
  })

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY)
    setAuthToken(null)
    setSession(null)
  }, [])

  // If the API ever says 401 (e.g. the token expired), drop the session and show the login page.
  useEffect(() => setUnauthorizedHandler(logout), [logout])

  const login = useCallback(async (email: string, password: string) => {
    const response = await api.login(email, password)
    const next = { token: response.token, user: response.user }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
    setAuthToken(next.token)
    setSession(next)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user ?? null,
      isAgent: session?.user.role === 'Agent',
      login,
      logout,
    }),
    [session, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>')
  return ctx
}
