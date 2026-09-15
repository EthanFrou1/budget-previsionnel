import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, ApiError } from '../api/client'
import type { BankAccount } from '../api/types'
import { AuthContextTestProvider } from '../auth/testUtils'
import { AccountsPage } from './AccountsPage'

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return { ...actual, apiFetch: vi.fn() }
})

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContextTestProvider isAuthenticated>
        <MemoryRouter>
          <AccountsPage />
        </MemoryRouter>
      </AuthContextTestProvider>
    </QueryClientProvider>,
  )
}

const account: BankAccount = { id: 1, bankName: 'BoursoBank', label: 'Compte courant', iban: 'FR76...' }

describe('AccountsPage', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('renders the accounts returned by the API', async () => {
    vi.mocked(apiFetch).mockResolvedValueOnce([account])

    renderPage()

    const card = (await screen.findByText('Compte courant')).closest('li')!
    expect(within(card).getByText(/BoursoBank/)).toBeInTheDocument()
    expect(apiFetch).toHaveBeenCalledWith('/api/bank-accounts', expect.objectContaining({}))
  })

  it('shows an empty state when the user has no accounts', async () => {
    vi.mocked(apiFetch).mockResolvedValueOnce([])

    renderPage()

    expect(await screen.findByText("Aucun compte pour l'instant.")).toBeInTheDocument()
  })

  it('creates an account and refreshes the list', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([]) // initial list
      .mockResolvedValueOnce(account) // POST create
      .mockResolvedValueOnce([account]) // refetch after invalidation

    const user = userEvent.setup()
    renderPage()

    await screen.findByText("Aucun compte pour l'instant.")

    await user.type(screen.getByLabelText('Libellé'), 'Compte courant')
    await user.click(screen.getByRole('button', { name: 'Ajouter' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/bank-accounts',
        expect.objectContaining({
          method: 'POST',
          body: { bankName: 'BoursoBank', label: 'Compte courant', iban: null },
        }),
      ),
    )
    expect(await screen.findByText('Compte courant')).toBeInTheDocument()
  })

  it('deletes an account after confirmation', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account]) // initial list
      .mockResolvedValueOnce(undefined) // DELETE
      .mockResolvedValueOnce([]) // refetch after invalidation
    vi.stubGlobal('confirm', vi.fn().mockReturnValue(true))

    const user = userEvent.setup()
    renderPage()

    await screen.findByText('Compte courant')
    await user.click(screen.getByRole('button', { name: 'Supprimer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith('/api/bank-accounts/1', expect.objectContaining({ method: 'DELETE' })),
    )
    expect(await screen.findByText("Aucun compte pour l'instant.")).toBeInTheDocument()
  })

  it('does not delete when the confirmation is dismissed', async () => {
    vi.mocked(apiFetch).mockResolvedValueOnce([account])
    vi.stubGlobal('confirm', vi.fn().mockReturnValue(false))

    const user = userEvent.setup()
    renderPage()

    await screen.findByText('Compte courant')
    await user.click(screen.getByRole('button', { name: 'Supprimer' }))

    expect(apiFetch).toHaveBeenCalledTimes(1)
  })

  it('imports a CSV file and shows the resulting summary', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account]) // initial list
      .mockResolvedValueOnce({
        totalRowsParsed: 3,
        newTransactionsImported: 2,
        duplicatesSkipped: 1,
        internalTransfersDetected: 0,
      })

    const user = userEvent.setup()
    renderPage()

    const card = (await screen.findByText('Compte courant')).closest('li')!
    const fileInput = within(card).getByLabelText('Importer un relevé CSV') as HTMLInputElement
    const file = new File(['Date;Libellé\r\n2026-01-01;Test'], 'releve.csv', { type: 'text/csv' })
    await user.upload(fileInput, file)

    await user.click(within(card).getByRole('button', { name: 'Importer' }))

    expect(await within(card).findByText(/2 importée\(s\)/)).toBeInTheDocument()

    const [path, options] = vi.mocked(apiFetch).mock.calls[1]
    expect(path).toBe('/api/bank-accounts/1/import')
    expect((options as { body: FormData }).body).toBeInstanceOf(FormData)
  })

  it('shows an error message when the API call fails', async () => {
    vi.mocked(apiFetch).mockRejectedValueOnce(new ApiError(500, 'Server error', 'Une erreur est survenue.'))

    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent('Une erreur est survenue.')
  })
})
