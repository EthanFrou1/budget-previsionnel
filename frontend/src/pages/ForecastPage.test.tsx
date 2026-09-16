import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, ApiError } from '../api/client'
import type { CalendarEntry, MonthlyForecast } from '../api/types'
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
    totalRecurringIncome: 0,
    netBalance: -(i + 1) * 100,
  }))
}

function renderPage(
  overrides: {
    monthly?: MonthlyForecast
    annual?: MonthlyForecast[]
    calendarMonthly?: CalendarEntry[]
    calendarAnnual?: CalendarEntry[]
  } = {},
) {
  const monthly: MonthlyForecast = overrides.monthly ?? {
    month: currentMonth,
    categoryLines: [],
    loanPayments: 0,
    total: 0,
    totalRecurringIncome: 0,
    netBalance: 0,
  }
  const annual = overrides.annual ?? buildAnnual(currentYear)
  const calendarMonthly = overrides.calendarMonthly ?? []
  const calendarAnnual = overrides.calendarAnnual ?? []

  vi.mocked(apiFetch).mockImplementation(async (path: unknown) => {
    const p = path as string
    if (p === '/api/categories') return []
    if (p === `/api/forecast/monthly?month=${currentMonth}`) return monthly
    if (p.startsWith('/api/forecast/monthly?month=')) {
      const month = p.split('=')[1]
      return { month, categoryLines: [], loanPayments: 0, total: 0, totalRecurringIncome: 0, netBalance: 0 }
    }
    if (p === `/api/forecast/annual?year=${currentYear}`) return annual
    if (p.startsWith('/api/forecast/annual?year=')) return []
    if (p === `/api/calendar/monthly?month=${currentMonth}`) return calendarMonthly
    if (p.startsWith('/api/calendar/monthly?month=')) return []
    if (p === `/api/calendar/annual?year=${currentYear}`) return calendarAnnual
    if (p.startsWith('/api/calendar/annual?year=')) return []
    if (p.startsWith('/api/budgets')) return []
    if (p === '/api/recurring-expenses') return []
    if (p === '/api/recurring-incomes') return []
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
        totalRecurringIncome: 0,
        netBalance: -1190,
      },
    })

    expect(await screen.findByText('Logement')).toBeInTheDocument()
    expect(screen.getByText('Budget')).toBeInTheDocument()
    expect(screen.getByText('Abonnements')).toBeInTheDocument()
    expect(screen.getByText('Récurrent')).toBeInTheDocument()
    expect(screen.getByText('Remboursements de crédits')).toBeInTheDocument()
    expect(screen.getByText(/1\s*190,00/)).toBeInTheDocument()
    // No recurring income this month - the "Revenus"/"Solde net" rows don't render at all.
    expect(screen.queryByText('Revenus récurrents prévus')).not.toBeInTheDocument()
  })

  it('shows recurring income and the net balance when there is recurring income', async () => {
    renderPage({
      monthly: {
        month: currentMonth,
        categoryLines: [{ categoryId: 10, categoryLabel: 'Logement', amount: 900, source: 'Budget' }],
        loanPayments: 0,
        total: 900,
        totalRecurringIncome: 2200,
        netBalance: 1300,
      },
    })

    expect(await screen.findByText('Revenus récurrents prévus')).toBeInTheDocument()
    expect(screen.getByText(/2\s*200,00/)).toBeInTheDocument()
    expect(screen.getByText('Solde net prévisionnel')).toBeInTheDocument()
    expect(screen.getByText(/1\s*300,00/)).toBeInTheDocument()
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
      if (p === '/api/recurring-incomes') return []
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

  it('switches to the calendar view and shows a recurring expense and a loan on their dates', async () => {
    const day = String(now.getDate()).padStart(2, '0')
    const todayIso = `${currentMonth.slice(0, 7)}-${day}`

    renderPage({
      calendarMonthly: [
        {
          date: todayIso,
          label: 'Netflix',
          amount: 15.99,
          type: 'RecurringExpense',
          isLastOccurrence: false,
        },
        { date: todayIso, label: 'Prêt auto', amount: 300, type: 'Loan', isLastOccurrence: true },
        { date: todayIso, label: 'Salaire', amount: 2200, type: 'RecurringIncome', isLastOccurrence: false },
      ],
    })
    await screen.findByText('Aucune dépense prévue ce mois-ci.')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Calendrier' }))

    expect(await screen.findByText('Netflix')).toBeInTheDocument()
    expect(screen.getByText('Prêt auto')).toBeInTheDocument()
    expect(screen.getByText('Salaire')).toBeInTheDocument()
    expect(screen.getByText('(dernière)')).toBeInTheDocument()
  })

  it('shows an empty state on the calendar month view when nothing is due', async () => {
    renderPage()
    await screen.findByText('Aucune dépense prévue ce mois-ci.')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Calendrier' }))

    expect(await screen.findByText('Aucune échéance ce mois-ci.')).toBeInTheDocument()
  })

  it('navigates to the next month on the calendar view', async () => {
    renderPage()
    await screen.findByText('Aucune dépense prévue ce mois-ci.')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Calendrier' }))
    await screen.findByText('Aucune échéance ce mois-ci.')
    await user.click(screen.getByRole('button', { name: 'Mois suivant' }))

    const nextMonth = `${currentYear}-${String(now.getMonth() + 2).padStart(2, '0')}`
    await waitFor(() => expect(screen.getByLabelText('Mois')).toHaveValue(nextMonth))
  })

  it('switches to the calendar annual view and groups entries by month', async () => {
    renderPage({
      calendarAnnual: [
        {
          date: `${currentYear}-03-05`,
          label: 'Loyer',
          amount: 800,
          type: 'RecurringExpense',
          isLastOccurrence: false,
        },
      ],
    })
    await screen.findByText('Aucune dépense prévue ce mois-ci.')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Calendrier' }))
    await screen.findByText('Aucune échéance ce mois-ci.')
    await user.click(screen.getByRole('button', { name: 'Année' }))

    expect(await screen.findByText(/Loyer/)).toBeInTheDocument()
  })
})
