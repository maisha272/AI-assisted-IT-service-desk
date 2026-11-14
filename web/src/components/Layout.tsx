import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import {
  IconBook,
  IconDashboard,
  IconLogOut,
  IconPlug,
  IconPlusCircle,
  IconReports,
  IconTickets,
} from './Icons'

function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

export default function Layout() {
  const { user, isAgent, logout } = useAuth()

  return (
    <div className="app app-shell">
      <aside className="sidebar" aria-label="Sidebar navigation">
        <div className="sidebar-header">
          <div className="brand">
            <span className="brand-mark">SD</span>
            <div>
              Service Desk
              <span className="brand-subtitle">{isAgent ? 'MSP Console' : 'Client Portal'}</span>
            </div>
          </div>
        </div>

        <nav className="sidebar-nav">
          <div className="sidebar-section-label">Workspace</div>
          {isAgent && (
            <NavLink to="/dashboard">
              <IconDashboard />
              Dashboard
            </NavLink>
          )}
          <NavLink to="/tickets" end>
            <IconTickets />
            {isAgent ? 'Tickets' : 'My tickets'}
          </NavLink>
          <NavLink to="/tickets/new">
            <IconPlusCircle />
            New ticket
          </NavLink>

          {isAgent && (
            <>
              <div className="sidebar-section-label">Operations &amp; AI</div>
              <NavLink to="/knowledge">
                <IconBook />
                Knowledge base
              </NavLink>
              <NavLink to="/reports">
                <IconReports />
                Reports
              </NavLink>
              <NavLink to="/integrations">
                <IconPlug />
                Integrations
              </NavLink>
            </>
          )}
        </nav>

        <div className="sidebar-footer">
          <div className="sidebar-footer-card">
            <span className="status-dot-live" aria-hidden="true" />
            <span>SLA monitor &amp; AI triage active</span>
          </div>
        </div>
      </aside>

      <div className="main-area">
        <header className="topbar">
          <div className="topbar-context">
            <span>{isAgent ? 'MSP Support Operations' : (user?.clientName ?? 'Client Support Portal')}</span>
          </div>
          {user && (
            <div className="user-menu">
              <div className="user-profile">
                <span className="user-avatar" aria-hidden="true">
                  {initials(user.displayName)}
                </span>
                <span className="user-name">
                  {user.displayName}
                  <span className="role-pill">{isAgent ? 'Agent' : user.clientName}</span>
                </span>
              </div>
              <button className="button small" onClick={logout}>
                <IconLogOut size={14} />
                Sign out
              </button>
            </div>
          )}
        </header>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
