import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from '../api/client'
import type { Category, RecurringIncome } from '../api/types'
import { AuthContextTestProvider } from '../auth/testUtils'
import { ConfirmProvider } from '../components/ConfirmDialog'
import { RecurringIncomesSection } from './RecurringIncomesSection'

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return { ...actual, apiFetch: vi.fn() }
})

const categories: Category[] = [
  {
    id: 10,
    name: 'Emploi',
    icon: null,
    color: null,
    parentCategoryId: null,
    isSystemDefault: true,
    isOwnedByCurrentUser: false,
  },
]

const income: RecurringIncome = {
  id: 1,
  label: 'Salaire',
  amount: 2200,
  categoryId: 10,
  categoryName: 'Emploi',
  frequency: 'Monthly',
  startDate: '2026-01-01',
  endDate: null,
}

function renderSection(overrides: { incomes?: RecurringIncome[] } = {}) {
  let incomes = overrides.incomes ?? []

  vi.mocked(apiFetch).mockImplementation(async (path: unknown, options?: unknown) => {
    const p = path as string
    const opts = (options ?? {}) as { method?: string; body?: unknown }

    if (p === '/api/recurring-incomes' && (!opts.method || opts.method === 'GET')) {
      return incomes
    }
    if (p === '/api/recurring-incomes' && opts.method === 'POST') {
      const body = opts.body as { categoryId: number | null } & Omit<RecurringIncome, 'id' | 'categoryName'>
      const categoryName = categories.find((c) => c.id === body.categoryId)?.name ?? null
      const created: RecurringIncome = { id: incomes.length + 1, categoryName, ...body }
      incomes = [...incomes, created]
      return created
    }
    if (p.startsWith('/api/recurring-incomes/') && opts.method === 'PUT') {
      const id = Number(p.split('/').pop())
      incomes = incomes.map((i) =>
        i.id === id ? { ...i, ...(opts.body as Omit<RecurringIncome, 'id'>) } : i,
      )
      return incomes.find((i) => i.id === id)
    }
    if (p.startsWith('/api/recurring-incomes/') && opts.method === 'DELETE') {
      const id = Number(p.split('/').pop())
      incomes = incomes.filter((i) => i.id !== id)
      return undefined
    }
    return undefined
  })

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContextTestProvider isAuthenticated>
        <ConfirmProvider>
          <RecurringIncomesSection categories={categories} />
        </ConfirmProvider>
      </AuthContextTestProvider>
    </QueryClientProvider>,
  )
}

describe('RecurringIncomesSection', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('renders an existing recurring income', async () => {
    renderSection({ incomes: [income] })

    const row = (await screen.findByText(/Salaire/)).closest('li')!
    expect(within(row).getByText(/Mensuelle/)).toBeInTheDocument()
  })

  it('shows an empty state when there are none', async () => {
    renderSection({ incomes: [] })

    expect(await screen.findByText("Aucun revenu récurrent pour l'instant.")).toBeInTheDocument()
  })

  it('creates a recurring income', async () => {
    renderSection({ incomes: [] })
    await screen.findByText("Aucun revenu récurrent pour l'instant.")

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: '+ Ajouter un revenu' }))
    await user.type(screen.getByLabelText('Libellé'), 'Salaire')
    await user.type(screen.getByLabelText('Montant'), '2200')
    await user.selectOptions(screen.getByLabelText('Catégorie'), '10')
    await user.selectOptions(screen.getByLabelText('Fréquence'), 'Monthly')
    await user.type(screen.getByLabelText('Date de début'), '2026-02-01')

    await user.click(screen.getByRole('button', { name: 'Ajouter le revenu' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/recurring-incomes',
        expect.objectContaining({
          method: 'POST',
          body: {
            label: 'Salaire',
            amount: 2200,
            categoryId: 10,
            frequency: 'Monthly',
            startDate: '2026-02-01',
            endDate: null,
          },
        }),
      ),
    )
    expect(await screen.findByText(/Salaire/)).toBeInTheDocument()
  })

  it('deletes a recurring income after confirmation', async () => {
    renderSection({ incomes: [income] })
    await screen.findByText(/Salaire/)

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Supprimer' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Supprimer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/recurring-incomes/1',
        expect.objectContaining({ method: 'DELETE' }),
      ),
    )
    expect(await screen.findByText("Aucun revenu récurrent pour l'instant.")).toBeInTheDocument()
  })
})
