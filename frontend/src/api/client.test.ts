import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, ApiError } from './client'

function mockFetchOnce(response: { status: number; body?: unknown }) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      ok: response.status >= 200 && response.status < 300,
      status: response.status,
      json: () => Promise.resolve(response.body),
    }),
  )
}

describe('apiFetch', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('returns the parsed body on success', async () => {
    mockFetchOnce({ status: 200, body: { id: 1, email: 'test@example.com' } })

    const result = await apiFetch<{ id: number; email: string }>('/api/auth/me')

    expect(result).toEqual({ id: 1, email: 'test@example.com' })
  })

  it('returns undefined for a 204 No Content response', async () => {
    mockFetchOnce({ status: 204 })

    const result = await apiFetch('/api/bank-accounts/1')

    expect(result).toBeUndefined()
  })

  it('throws ApiError with the ProblemDetails title/detail/status on failure', async () => {
    mockFetchOnce({
      status: 404,
      body: { title: 'Not found', detail: 'Bank account 1 was not found.', status: 404 },
    })

    await expect(apiFetch('/api/bank-accounts/1')).rejects.toMatchObject({
      status: 404,
      title: 'Not found',
      message: 'Bank account 1 was not found.',
    })
  })

  it('carries FluentValidation field errors from a ValidationProblemDetails response', async () => {
    mockFetchOnce({
      status: 400,
      body: {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { Email: ["'Email' is not a valid email address."] },
      },
    })

    const error = await apiFetch('/api/auth/register').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).errors).toEqual({ Email: ["'Email' is not a valid email address."] })
  })

  it('attaches the bearer token when one is provided', async () => {
    mockFetchOnce({ status: 200, body: {} })

    await apiFetch('/api/auth/me', { token: 'abc123' })

    const [, init] = vi.mocked(fetch).mock.calls[0]
    const headers = init?.headers as Record<string, string> | undefined
    expect(headers?.Authorization).toBe('Bearer abc123')
  })
})
