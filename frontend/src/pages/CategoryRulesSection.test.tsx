import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from '../api/client'
import type { Category, CategoryRule } from '../api/types'
import { AuthContextTestProvider } from '../auth/testUtils'
import { CategoryRulesSection } from './CategoryRulesSection'

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return { ...actual, apiFetch: vi.fn() }
})

const categories: Category[] = [
  {
    id: 10,
    name: 'Alimentation',
    icon: null,
    color: null,
    parentCategoryId: null,
    isSystemDefault: true,
    isOwnedByCurrentUser: false,
  },
  {
    id: 11,
    name: 'Transport',
    icon: null,
    color: null,
    parentCategoryId: null,
    isSystemDefault: true,
    isOwnedByCurrentUser: false,
  },
]

const rule: CategoryRule = { id: 1, matchPattern: 'NETFLIX', categoryId: 10, priority: 100 }

function renderSection(overrides: { rules?: CategoryRule[] } = {}) {
  vi.mocked(apiFetch).mockImplementation(async (path: unknown) => {
    const p = path as string
    if (p === '/api/category-rules') {
      return overrides.rules ?? []
    }
    return undefined
  })

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContextTestProvider isAuthenticated>
        <CategoryRulesSection categories={categories} />
      </AuthContextTestProvider>
    </QueryClientProvider>,
  )
}

describe('CategoryRulesSection', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('renders existing rules with the resolved category name', async () => {
    renderSection({ rules: [rule] })

    // The line renders across sibling text nodes/spans (pattern, arrow, category name,
    // priority), so match on substrings rather than one exact concatenated string.
    expect(await screen.findByText(/NETFLIX/)).toBeInTheDocument()
    expect(screen.getByText('Alimentation')).toBeInTheDocument()
    expect(screen.getByText(/priorité 100/)).toBeInTheDocument()
  })

  it('shows an empty state when there are no rules', async () => {
    renderSection({ rules: [] })

    expect(await screen.findByText("Aucune règle pour l'instant.")).toBeInTheDocument()
  })

  it('creates a rule with the entered pattern, category and priority', async () => {
    renderSection({ rules: [] })
    await screen.findByText("Aucune règle pour l'instant.")

    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Motif à rechercher dans le libellé'), 'SPOTIFY')
    await user.selectOptions(screen.getByLabelText('Catégorie de la règle'), '11')
    await user.clear(screen.getByLabelText('Priorité'))
    await user.type(screen.getByLabelText('Priorité'), '50')
    await user.click(screen.getByRole('button', { name: 'Ajouter la règle' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/category-rules',
        expect.objectContaining({
          method: 'POST',
          body: { matchPattern: 'SPOTIFY', categoryId: 11, priority: 50 },
        }),
      ),
    )
  })

  it('deletes a rule after confirmation', async () => {
    renderSection({ rules: [rule] })
    await screen.findByText(/NETFLIX/)
    vi.stubGlobal('confirm', vi.fn().mockReturnValue(true))

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Supprimer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith('/api/category-rules/1', expect.objectContaining({ method: 'DELETE' })),
    )
  })

  it('does not delete when the confirmation is dismissed', async () => {
    renderSection({ rules: [rule] })
    await screen.findByText(/NETFLIX/)
    vi.stubGlobal('confirm', vi.fn().mockReturnValue(false))

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Supprimer' }))

    expect(apiFetch).not.toHaveBeenCalledWith('/api/category-rules/1', expect.objectContaining({ method: 'DELETE' }))
  })
})
