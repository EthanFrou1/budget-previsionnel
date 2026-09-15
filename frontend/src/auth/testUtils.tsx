import type { ReactNode } from 'react'
import { AuthContext, type AuthContextValue } from './authContext'

/** Test-only provider that supplies a fixed AuthContextValue, for components/routes
 * that only need to react to isAuthenticated/user without exercising the real
 * localStorage-backed login/register flow. */
export function AuthContextTestProvider({
  children,
  isAuthenticated,
  user = isAuthenticated ? { id: 1, email: 'test@example.com' } : null,
}: {
  children: ReactNode
  isAuthenticated: boolean
  user?: AuthContextValue['user']
}) {
  const value: AuthContextValue = {
    user,
    token: isAuthenticated ? 'test-token' : null,
    isAuthenticated,
    login: async () => {},
    register: async () => {},
    logout: () => {},
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
