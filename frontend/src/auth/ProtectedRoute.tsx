import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from './authContext'

/** Wraps a set of routes (via <Route element={<ProtectedRoute />}>...</Route>) that
 * require a logged-in user, redirecting to /login otherwise. */
export function ProtectedRoute() {
  const { isAuthenticated } = useAuth()

  return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace />
}

/** The inverse, for /login and /register: skip straight past them if already
 * authenticated instead of showing a login form to someone already logged in. */
export function PublicOnlyRoute() {
  const { isAuthenticated } = useAuth()

  return isAuthenticated ? <Navigate to="/" replace /> : <Outlet />
}
