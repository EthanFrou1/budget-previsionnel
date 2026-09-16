import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from '../api/client'
import type { BankAccount, Category, Transaction, TransactionPage as TransactionPageType } from '../api/types'
import { AuthContextTestProvider } from '../auth/testUtils'
import { TransactionsPage } from './TransactionsPage'

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return { ...actual, apiFetch: vi.fn() }
})

const account: BankAccount = { id: 1, bankName: 'BoursoBank', label: 'Compte courant', iban: null }
const foodCategory: Category = {
  id: 10,
  name: 'Alimentation',
  icon: null,
  color: null,
  parentCategoryId: null,
  isSystemDefault: true,
  isOwnedByCurrentUser: false,
}
const transportCategory: Category = {
  id: 11,
  name: 'Transport',
  icon: null,
  color: null,
  parentCategoryId: null,
  isSystemDefault: true,
  isOwnedByCurrentUser: false,
}

const transaction: Transaction = {
  id: 100,
  bankAccountId: 1,
  bankAccountLabel: 'Compte courant',
  date: '2026-08-31',
  rawLabel: 'CARTE EXAMPLE SHOP',
  cleanedLabel: 'Example Shop',
  amount: -10.99,
  categoryId: null,
  categoryName: null,
  isInternalTransfer: false,
  notes: null,
}

const incomeTransaction: Transaction = {
  ...transaction,
  id: 101,
  cleanedLabel: 'Salaire',
  amount: 1500,
}

const internalTransferTransaction: Transaction = {
  ...transaction,
  id: 102,
  cleanedLabel: 'Virement épargne',
  isInternalTransfer: true,
}

function mockApiRouter(
  overrides: {
    transactions?: TransactionPageType
    accounts?: BankAccount[]
    categories?: Category[]
    rules?: unknown[]
  } = {},
) {
  vi.mocked(apiFetch).mockImplementation(async (path: unknown, ...rest: unknown[]) => {
    const p = path as string
    if (p.startsWith('/api/transactions?')) {
      return overrides.transactions ?? { items: [], totalCount: 0, page: 1, pageSize: 50 }
    }
    if (p === '/api/bank-accounts') {
      return overrides.accounts ?? [account]
    }
    if (p === '/api/categories') {
      return overrides.categories ?? [foodCategory, transportCategory]
    }
    if (p === '/api/category-rules') {
      return overrides.rules ?? []
    }
    // Mutations (PUT category, POST/DELETE rule) - just no-op, tests assert the call itself.
    void rest
    return undefined
  })
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContextTestProvider isAuthenticated>
        <MemoryRouter>
          <TransactionsPage />
        </MemoryRouter>
      </AuthContextTestProvider>
    </QueryClientProvider>,
  )
}

describe('TransactionsPage', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('renders transactions with formatted date, amount and category selector', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })

    renderPage()

    expect(await screen.findByText('Example Shop')).toBeInTheDocument()
    expect(screen.getByText('31/08/2026')).toBeInTheDocument()
    // The space between amount and symbol can be a narrow no-break space depending on the
    // ICU data available - match loosely rather than hardcode a specific whitespace byte.
    expect(screen.getByText(/^-10,99.€$/)).toBeInTheDocument()
    expect(screen.getByLabelText('Catégorie de Example Shop')).toHaveValue('')
  })

  it('shows an empty state when no transaction matches the filters', async () => {
    mockApiRouter({ transactions: { items: [], totalCount: 0, page: 1, pageSize: 50 } })

    renderPage()

    expect(await screen.findByText('Aucune transaction ne correspond à ces filtres.')).toBeInTheDocument()
  })

  it('refetches with the account filter when a bank account is selected', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    await user.selectOptions(screen.getByLabelText('Compte'), '1')

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        expect.stringContaining('bankAccountId=1'),
        expect.objectContaining({}),
      ),
    )
  })

  it('debounces the search box before refetching', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')
    const callsBeforeTyping = vi.mocked(apiFetch).mock.calls.length

    await user.type(screen.getByLabelText('Recherche'), 'netflix')

    // Debounce (400ms) means no immediate extra call from typing itself.
    expect(vi.mocked(apiFetch).mock.calls.length).toBe(callsBeforeTyping)

    await waitFor(
      () =>
        expect(apiFetch).toHaveBeenCalledWith(
          expect.stringContaining('search=netflix'),
          expect.objectContaining({}),
        ),
      { timeout: 2000 },
    )
  })

  it('sends a PUT request when a transaction is (re)categorized', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    await user.selectOptions(screen.getByLabelText('Catégorie de Example Shop'), '10')

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/transactions/100/category',
        expect.objectContaining({ method: 'PUT', body: { categoryId: 10 } }),
      ),
    )
  })

  it('saves a personal note on blur, but only when it actually changed', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    const noteInput = screen.getByLabelText('Note pour Example Shop')
    await user.click(noteInput)
    await user.tab() // blur without typing anything - must not fire a no-op PUT
    expect(apiFetch).not.toHaveBeenCalledWith(
      '/api/transactions/100/notes',
      expect.objectContaining({ method: 'PUT' }),
    )

    await user.type(noteInput, 'Remboursé par Paul')
    await user.tab()

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/transactions/100/notes',
        expect.objectContaining({ method: 'PUT', body: { notes: 'Remboursé par Paul' } }),
      ),
    )
  })

  it('applies the "this month" quick date preset', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    await user.selectOptions(screen.getByLabelText('Période'), 'month')

    const now = new Date()
    const firstOfMonth = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`
    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`fromDate=${firstOfMonth}`),
        expect.objectContaining({}),
      ),
    )
  })

  it('hides the custom date range fields until "Personnalisé" is selected', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    expect(screen.queryByLabelText('Du')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Au')).not.toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Période'), 'custom')

    expect(screen.getByLabelText('Du')).toBeInTheDocument()
    expect(screen.getByLabelText('Au')).toBeInTheDocument()
  })

  it('applies a custom date range once "Personnalisé" reveals the fields', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    await user.selectOptions(screen.getByLabelText('Période'), 'custom')
    await user.type(screen.getByLabelText('Du'), '2026-01-01')

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        expect.stringContaining('fromDate=2026-01-01'),
        expect.objectContaining({}),
      ),
    )
  })

  it('resets the date range when "Toutes les périodes" is selected again', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    await user.selectOptions(screen.getByLabelText('Période'), 'month')
    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        expect.stringContaining('fromDate='),
        expect.objectContaining({}),
      ),
    )

    await user.selectOptions(screen.getByLabelText('Période'), '')

    expect(screen.queryByLabelText('Du')).not.toBeInTheDocument()
    await waitFor(() => {
      const lastCall = vi.mocked(apiFetch).mock.calls.at(-1)!
      expect(lastCall[0]).not.toContain('fromDate=')
    })
  })

  it('paginates using the totalCount returned by the API', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 120, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    expect(screen.getByText('Page 1 / 3 (120 transaction(s))')).toBeInTheDocument()
    const previousButton = screen.getByRole('button', { name: 'Précédent' })
    expect(previousButton).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Suivant' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(expect.stringContaining('page=2'), expect.objectContaining({})),
    )
  })

  it('defaults to sorting by date descending', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })

    renderPage()
    await screen.findByText('Example Shop')

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        expect.stringMatching(/sortBy=date.*sortDirection=desc/),
        expect.objectContaining({}),
      ),
    )
    expect(screen.getByRole('columnheader', { name: /Date/ })).toHaveAttribute('aria-sort', 'descending')
  })

  it('sorts by amount ascending on first click, then flips to descending on a second click', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    await user.click(screen.getByRole('button', { name: /Montant/ }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        expect.stringMatching(/sortBy=amount.*sortDirection=asc/),
        expect.objectContaining({}),
      ),
    )
    expect(screen.getByRole('columnheader', { name: /Montant/ })).toHaveAttribute('aria-sort', 'ascending')
    // Date is no longer the active sort column once another column takes over.
    expect(screen.getByRole('columnheader', { name: /Date/ })).toHaveAttribute('aria-sort', 'none')

    await user.click(screen.getByRole('button', { name: /Montant/ }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        expect.stringMatching(/sortBy=amount.*sortDirection=desc/),
        expect.objectContaining({}),
      ),
    )
    expect(screen.getByRole('columnheader', { name: /Montant/ })).toHaveAttribute('aria-sort', 'descending')
  })

  it('resets to page 1 when the sort column changes', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 120, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')
    await user.click(screen.getByRole('button', { name: 'Suivant' }))
    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(expect.stringContaining('page=2'), expect.objectContaining({})),
    )

    await user.click(screen.getByRole('button', { name: /Libellé/ }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        expect.stringMatching(/page=1.*sortBy=label/),
        expect.objectContaining({}),
      ),
    )
  })

  it('shows an error message when the transactions request fails', async () => {
    const { ApiError } = await vi.importActual<typeof import('../api/client')>('../api/client')
    vi.mocked(apiFetch).mockImplementation(async (path: unknown) => {
      const p = path as string
      if (p.startsWith('/api/transactions?')) {
        throw new ApiError(500, 'Server error', 'Une erreur est survenue.')
      }
      if (p === '/api/bank-accounts') return [account]
      if (p === '/api/categories') return [foodCategory, transportCategory]
      if (p === '/api/category-rules') return []
      return undefined
    })

    renderPage()

    const alert = await screen.findByRole('alert')
    expect(within(alert).getByText('Une erreur est survenue.')).toBeInTheDocument()
  })

  it('offers the "mark as recurring" shortcut on both expenses and income, but not internal transfers', async () => {
    mockApiRouter({
      transactions: {
        items: [transaction, incomeTransaction, internalTransferTransaction],
        totalCount: 3,
        page: 1,
        pageSize: 50,
      },
    })

    renderPage()
    await screen.findByText('Example Shop')

    expect(
      screen.getByRole('button', { name: 'Marquer Example Shop comme dépense récurrente' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Marquer Salaire comme revenu récurrent' })).toBeInTheDocument()
    expect(screen.getByText('Virement épargne')).toBeInTheDocument()
    // The internal-transfer row renders no "mark as recurring" button at all.
    expect(
      screen.queryByRole('button', { name: /Marquer Virement épargne/ }),
    ).not.toBeInTheDocument()
  })

  it('creates a recurring expense pre-filled from the transaction on confirm', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    await user.click(screen.getByRole('button', { name: 'Marquer Example Shop comme dépense récurrente' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Fréquence'), 'Yearly')
    await user.click(screen.getByRole('button', { name: 'Créer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/recurring-expenses',
        expect.objectContaining({
          method: 'POST',
          body: {
            label: 'Example Shop',
            amount: 10.99,
            categoryId: null,
            frequency: 'Yearly',
            startDate: '2026-08-31',
            endDate: null,
          },
        }),
      ),
    )
    expect(await screen.findByText('Ajoutée ✓')).toBeInTheDocument()
  })

  it('creates a recurring income pre-filled from an income transaction on confirm', async () => {
    mockApiRouter({ transactions: { items: [incomeTransaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Salaire')

    await user.click(screen.getByRole('button', { name: 'Marquer Salaire comme revenu récurrent' }))
    expect(screen.getByRole('heading', { name: 'Marquer comme revenu récurrent' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Créer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/recurring-incomes',
        expect.objectContaining({
          method: 'POST',
          body: {
            label: 'Salaire',
            amount: 1500,
            categoryId: null,
            frequency: 'Monthly',
            startDate: '2026-08-31',
            endDate: null,
          },
        }),
      ),
    )
    expect(await screen.findByText('Ajoutée ✓')).toBeInTheDocument()
  })

  it('cancels the frequency picker without creating anything', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    await user.click(screen.getByRole('button', { name: 'Marquer Example Shop comme dépense récurrente' }))
    await user.click(screen.getByRole('button', { name: 'Annuler' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Marquer Example Shop comme dépense récurrente' }),
    ).toBeInTheDocument()
    expect(apiFetch).not.toHaveBeenCalledWith('/api/recurring-expenses', expect.anything())
  })

  it('closes the dialog on Escape without creating anything', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    await user.click(screen.getByRole('button', { name: 'Marquer Example Shop comme dépense récurrente' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(apiFetch).not.toHaveBeenCalledWith('/api/recurring-expenses', expect.anything())
  })

  it('closes the dialog on a backdrop click without creating anything', async () => {
    mockApiRouter({ transactions: { items: [transaction], totalCount: 1, page: 1, pageSize: 50 } })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    await user.click(screen.getByRole('button', { name: 'Marquer Example Shop comme dépense récurrente' }))
    const dialog = screen.getByRole('dialog')

    // The backdrop is the dialog's own positioning parent - clicking it directly (not the
    // panel inside) should close without creating anything.
    await user.click(dialog.parentElement!)

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(apiFetch).not.toHaveBeenCalledWith('/api/recurring-expenses', expect.anything())
  })

  it('shows an error inline when creating the recurring expense fails', async () => {
    const { ApiError } = await vi.importActual<typeof import('../api/client')>('../api/client')
    vi.mocked(apiFetch).mockImplementation(async (path: unknown, ...rest: unknown[]) => {
      const p = path as string
      if (p.startsWith('/api/transactions?')) {
        return { items: [transaction], totalCount: 1, page: 1, pageSize: 50 }
      }
      if (p === '/api/bank-accounts') return [account]
      if (p === '/api/categories') return [foodCategory, transportCategory]
      if (p === '/api/category-rules') return []
      if (p === '/api/recurring-expenses') {
        throw new ApiError(400, 'Bad request', 'Une erreur est survenue.')
      }
      void rest
      return undefined
    })
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Example Shop')

    await user.click(screen.getByRole('button', { name: 'Marquer Example Shop comme dépense récurrente' }))
    await user.click(screen.getByRole('button', { name: 'Créer' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Une erreur est survenue.')
    expect(screen.getByRole('button', { name: 'Créer' })).toBeInTheDocument()
  })
})
