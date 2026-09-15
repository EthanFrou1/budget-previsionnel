import { Link, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/authContext'

const NAV_LINKS = [
  { to: '/', label: 'Dashboard' },
  { to: '/accounts', label: 'Comptes' },
  { to: '/transactions', label: 'Transactions' },
  { to: '/savings', label: 'Épargne' },
  { to: '/loans', label: 'Crédits' },
  { to: '/forecast', label: 'Prévisionnel' },
]

/** Shared header/nav, extracted once a third page (Lot 12) needed the same bar
 * HomePage and AccountsPage already duplicated (Lot 10/11). HomePage became
 * DashboardPage at Lot 13. */
export function AppHeader() {
  const { user, logout } = useAuth()
  const location = useLocation()

  return (
    <header className="flex flex-wrap items-center justify-between gap-3 border-b border-gray-200 bg-white px-4 py-3 dark:border-gray-700 dark:bg-gray-800">
      <nav className="flex items-center gap-4">
        {NAV_LINKS.map((link) => (
          <Link
            key={link.to}
            to={link.to}
            className={
              location.pathname === link.to
                ? 'text-sm font-semibold text-sky-600 dark:text-sky-400'
                : 'text-sm font-medium text-gray-600 hover:text-gray-900 dark:text-gray-300 dark:hover:text-white'
            }
          >
            {link.label}
          </Link>
        ))}
      </nav>
      <div className="flex items-center gap-3 text-sm text-gray-600 dark:text-gray-300">
        <span>{user?.email}</span>
        <button
          onClick={logout}
          className="rounded border border-gray-300 px-3 py-1 hover:bg-gray-100 dark:border-gray-600 dark:hover:bg-gray-700"
        >
          Déconnexion
        </button>
      </div>
    </header>
  )
}
