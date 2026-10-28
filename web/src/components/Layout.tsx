import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export default function Layout() {
  const { user, isAgent, logout } = useAuth()

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark">SD</span> Service Desk
        </div>
        <nav>
          {isAgent && <NavLink to="/dashboard">Dashboard</NavLink>}
          <NavLink to="/tickets" end>
            {isAgent ? 'Tickets' : 'My tickets'}
          </NavLink>
          <NavLink to="/tickets/new">New ticket</NavLink>
          {isAgent && <NavLink to="/knowledge">Knowledge base</NavLink>}
          {isAgent && <NavLink to="/integrations">Integrations</NavLink>}
        </nav>
        {user && (
          <div className="user-menu">
            <span>
              {user.displayName}
              <span className="role-pill">{isAgent ? 'Agent' : user.clientName}</span>
            </span>
            <button className="button small" onClick={logout}>
              Sign out
            </button>
          </div>
        )}
      </header>
      <main className="content">
        <Outlet />
      </main>
    </div>
  )
}
