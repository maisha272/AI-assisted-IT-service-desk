import { NavLink, Outlet } from 'react-router-dom'

export default function Layout() {
  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark">SD</span> Service Desk
        </div>
        <nav>
          <NavLink to="/tickets" end>
            Tickets
          </NavLink>
          <NavLink to="/tickets/new">New ticket</NavLink>
        </nav>
      </header>
      <main className="content">
        <Outlet />
      </main>
    </div>
  )
}
