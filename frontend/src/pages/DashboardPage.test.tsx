import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, ApiError } from '../api/client'
import type { BalancePoint, BankAccount, CategoryBreakdownEntry, MonthlyComparisonEntry } from '../api/types'
import { AuthContextTestProvider } from '../auth/testUtils'
import { DashboardPage } from './DashboardPage'

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return { ...actual, apiFetch: vi.fn() }
})

const account: BankAccount = { id: 1, bankName: 'BoursoBank', label: 'Compte courant', iban: null }

const balancePoints: BalancePoint[] = [
  { date: '2026-08-01', netChange: 100, cumulativeBalance: 100 },
  { date: '2026-08-15', netChange: -30, cumulativeBalance: 70 },
]

const breakdown: CategoryBreakdownEntry[] = [
  { categoryId: 1, categoryName: 'Alimentation', amount: 200 },
  { categoryId: 2, categoryName: 'Transport', amount: 50 },
]

const monthly: MonthlyComparisonEntry[] = Array.from({ length: 12 }, (_, i) => ({
  month: `2026-${String(i + 1).padStart(2, '0')}-01`,
  income: i === 7 ? 2000 : 0,
  expense: i === 7 ? 250 : 0,
  net: i === 7 ? 1750 : 0,
}))

function mockApiRouter(
  overrides: {
    accounts?: BankAccount[]
    balance?: BalancePoint[] | ApiError
    breakdown?: CategoryBreakdownEntry[] | ApiError
    monthly?: MonthlyComparisonEntry[]
  } = {},
) {
  vi.mocked(apiFetch).mockImplementation(async (path: unknown) => {
    const p = path as string
    if (p === '/api/bank-accounts') return overrides.accounts ?? [account]
    if (p.startsWith('/api/dashboard/balance-evolution')) {
      const v = overrides.balance ?? balancePoints
      if (v instanceof ApiError) throw v
      return v
    }
    if (p.startsWith('/api/dashboard/category-breakdown')) {
      const v = overrides.breakdown ?? breakdown
      if (v instanceof ApiError) throw v
      return v
    }
    if (p.startsWith('/api/dashboard/monthly-comparison')) return overrides.monthly ?? monthly
    throw new Error(`Unhandled path in test: ${p}`)
  })
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContextTestProvider isAuthenticated>
        <MemoryRouter>
          <DashboardPage />
        </MemoryRouter>
      </AuthContextTestProvider>
    </QueryClientProvider>,
  )
}

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders the three chart cards with data from the API', async () => {
    mockApiRouter()

    renderPage()

    expect(await screen.findByText('Évolution du solde cumulé')).toBeInTheDocument()
    expect(screen.getByText('Dépenses par catégorie')).toBeInTheDocument()
    expect(screen.getByText(/Comparatif mensuel 2026/)).toBeInTheDocument()
    expect(await screen.findByText('Alimentation')).toBeInTheDocument()
    expect(screen.getByText('Transport')).toBeInTheDocument()
    expect(screen.getByText('Aoû')).toBeInTheDocument()
  })

  it('computes KPI tiles (income derived as net change + total expense) in the consolidated view', async () => {
    mockApiRouter()

    renderPage()

    // netTotal = 100 - 30 = 70; expenseTotal = 200 + 50 = 250; income = 70 + 250 = 320.
    // { selector: 'p' } disambiguates the stat-tile label from the "Revenus"/"Dépenses"
    // legend swatches MonthlyComparisonChart renders as <span>s with the same text.
    expect(await screen.findByText('Revenus', { selector: 'p' })).toBeInTheDocument()
    // { selector: 'p' } again: 70,00 € here happens to also match the balance chart's
    // own end-of-line direct label (an SVG <text>, coincidentally the same value since
    // this fixture's starting balance is 0).
    expect(screen.getByText(/^320,00.€$/, { selector: 'p' })).toBeInTheDocument()
    expect(screen.getByText(/^250,00.€$/, { selector: 'p' })).toBeInTheDocument()
    expect(screen.getByText(/^70,00.€$/, { selector: 'p' })).toBeInTheDocument()
  })

  it('hides the derived Revenus tile and refetches once a single account is selected', async () => {
    mockApiRouter()
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Revenus', { selector: 'p' })

    await user.selectOptions(screen.getByLabelText('Compte'), '1')

    expect(screen.queryByText('Revenus', { selector: 'p' })).not.toBeInTheDocument()
    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        expect.stringMatching(/\/api\/dashboard\/balance-evolution\?.*bankAccountId=1/),
        expect.objectContaining({}),
      ),
    )
  })

  it('refetches with the current month range when the "Ce mois-ci" preset is selected', async () => {
    mockApiRouter()
    const user = userEvent.setup()

    renderPage()
    await screen.findByText('Évolution du solde cumulé')

    await user.click(screen.getByRole('button', { name: 'Ce mois-ci' }))

    const now = new Date()
    const firstOfMonth = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`
    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        expect.stringContaining(`fromDate=${firstOfMonth}`),
        expect.objectContaining({}),
      ),
    )
  })

  it('shows an error for one chart without breaking the others', async () => {
    mockApiRouter({ breakdown: new ApiError(500, 'Server error', 'Une erreur est survenue.') })

    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent('Une erreur est survenue.')
    expect(screen.getByText('Aoû')).toBeInTheDocument()
  })
})
