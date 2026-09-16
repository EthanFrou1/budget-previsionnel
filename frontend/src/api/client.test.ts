import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, apiFetchBlob, ApiError } from './client'

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

function mockFetchBlobOnce(response: { status: number; blob?: Blob; body?: unknown; contentDisposition?: string }) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      ok: response.status >= 200 && response.status < 300,
      status: response.status,
      headers: { get: (name: string) => (name === 'Content-Disposition' ? (response.contentDisposition ?? null) : null) },
      json: () => Promise.resolve(response.body),
      blob: () => Promise.resolve(response.blob),
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

describe('apiFetchBlob', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('returns the blob and the filename parsed from Content-Disposition', async () => {
    const blob = new Blob(['csv content'], { type: 'text/csv' })
    mockFetchBlobOnce({ status: 200, blob, contentDisposition: 'attachment; filename="août-2026.csv"' })

    const result = await apiFetchBlob('/api/bank-accounts/1/import/history/1/file')

    expect(result.blob).toBe(blob)
    expect(result.fileName).toBe('août-2026.csv')
  })

  it('returns a null filename when there is no Content-Disposition header', async () => {
    const blob = new Blob(['csv content'], { type: 'text/csv' })
    mockFetchBlobOnce({ status: 200, blob })

    const result = await apiFetchBlob('/api/bank-accounts/1/import/history/1/file')

    expect(result.fileName).toBeNull()
  })

  it('throws ApiError on failure', async () => {
    mockFetchBlobOnce({
      status: 404,
      body: { title: 'Not found', detail: 'Import batch 1 was not found.', status: 404 },
    })

    await expect(apiFetchBlob('/api/bank-accounts/1/import/history/1/file')).rejects.toMatchObject({
      status: 404,
      title: 'Not found',
      message: 'Import batch 1 was not found.',
    })
  })
})
