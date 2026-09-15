import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, ApiError } from '../api/client'
import type { Loan } from '../api/types'
import { AuthContextTestProvider } from '../auth/testUtils'
import { LoansPage } from './LoansPage'

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return { ...actual, apiFetch: vi.fn() }
})

function renderPage(overrides: { loans?: Loan[] } = {}) {
  let loans = overrides.loans ?? []

  vi.mocked(apiFetch).mockImplementation(async (path: unknown, options?: unknown) => {
    const p = path as string
    const opts = (options ?? {}) as { method?: string; body?: unknown }

    if (p === '/api/loans' && (!opts.method || opts.method === 'GET')) {
      return loans
    }
    if (p === '/api/loans' && opts.method === 'POST') {
      const created = { id: loans.length + 1, ...(opts.body as Omit<Loan, 'id'>) }
      loans = [...loans, created]
      return created
    }
    if (p.startsWith('/api/loans/') && opts.method === 'PUT') {
      const id = Number(p.split('/').pop())
      loans = loans.map((l) => (l.id === id ? { ...l, ...(opts.body as Omit<Loan, 'id'>) } : l))
      return loans.find((l) => l.id === id)
    }
    if (p.startsWith('/api/loans/') && opts.method === 'DELETE') {
      const id = Number(p.split('/').pop())
      loans = loans.filter((l) => l.id !== id)
      return undefined
    }
    return undefined
  })

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContextTestProvider isAuthenticated>
        <MemoryRouter>
          <LoansPage />
        </MemoryRouter>
      </AuthContextTestProvider>
    </QueryClientProvider>,
  )
}

const loan: Loan = {
  id: 1,
  label: 'Prêt auto',
  principalAmount: 10000,
  remainingAmount: 4000,
  interestRate: 2.5,
  monthlyPayment: 250,
  endDate: '2027-06-01',
}

describe('LoansPage', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('renders a loan with its repayment progress', async () => {
    renderPage({ loans: [loan] })

    const card = (await screen.findByText('Prêt auto')).closest('div')!
    expect(within(card).getByText(/4\s*000,00.€ \/ 10\s*000,00.€/)).toBeInTheDocument()
    expect(within(card).getByText(/2,5%/)).toBeInTheDocument()
    expect(screen.getByText('60% remboursé')).toBeInTheDocument()
  })

  it('shows an empty state when there are no loans', async () => {
    renderPage({ loans: [] })

    expect(await screen.findByText("Aucun crédit pour l'instant.")).toBeInTheDocument()
  })

  it('creates a loan and refreshes the list', async () => {
    renderPage({ loans: [] })
    await screen.findByText("Aucun crédit pour l'instant.")

    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Libellé'), 'Prêt travaux')
    await user.type(screen.getByLabelText('Montant emprunté'), '5000')
    await user.type(screen.getByLabelText('Capital restant dû'), '5000')
    await user.type(screen.getByLabelText('Mensualité'), '150')
    await user.type(screen.getByLabelText('Échéance finale'), '2028-01-01')

    await user.click(screen.getByRole('button', { name: 'Ajouter' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/loans',
        expect.objectContaining({
          method: 'POST',
          body: {
            label: 'Prêt travaux',
            principalAmount: 5000,
            remainingAmount: 5000,
            interestRate: 0,
            monthlyPayment: 150,
            endDate: '2028-01-01',
          },
        }),
      ),
    )
    expect(await screen.findByText('Prêt travaux')).toBeInTheDocument()
  })

  it('deletes a loan after confirmation', async () => {
    renderPage({ loans: [loan] })
    await screen.findByText('Prêt auto')
    vi.stubGlobal('confirm', vi.fn().mockReturnValue(true))

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Supprimer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith('/api/loans/1', expect.objectContaining({ method: 'DELETE' })),
    )
    expect(await screen.findByText("Aucun crédit pour l'instant.")).toBeInTheDocument()
  })

  it('shows an error message when the API call fails', async () => {
    vi.mocked(apiFetch).mockRejectedValueOnce(new ApiError(500, 'Server error', 'Une erreur est survenue.'))

    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent('Une erreur est survenue.')
  })
})
