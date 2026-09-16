import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from '../api/client'
import type { BudgetLine, Category } from '../api/types'
import { AuthContextTestProvider } from '../auth/testUtils'
import { ConfirmProvider } from '../components/ConfirmDialog'
import { BudgetSection } from './BudgetSection'

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return { ...actual, apiFetch: vi.fn() }
})

const categories: Category[] = [
  {
    id: 10,
    name: 'Logement',
    icon: null,
    color: null,
    parentCategoryId: null,
    isSystemDefault: true,
    isOwnedByCurrentUser: false,
  },
  {
    id: 11,
    name: 'Loisirs',
    icon: null,
    color: null,
    parentCategoryId: null,
    isSystemDefault: true,
    isOwnedByCurrentUser: false,
  },
]

const month = '2026-03-01'

const line: BudgetLine = {
  id: 1,
  month,
  categoryId: 10,
  categoryName: 'Logement',
  plannedAmount: 900,
  actualAmount: 950,
}

function renderSection(overrides: { lines?: BudgetLine[] } = {}) {
  let lines = overrides.lines ?? []

  vi.mocked(apiFetch).mockImplementation(async (path: unknown, options?: unknown) => {
    const p = path as string
    const opts = (options ?? {}) as { method?: string; body?: unknown }

    if (p === `/api/budgets?month=${month}`) {
      return lines
    }
    if (p === '/api/budgets' && opts.method === 'POST') {
      const body = opts.body as { categoryId: number; plannedAmount: number }
      const category = categories.find((c) => c.id === body.categoryId)!
      const created: BudgetLine = {
        id: lines.length + 1,
        month,
        categoryId: body.categoryId,
        categoryName: category.name,
        plannedAmount: body.plannedAmount,
        actualAmount: 0,
      }
      lines = [...lines, created]
      return created
    }
    if (p.startsWith('/api/budgets/') && opts.method === 'PUT') {
      const id = Number(p.split('/').pop())
      const body = opts.body as { plannedAmount: number }
      lines = lines.map((l) => (l.id === id ? { ...l, plannedAmount: body.plannedAmount } : l))
      return lines.find((l) => l.id === id)
    }
    if (p.startsWith('/api/budgets/') && opts.method === 'DELETE') {
      const id = Number(p.split('/').pop())
      lines = lines.filter((l) => l.id !== id)
      return undefined
    }
    return undefined
  })

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContextTestProvider isAuthenticated>
        <ConfirmProvider>
          <BudgetSection month={month} categories={categories} />
        </ConfirmProvider>
      </AuthContextTestProvider>
    </QueryClientProvider>,
  )
}

describe('BudgetSection', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('renders a budget line with planned and actual amounts', async () => {
    renderSection({ lines: [line] })

    expect(await screen.findByText('Logement')).toBeInTheDocument()
    expect(screen.getByText(/900,00/)).toBeInTheDocument()
    expect(screen.getByText(/950,00/)).toBeInTheDocument()
  })

  it('shows an empty state when there are no lines', async () => {
    renderSection({ lines: [] })

    expect(await screen.findByText('Aucune ligne de budget pour ce mois.')).toBeInTheDocument()
  })

  it('hides the create form behind a toggle button until the user asks for it', async () => {
    renderSection({ lines: [] })

    await screen.findByText('Aucune ligne de budget pour ce mois.')
    expect(screen.queryByLabelText('Catégorie')).not.toBeInTheDocument()

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: '+ Ajouter une ligne' }))
    expect(screen.getByLabelText('Catégorie')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Annuler' }))
    expect(screen.queryByLabelText('Catégorie')).not.toBeInTheDocument()
  })

  it('only offers categories without an existing budget line for the month', async () => {
    renderSection({ lines: [line] })
    await screen.findByText('Logement')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: '+ Ajouter une ligne' }))

    const select = screen.getByLabelText('Catégorie') as HTMLSelectElement
    const optionLabels = Array.from(select.options).map((o) => o.textContent)
    expect(optionLabels).toEqual(['Loisirs'])
  })

  it('creates a budget line', async () => {
    renderSection({ lines: [] })
    await screen.findByText('Aucune ligne de budget pour ce mois.')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: '+ Ajouter une ligne' }))
    await user.selectOptions(screen.getByLabelText('Catégorie'), '11')
    await user.type(screen.getByLabelText('Montant planifié'), '120')
    await user.click(screen.getByRole('button', { name: 'Ajouter la ligne' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/budgets',
        expect.objectContaining({ method: 'POST', body: { month, categoryId: 11, plannedAmount: 120 } }),
      ),
    )
    expect(await screen.findByText('Loisirs')).toBeInTheDocument()
  })

  it('edits the planned amount in place', async () => {
    renderSection({ lines: [line] })
    await screen.findByText('Logement')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Modifier' }))

    const input = screen.getByLabelText('Montant planifié pour Logement')
    await user.clear(input)
    await user.type(input, '1000')
    await user.click(screen.getByRole('button', { name: 'OK' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/budgets/1',
        expect.objectContaining({ method: 'PUT', body: { plannedAmount: 1000 } }),
      ),
    )
  })

  it('deletes a budget line after confirmation', async () => {
    renderSection({ lines: [line] })
    await screen.findByText('Logement')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Supprimer' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Supprimer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith('/api/budgets/1', expect.objectContaining({ method: 'DELETE' })),
    )
    expect(await screen.findByText('Aucune ligne de budget pour ce mois.')).toBeInTheDocument()
  })
})
