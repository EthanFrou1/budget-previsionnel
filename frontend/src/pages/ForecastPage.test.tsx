import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, ApiError } from '../api/client'
import type { MonthlyForecast } from '../api/types'
import { AuthContextTestProvider } from '../auth/testUtils'
import { ForecastPage } from './ForecastPage'

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return { ...actual, apiFetch: vi.fn() }
})

const now = new Date()
const currentYear = now.getFullYear()
const currentMonth = `${currentYear}-${String(now.getMonth() + 1).padStart(2, '0')}-01`

function buildAnnual(year: number): MonthlyForecast[] {
  return Array.from({ length: 12 }, (_, i) => ({
    month: `${year}-${String(i + 1).padStart(2, '0')}-01`,
    categoryLines: [],
    loanPayments: 0,
    total: (i + 1) * 100,
  }))
}

function renderPage(overrides: { monthly?: MonthlyForecast; annual?: MonthlyForecast[] } = {}) {
  const monthly: MonthlyForecast = overrides.monthly ?? {
    month: currentMonth,
    categoryLines: [],
    loanPayments: 0,
    total: 0,
  }
  const annual = overrides.annual ?? buildAnnual(currentYear)

  vi.mocked(apiFetch).mockImplementation(async (path: unknown) => {
    const p = path as string
    if (p === '/api/categories') return []
    if (p === `/api/forecast/monthly?month=${currentMonth}`) return monthly
    if (p.startsWith('/api/forecast/monthly?month=')) {
      const month = p.split('=')[1]
      return { month, categoryLines: [], loanPayments: 0, total: 0 }
    }
    if (p === `/api/forecast/annual?year=${currentYear}`) return annual
    if (p.startsWith('/api/forecast/annual?year=')) return []
    if (p.startsWith('/api/budgets')) return []
    if (p === '/api/recurring-expenses') return []
    return undefined
  })

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContextTestProvider isAuthenticated>
        <MemoryRouter>
          <ForecastPage />
        </MemoryRouter>
      </AuthContextTestProvider>
    </QueryClientProvider>,
  )
}

describe('ForecastPage', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('renders the monthly forecast with category lines, loan payments and total', async () => {
    renderPage({
      monthly: {
        month: currentMonth,
        categoryLines: [
          { categoryId: 10, categoryLabel: 'Logement', amount: 900, source: 'Budget' },
          { categoryId: 11, categoryLabel: 'Abonnements', amount: 40, source: 'RecurringExpense' },
        ],
        loanPayments: 250,
        total: 1190,
      },
    })

    expect(await screen.findByText('Logement')).toBeInTheDocument()
    expect(screen.getByText('Budget')).toBeInTheDocument()
    expect(screen.getByText('Abonnements')).toBeInTheDocument()
    expect(screen.getByText('Récurrent')).toBeInTheDocument()
    expect(screen.getByText('Remboursements de crédits')).toBeInTheDocument()
    expect(screen.getByText(/1\s*190,00/)).toBeInTheDocument()
  })

  it('shows an empty state when nothing is forecast for the month', async () => {
    renderPage()

    expect(await screen.findByText('Aucune dépense prévue ce mois-ci.')).toBeInTheDocument()
  })

  it('switches to the annual view and lists a total per month', async () => {
    renderPage()
    await screen.findByText('Aucune dépense prévue ce mois-ci.')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Annuel' }))

    expect(await screen.findByText('Prévisionnel annuel')).toBeInTheDocument()
    const marchRow = screen.getByText(/mars/).closest('tr')!
    expect(within(marchRow).getByText(/300,00/)).toBeInTheDocument()
    // Total annuel = 100+200+...+1200 = 7800
    expect(screen.getByText(/7\s*800,00/)).toBeInTheDocument()
  })

  it('jumps back to the monthly detail from the annual view', async () => {
    renderPage()
    await screen.findByText('Aucune dépense prévue ce mois-ci.')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Annuel' }))
    const marchRow = (await screen.findByText(/mars/)).closest('tr')!
    await user.click(within(marchRow).getByRole('button', { name: 'Détail' }))

    expect(screen.getByLabelText('Mois')).toHaveValue(`${currentYear}-03`)
    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        `/api/forecast/monthly?month=${currentYear}-03-01`,
        expect.objectContaining({}),
      ),
    )
  })

  it('shows an error message when the forecast call fails', async () => {
    vi.mocked(apiFetch).mockImplementation(async (path: unknown) => {
      const p = path as string
      if (p.startsWith('/api/forecast/monthly')) {
        throw new ApiError(500, 'Server error', 'Une erreur est survenue.')
      }
      if (p === '/api/categories') return []
      if (p.startsWith('/api/budgets')) return []
      if (p === '/api/recurring-expenses') return []
      return undefined
    })

    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <AuthContextTestProvider isAuthenticated>
          <MemoryRouter>
            <ForecastPage />
          </MemoryRouter>
        </AuthContextTestProvider>
      </QueryClientProvider>,
    )

    expect(await screen.findByRole('alert')).toHaveTextContent('Une erreur est survenue.')
  })
})
