import { createContext, useContext } from 'react'
import type { UserResponse } from '../api/types'

export interface AuthContextValue {
  user: UserResponse | null
  token: string | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  logout: () => void
}

// Split from AuthProvider.tsx (which only exports the component): a file that exports
// both a component and other values (a context, a hook) opts it out of React Fast
// Refresh, so this stays a plain module - no JSX here.
export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
