import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from '../api/client'
import type { Category, RecurringExpense } from '../api/types'
import { AuthContextTestProvider } from '../auth/testUtils'
import { RecurringExpensesSection } from './RecurringExpensesSection'

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return { ...actual, apiFetch: vi.fn() }
})

const categories: Category[] = [
  { id: 10, name: 'Logement', icon: null, color: null, parentCategoryId: null, isSystemDefault: true, isOwnedByCurrentUser: false },
  { id: 11, name: 'Abonnements', icon: null, color: null, parentCategoryId: null, isSystemDefault: true, isOwnedByCurrentUser: false },
]

const expense: RecurringExpense = {
  id: 1,
  label: 'Loyer',
  amount: 800,
  categoryId: 10,
  categoryName: 'Logement',
  frequency: 'Monthly',
  startDate: '2026-01-01',
  endDate: null,
}

function renderSection(overrides: { expenses?: RecurringExpense[] } = {}) {
  let expenses = overrides.expenses ?? []

  vi.mocked(apiFetch).mockImplementation(async (path: unknown, options?: unknown) => {
    const p = path as string
    const opts = (options ?? {}) as { method?: string; body?: unknown }

    if (p === '/api/recurring-expenses' && (!opts.method || opts.method === 'GET')) {
      return expenses
    }
    if (p === '/api/recurring-expenses' && opts.method === 'POST') {
      const body = opts.body as { categoryId: number | null } & Omit<RecurringExpense, 'id' | 'categoryName'>
      const categoryName = categories.find((c) => c.id === body.categoryId)?.name ?? null
      const created: RecurringExpense = { id: expenses.length + 1, categoryName, ...body }
      expenses = [...expenses, created]
      return created
    }
    if (p.startsWith('/api/recurring-expenses/') && opts.method === 'PUT') {
      const id = Number(p.split('/').pop())
      expenses = expenses.map((e) => (e.id === id ? { ...e, ...(opts.body as Omit<RecurringExpense, 'id'>) } : e))
      return expenses.find((e) => e.id === id)
    }
    if (p.startsWith('/api/recurring-expenses/') && opts.method === 'DELETE') {
      const id = Number(p.split('/').pop())
      expenses = expenses.filter((e) => e.id !== id)
      return undefined
    }
    return undefined
  })

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContextTestProvider isAuthenticated>
        <RecurringExpensesSection categories={categories} />
      </AuthContextTestProvider>
    </QueryClientProvider>,
  )
}

describe('RecurringExpensesSection', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('renders an existing recurring expense', async () => {
    renderSection({ expenses: [expense] })

    // The frequency <select> also has a "Mensuelle" option, so scope to the row.
    const row = (await screen.findByText(/Loyer/)).closest('li')!
    expect(within(row).getByText(/Mensuelle/)).toBeInTheDocument()
    expect(within(row).getByText(/Logement/)).toBeInTheDocument()
  })

  it('shows an empty state when there are none', async () => {
    renderSection({ expenses: [] })

    expect(await screen.findByText("Aucune dépense récurrente pour l'instant.")).toBeInTheDocument()
  })

  it('creates a recurring expense', async () => {
    renderSection({ expenses: [] })
    await screen.findByText("Aucune dépense récurrente pour l'instant.")

    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Libellé de la dépense récurrente'), 'Netflix')
    await user.type(screen.getByLabelText('Montant'), '15')
    await user.selectOptions(screen.getByLabelText('Catégorie de la dépense récurrente'), '11')
    await user.selectOptions(screen.getByLabelText('Fréquence'), 'Monthly')
    await user.type(screen.getByLabelText('Date de début'), '2026-02-01')

    await user.click(screen.getByRole('button', { name: 'Ajouter la dépense' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/recurring-expenses',
        expect.objectContaining({
          method: 'POST',
          body: { label: 'Netflix', amount: 15, categoryId: 11, frequency: 'Monthly', startDate: '2026-02-01', endDate: null },
        }),
      ),
    )
    expect(await screen.findByText(/Netflix/)).toBeInTheDocument()
  })

  it('edits a recurring expense in place', async () => {
    renderSection({ expenses: [expense] })
    await screen.findByText(/Loyer/)

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Modifier' }))

    // Both the always-visible create form and this edit form have a "Montant" field,
    // so scope to the edit form (identified by its "Enregistrer" button).
    const editForm = screen.getByRole('button', { name: 'Enregistrer' }).closest('form')!
    const amountInput = within(editForm).getByLabelText('Montant')
    await user.clear(amountInput)
    await user.type(amountInput, '820')
    await user.click(within(editForm).getByRole('button', { name: 'Enregistrer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/recurring-expenses/1',
        expect.objectContaining({ method: 'PUT', body: expect.objectContaining({ amount: 820 }) }),
      ),
    )
  })

  it('deletes a recurring expense after confirmation', async () => {
    renderSection({ expenses: [expense] })
    await screen.findByText(/Loyer/)
    vi.stubGlobal('confirm', vi.fn().mockReturnValue(true))

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Supprimer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith('/api/recurring-expenses/1', expect.objectContaining({ method: 'DELETE' })),
    )
    expect(await screen.findByText("Aucune dépense récurrente pour l'instant.")).toBeInTheDocument()
  })
})
