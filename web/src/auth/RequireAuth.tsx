import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'

/**
 * Route guard. This is a UX convenience only — the API enforces every permission itself,
 * so hiding a page here is never the thing that keeps data safe.
 */
export function RequireAuth({ children, agentOnly = false }: { children: ReactNode; agentOnly?: boolean }) {
  const { user, isAgent } = useAuth()
  const location = useLocation()

  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (agentOnly && !isAgent) return <Navigate to="/tickets" replace />
  return <>{children}</>
}
