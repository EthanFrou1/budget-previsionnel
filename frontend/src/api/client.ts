import type { ProblemDetails, ValidationProblemDetails } from './types'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080'

/** Thrown for any non-2xx response. `errors` is only set for a 400 shaped by
 * ValidationActionFilter (FluentValidation failures) - see AuthContracts.cs/Program.cs
 * on the backend for where this shape comes from. */
export class ApiError extends Error {
  // Plain field declarations, not constructor parameter-property shorthand: the
  // template's tsconfig enables erasableSyntaxOnly, which requires TS syntax to strip
  // down to plain JS with no extra codegen - parameter properties don't qualify.
  readonly status: number
  readonly title: string
  readonly errors?: Record<string, string[]>

  constructor(status: number, title: string, message: string, errors?: Record<string, string[]>) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.errors = errors
  }
}

interface ApiFetchOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  token?: string | null
}

export async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<T> {
  // FormData (multipart file upload, e.g. the CSV import) must not be JSON-stringified
  // and must NOT get an explicit Content-Type - the browser sets one with the required
  // boundary parameter, which we can't reproduce by hand.
  const isFormData = options.body instanceof FormData
  const body: BodyInit | undefined =
    options.body === undefined
      ? undefined
      : options.body instanceof FormData
        ? options.body
        : JSON.stringify(options.body)

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: options.method ?? 'GET',
    headers: {
      ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
      ...(options.token ? { Authorization: `Bearer ${options.token}` } : {}),
    },
    body,
  })

  if (!response.ok) {
    const problem: (ProblemDetails & ValidationProblemDetails) | null = await response
      .json()
      .catch(() => null)
    throw new ApiError(
      response.status,
      problem?.title ?? 'Request failed',
      problem?.detail ?? response.statusText,
      problem?.errors,
    )
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}
