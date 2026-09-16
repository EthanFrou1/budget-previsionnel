import { Link, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/authContext'
import { useTheme } from '../theme/useTheme'

const NAV_LINKS = [
  { to: '/', label: 'Dashboard' },
  { to: '/accounts', label: 'Comptes' },
  { to: '/transactions', label: 'Transactions' },
  { to: '/savings', label: 'Épargne' },
  { to: '/loans', label: 'Crédits' },
  { to: '/forecast', label: 'Prévisionnel' },
]

/**
 * A horizontal bar on mobile (nav wraps under the brand), a fixed-width sticky
 * sidebar from `lg` up - on desktop, six nav links plus the user/logout row no
 * longer eat vertical space above every screen's content. Renamed from
 * AppHeader (Lot 12) now that it's primarily a sidebar rather than a header.
 */
export function AppSidebar() {
  const { user, logout } = useAuth()
  const location = useLocation()
  const { theme, toggleTheme } = useTheme()

  return (
    <header className="flex shrink-0 flex-wrap items-center justify-between gap-3 border-b border-border bg-surface px-4 py-3 lg:h-svh lg:w-56 lg:flex-col lg:flex-nowrap lg:items-stretch lg:justify-start lg:gap-6 lg:sticky lg:top-0 lg:border-b-0 lg:border-r lg:px-4 lg:py-6">
      <p className="font-display hidden text-lg font-semibold text-heading lg:block lg:px-2">
        Budget Prévisionnel
      </p>

      <nav className="flex items-center gap-4 lg:flex-col lg:items-stretch lg:gap-1">
        {NAV_LINKS.map((link) => (
          <Link
            key={link.to}
            to={link.to}
            className={
              location.pathname === link.to
                ? 'text-sm font-semibold text-accent lg:rounded lg:bg-accent/15 lg:px-2 lg:py-2'
                : 'text-sm font-medium text-body hover:text-heading lg:rounded lg:px-2 lg:py-2 lg:hover:bg-overlay'
            }
          >
            {link.label}
          </Link>
        ))}
      </nav>

      <div className="flex items-center gap-3 text-sm text-body lg:mt-auto lg:flex-col lg:items-stretch lg:gap-2 lg:border-t lg:border-border lg:pt-4">
        <span className="lg:truncate lg:px-2">{user?.email}</span>
        <button
          onClick={toggleTheme}
          aria-label={theme === 'dark' ? 'Activer le mode clair' : 'Activer le mode sombre'}
          className="rounded border border-border px-3 py-1 hover:bg-overlay"
        >
          {theme === 'dark' ? '☀️ Clair' : '🌙 Sombre'}
        </button>
        <button onClick={logout} className="rounded border border-border px-3 py-1 hover:bg-overlay">
          Déconnexion
        </button>
      </div>
    </header>
  )
}
