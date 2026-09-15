import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from '../api/client'
import { AuthProvider } from './AuthProvider'
import { useAuth } from './authContext'

vi.mock('../api/client', () => ({
  apiFetch: vi.fn(),
}))

const STORAGE_KEY = 'budget-previsionnel.auth'

function TestConsumer() {
  const { user, isAuthenticated, login, logout } = useAuth()

  return (
    <div>
      <p data-testid="status">{isAuthenticated ? `logged in as ${user?.email}` : 'logged out'}</p>
      <button onClick={() => login('test@example.com', 'password123')}>login</button>
      <button onClick={logout}>logout</button>
    </div>
  )
}

describe('AuthProvider', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  afterEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  it('starts logged out when localStorage has nothing stored', () => {
    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    expect(screen.getByTestId('status')).toHaveTextContent('logged out')
  })

  it('restores a session already stored in localStorage', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ token: 'stored-token', user: { id: 1, email: 'saved@example.com' } }),
    )

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    expect(screen.getByTestId('status')).toHaveTextContent('logged in as saved@example.com')
  })

  it('login persists the token and user to localStorage', async () => {
    vi.mocked(apiFetch).mockResolvedValue({
      token: 'new-token',
      expiresAtUtc: '2026-01-01T00:00:00Z',
      user: { id: 2, email: 'test@example.com' },
    })
    const user = userEvent.setup()

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )
    await user.click(screen.getByRole('button', { name: 'login' }))

    await waitFor(() =>
      expect(screen.getByTestId('status')).toHaveTextContent('logged in as test@example.com'),
    )
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY)!)).toMatchObject({ token: 'new-token' })
  })

  it('logout clears the stored session', async () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ token: 'stored-token', user: { id: 1, email: 'saved@example.com' } }),
    )
    const user = userEvent.setup()

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )
    await user.click(screen.getByRole('button', { name: 'logout' }))

    expect(screen.getByTestId('status')).toHaveTextContent('logged out')
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull()
  })
})
