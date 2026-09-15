import { useCallback } from 'react'
import { apiFetch, ApiError } from '../api/client'
import { useAuth } from './authContext'

type FetchOptions = Parameters<typeof apiFetch>[1]

/**
 * The base for every authenticated API call from Lot 11 onward (used as a TanStack
 * Query queryFn/mutationFn). Attaches the current token and logs out automatically on
 * a 401 - a request with an expired token surfaces as a redirect to /login instead of
 * a confusing stuck loading/error state on whatever screen triggered it.
 */
export function useApiClient() {
  const { token, logout } = useAuth()

  return useCallback(
    async <T>(path: string, options: Omit<FetchOptions, 'token'> = {}): Promise<T> => {
      try {
        return await apiFetch<T>(path, { ...options, token })
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          logout()
        }
        throw error
      }
    },
    [token, logout],
  )
}
