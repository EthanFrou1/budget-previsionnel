import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { ProtectedRoute, PublicOnlyRoute } from './ProtectedRoute'
import { AuthContextTestProvider } from './testUtils'

function renderProtected(isAuthenticated: boolean, initialPath = '/private') {
  return render(
    <AuthContextTestProvider isAuthenticated={isAuthenticated}>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route element={<ProtectedRoute />}>
            <Route path="/private" element={<p>private content</p>} />
          </Route>
          <Route path="/login" element={<p>login page</p>} />
        </Routes>
      </MemoryRouter>
    </AuthContextTestProvider>,
  )
}

describe('ProtectedRoute', () => {
  it('renders the route when authenticated', () => {
    renderProtected(true)

    expect(screen.getByText('private content')).toBeInTheDocument()
  })

  it('redirects to /login when not authenticated', () => {
    renderProtected(false)

    expect(screen.getByText('login page')).toBeInTheDocument()
    expect(screen.queryByText('private content')).not.toBeInTheDocument()
  })
})

describe('PublicOnlyRoute', () => {
  function renderPublicOnly(isAuthenticated: boolean) {
    return render(
      <AuthContextTestProvider isAuthenticated={isAuthenticated}>
        <MemoryRouter initialEntries={['/login']}>
          <Routes>
            <Route element={<PublicOnlyRoute />}>
              <Route path="/login" element={<p>login page</p>} />
            </Route>
            <Route path="/" element={<p>home page</p>} />
          </Routes>
        </MemoryRouter>
      </AuthContextTestProvider>,
    )
  }

  it('renders the route when not authenticated', () => {
    renderPublicOnly(false)

    expect(screen.getByText('login page')).toBeInTheDocument()
  })

  it('redirects away from /login when already authenticated', () => {
    renderPublicOnly(true)

    expect(screen.getByText('home page')).toBeInTheDocument()
    expect(screen.queryByText('login page')).not.toBeInTheDocument()
  })
})
