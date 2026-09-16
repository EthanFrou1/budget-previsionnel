import { useCallback } from 'react'
import { apiFetch, apiFetchBlob, ApiError } from '../api/client'
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

/**
 * Triggers a browser save of a binary endpoint's response (e.g. re-downloading a stored
 * CSV import) - a plain <a href> can't carry the Authorization header this API requires,
 * so the file is fetched as a blob and handed to the browser via a throwaway object URL.
 */
export function useFileDownload() {
  const { token, logout } = useAuth()

  return useCallback(
    async (path: string, fallbackFileName: string) => {
      try {
        const { blob, fileName } = await apiFetchBlob(path, { token })
        const url = URL.createObjectURL(blob)
        try {
          const link = document.createElement('a')
          link.href = url
          link.download = fileName ?? fallbackFileName
          document.body.appendChild(link)
          link.click()
          link.remove()
        } finally {
          URL.revokeObjectURL(url)
        }
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
