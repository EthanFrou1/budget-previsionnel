import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, ApiError } from '../api/client'
import type { BankAccount, SavingsGoal } from '../api/types'
import { AuthContextTestProvider } from '../auth/testUtils'
import { SavingsPage } from './SavingsPage'

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return { ...actual, apiFetch: vi.fn() }
})

const account: BankAccount = { id: 1, bankName: 'BoursoBank', label: 'Livret A', iban: null }

function renderPage(overrides: { goals?: SavingsGoal[]; accounts?: BankAccount[] } = {}) {
  let goals = overrides.goals ?? []
  const accounts = overrides.accounts ?? [account]

  vi.mocked(apiFetch).mockImplementation(async (path: unknown, options?: unknown) => {
    const p = path as string
    const opts = (options ?? {}) as { method?: string; body?: unknown }

    if (p === '/api/bank-accounts') {
      return accounts
    }
    if (p === '/api/savings-goals' && (!opts.method || opts.method === 'GET')) {
      return goals
    }
    if (p === '/api/savings-goals' && opts.method === 'POST') {
      const goal = { id: goals.length + 1, ...(opts.body as Omit<SavingsGoal, 'id'>) }
      goals = [...goals, goal]
      return goal
    }
    if (p.startsWith('/api/savings-goals/') && opts.method === 'PUT') {
      const id = Number(p.split('/').pop())
      goals = goals.map((g) => (g.id === id ? { ...g, ...(opts.body as Omit<SavingsGoal, 'id'>) } : g))
      return goals.find((g) => g.id === id)
    }
    if (p.startsWith('/api/savings-goals/') && opts.method === 'DELETE') {
      const id = Number(p.split('/').pop())
      goals = goals.filter((g) => g.id !== id)
      return undefined
    }
    return undefined
  })

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContextTestProvider isAuthenticated>
        <MemoryRouter>
          <SavingsPage />
        </MemoryRouter>
      </AuthContextTestProvider>
    </QueryClientProvider>,
  )
}

const goal: SavingsGoal = {
  id: 1,
  label: "Fonds d'urgence",
  targetAmount: 1000,
  currentAmount: 250,
  targetDate: '2026-12-01',
  linkedAccountId: 1,
}

describe('SavingsPage', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('renders a goal with its progress and linked account', async () => {
    renderPage({ goals: [goal] })

    const card = (await screen.findByText("Fonds d'urgence")).closest('div')!
    expect(within(card).getByText(/250,00/)).toBeInTheDocument()
    expect(within(card).getByText(/1\s*000,00/)).toBeInTheDocument()
    expect(screen.getByText('25% atteint')).toBeInTheDocument()
    expect(within(card).getByText(/Livret A/)).toBeInTheDocument()
  })

  it('shows an empty state when there are no goals', async () => {
    renderPage({ goals: [] })

    expect(await screen.findByText("Aucun objectif d'épargne pour l'instant.")).toBeInTheDocument()
  })

  it('hides the create form behind a toggle button until the user asks for it', async () => {
    renderPage({ goals: [] })

    await screen.findByText("Aucun objectif d'épargne pour l'instant.")
    expect(screen.queryByLabelText('Libellé')).not.toBeInTheDocument()

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: '+ Ajouter un objectif' }))
    expect(screen.getByLabelText('Libellé')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Annuler' }))
    expect(screen.queryByLabelText('Libellé')).not.toBeInTheDocument()
  })

  it('creates a goal, refreshes the list and collapses the form again', async () => {
    renderPage({ goals: [] })
    await screen.findByText("Aucun objectif d'épargne pour l'instant.")

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: '+ Ajouter un objectif' }))
    await user.type(screen.getByLabelText('Libellé'), 'Vacances')
    await user.type(screen.getByLabelText('Montant visé'), '2000')

    await user.click(screen.getByRole('button', { name: 'Ajouter' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/savings-goals',
        expect.objectContaining({
          method: 'POST',
          body: {
            label: 'Vacances',
            targetAmount: 2000,
            currentAmount: 0,
            targetDate: null,
            linkedAccountId: null,
          },
        }),
      ),
    )
    expect(await screen.findByText('Vacances')).toBeInTheDocument()
    expect(screen.queryByLabelText('Libellé')).not.toBeInTheDocument()
  })

  it('edits a goal in place', async () => {
    renderPage({ goals: [goal] })
    await screen.findByText("Fonds d'urgence")

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Modifier' }))

    // Scope to the edit form (identified by its "Enregistrer" button) in case a create
    // form is also open elsewhere on the page.
    const editForm = screen.getByRole('button', { name: 'Enregistrer' }).closest('form')!
    const currentAmountInput = within(editForm).getByLabelText('Montant actuel')
    await user.clear(currentAmountInput)
    await user.type(currentAmountInput, '500')
    await user.click(within(editForm).getByRole('button', { name: 'Enregistrer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/savings-goals/1',
        expect.objectContaining({ method: 'PUT', body: expect.objectContaining({ currentAmount: 500 }) }),
      ),
    )
    expect(await screen.findByText('50% atteint')).toBeInTheDocument()
  })

  it('deletes a goal after confirmation', async () => {
    renderPage({ goals: [goal] })
    await screen.findByText("Fonds d'urgence")

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Supprimer' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Supprimer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/savings-goals/1',
        expect.objectContaining({ method: 'DELETE' }),
      ),
    )
    expect(await screen.findByText("Aucun objectif d'épargne pour l'instant.")).toBeInTheDocument()
  })

  it('shows an error message when the API call fails', async () => {
    vi.mocked(apiFetch).mockRejectedValueOnce(new ApiError(500, 'Server error', 'Une erreur est survenue.'))

    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent('Une erreur est survenue.')
  })
})
