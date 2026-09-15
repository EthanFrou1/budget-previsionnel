import { useCallback, useState, type ReactNode } from 'react'
import { apiFetch } from '../api/client'
import type { AuthResponse, UserResponse } from '../api/types'
import { AuthContext, type AuthContextValue } from './authContext'

const STORAGE_KEY = 'budget-previsionnel.auth'

interface StoredAuth {
  token: string
  user: UserResponse
}

function readStoredAuth(): StoredAuth | null {
  // localStorage can throw (private browsing, blocked site data) or hold a value from
  // a previous, incompatible app version - never let a bad read crash the whole app.
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as StoredAuth) : null
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<StoredAuth | null>(readStoredAuth)

  const persist = useCallback((value: StoredAuth | null) => {
    setAuth(value)
    try {
      if (value) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(value))
      } else {
        localStorage.removeItem(STORAGE_KEY)
      }
    } catch {
      // Same reasoning as readStoredAuth: a blocked/full storage shouldn't break auth
      // for the current session, just means it won't survive a reload.
    }
  }, [])

  const login = useCallback(
    async (email: string, password: string) => {
      const result = await apiFetch<AuthResponse>('/api/auth/login', {
        method: 'POST',
        body: { email, password },
      })
      persist({ token: result.token, user: result.user })
    },
    [persist],
  )

  const register = useCallback(
    async (email: string, password: string) => {
      const result = await apiFetch<AuthResponse>('/api/auth/register', {
        method: 'POST',
        body: { email, password },
      })
      persist({ token: result.token, user: result.user })
    },
    [persist],
  )

  const logout = useCallback(() => persist(null), [persist])

  const value: AuthContextValue = {
    user: auth?.user ?? null,
    token: auth?.token ?? null,
    isAuthenticated: auth !== null,
    login,
    register,
    logout,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
