import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { AuthContextTestProvider } from '../auth/testUtils'
import { AppSidebar } from './AppSidebar'

function renderSidebar() {
  return render(
    <AuthContextTestProvider isAuthenticated>
      <MemoryRouter>
        <AppSidebar />
      </MemoryRouter>
    </AuthContextTestProvider>,
  )
}

describe('AppSidebar theme toggle', () => {
  beforeEach(() => {
    localStorage.clear()
    delete document.documentElement.dataset.theme
  })

  afterEach(() => {
    localStorage.clear()
    delete document.documentElement.dataset.theme
  })

  it('defaults to dark (no stored preference, jsdom has no matchMedia) and applies it to <html>', async () => {
    renderSidebar()

    expect(await screen.findByRole('button', { name: 'Activer le mode clair' })).toBeInTheDocument()
    expect(document.documentElement.dataset.theme).toBe('dark')
  })

  it('toggles to light on click, updates <html> and persists the choice', async () => {
    renderSidebar()
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Activer le mode clair' }))

    expect(await screen.findByRole('button', { name: 'Activer le mode sombre' })).toBeInTheDocument()
    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem('theme')).toBe('light')
  })

  it('starts from a previously persisted theme', async () => {
    localStorage.setItem('theme', 'light')

    renderSidebar()

    expect(await screen.findByRole('button', { name: 'Activer le mode sombre' })).toBeInTheDocument()
    expect(document.documentElement.dataset.theme).toBe('light')
  })
})
