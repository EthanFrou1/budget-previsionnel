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
}

function mockApiRouter(overrides: {
  transactions?: TransactionPageType
  accounts?: BankAccount[]
  categories?: Category[]
  rules?: unknown[]
} = {}) {
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
      () => expect(apiFetch).toHaveBeenCalledWith(expect.stringContaining('search=netflix'), expect.objectContaining({})),
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
})
