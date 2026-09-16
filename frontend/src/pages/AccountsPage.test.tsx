import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, apiFetchBlob, ApiError } from '../api/client'
import type { BankAccount } from '../api/types'
import { AuthContextTestProvider } from '../auth/testUtils'
import { AccountsPage } from './AccountsPage'

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client')
  return { ...actual, apiFetch: vi.fn(), apiFetchBlob: vi.fn() }
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
    vi.mocked(apiFetchBlob).mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('renders the accounts returned by the API', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account])
      .mockResolvedValueOnce([]) // import history

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

  it('hides the create form behind a toggle button until the user asks for it', async () => {
    vi.mocked(apiFetch).mockResolvedValueOnce([])

    const user = userEvent.setup()
    renderPage()

    await screen.findByText("Aucun compte pour l'instant.")
    expect(screen.queryByLabelText('Libellé')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '+ Ajouter un compte' }))
    expect(screen.getByLabelText('Libellé')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Annuler' }))
    expect(screen.queryByLabelText('Libellé')).not.toBeInTheDocument()
  })

  it('creates an account, refreshes the list and collapses the form again', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([]) // initial list
      .mockResolvedValueOnce(account) // POST create
      .mockResolvedValueOnce([account]) // refetch after invalidation
      .mockResolvedValueOnce([]) // import history, once the card mounts

    const user = userEvent.setup()
    renderPage()

    await screen.findByText("Aucun compte pour l'instant.")
    await user.click(screen.getByRole('button', { name: '+ Ajouter un compte' }))

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
    expect(screen.queryByLabelText('Libellé')).not.toBeInTheDocument()
  })

  it('edits an account with each field properly labeled', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account]) // initial list
      .mockResolvedValueOnce([]) // import history, once the card mounts
      .mockResolvedValueOnce({ ...account, label: 'Compte perso' }) // PUT
      .mockResolvedValueOnce([{ ...account, label: 'Compte perso' }]) // refetch after invalidation

    const user = userEvent.setup()
    renderPage()

    const card = (await screen.findByText('Compte courant')).closest('li')!
    await user.click(within(card).getByRole('button', { name: 'Modifier' }))

    expect(within(card).getByLabelText('Banque')).toBeInTheDocument()
    expect(within(card).getByLabelText('IBAN (optionnel)')).toBeInTheDocument()
    const labelInput = within(card).getByLabelText('Libellé')
    await user.clear(labelInput)
    await user.type(labelInput, 'Compte perso')
    await user.click(within(card).getByRole('button', { name: 'Enregistrer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/bank-accounts/1',
        expect.objectContaining({
          method: 'PUT',
          body: { bankName: 'BoursoBank', label: 'Compte perso', iban: 'FR76...' },
        }),
      ),
    )
    expect(await screen.findByText('Compte perso')).toBeInTheDocument()
  })

  it('deletes an account after confirmation', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account]) // initial list
      .mockResolvedValueOnce([]) // import history, once the card mounts
      .mockResolvedValueOnce(undefined) // DELETE
      .mockResolvedValueOnce([]) // refetch after invalidation

    const user = userEvent.setup()
    renderPage()

    await screen.findByText('Compte courant')
    await user.click(screen.getByRole('button', { name: 'Supprimer' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Supprimer' }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/bank-accounts/1',
        expect.objectContaining({ method: 'DELETE' }),
      ),
    )
    expect(await screen.findByText("Aucun compte pour l'instant.")).toBeInTheDocument()
  })

  it('does not delete when the confirmation is dismissed', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account])
      .mockResolvedValueOnce([]) // import history, once the card mounts

    const user = userEvent.setup()
    renderPage()

    await screen.findByText('Compte courant')
    await user.click(screen.getByRole('button', { name: 'Supprimer' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Annuler' }))

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(apiFetch).not.toHaveBeenCalledWith('/api/bank-accounts/1', expect.objectContaining({ method: 'DELETE' }))
  })

  async function uploadAndPreview(card: HTMLElement, user: ReturnType<typeof userEvent.setup>) {
    const fileInput = within(card).getByLabelText('Importer un relevé CSV') as HTMLInputElement
    const file = new File(['Date;Libellé\r\n2026-01-01;Test'], 'releve.csv', { type: 'text/csv' })
    await user.upload(fileInput, file)
    await user.click(within(card).getByRole('button', { name: 'Aperçu' }))
  }

  it('previews a CSV file before importing anything', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account]) // initial list
      .mockResolvedValueOnce([]) // import history, once the card mounts
      .mockResolvedValueOnce([
        {
          date: '2026-01-01',
          rawLabel: 'CARTE EXAMPLE',
          cleanedLabel: 'Example',
          amount: -12.5,
          categoryId: null,
          categoryName: null,
        },
      ])

    const user = userEvent.setup()
    renderPage()

    const card = (await screen.findByText('Compte courant')).closest('li')!
    await uploadAndPreview(card, user)

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(screen.getByText('Example')).toBeInTheDocument()

    const previewCall = vi
      .mocked(apiFetch)
      .mock.calls.find(([path]) => path === '/api/bank-accounts/1/import/preview')!
    expect((previewCall[1] as { body: FormData }).body).toBeInstanceOf(FormData)
  })

  it('ignores a second click on Aperçu while the first preview is still in flight', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account]) // initial list
      .mockResolvedValueOnce([]) // import history, once the card mounts
      .mockResolvedValueOnce([]) // preview

    const user = userEvent.setup()
    renderPage()

    const card = (await screen.findByText('Compte courant')).closest('li')!
    const fileInput = within(card).getByLabelText('Importer un relevé CSV') as HTMLInputElement
    const file = new File(['Date;Libellé\r\n2026-01-01;Test'], 'releve.csv', { type: 'text/csv' })
    await user.upload(fileInput, file)

    // A React state-driven `disabled` alone can't stop two clicks that land before the
    // re-render commits - both clicks fire before either resolves, exercising the
    // synchronous ref guard rather than relying on timing.
    const previewButton = within(card).getByRole('button', { name: 'Aperçu' })
    await Promise.all([user.click(previewButton), user.click(previewButton)])

    await screen.findByRole('dialog')

    const previewCalls = vi
      .mocked(apiFetch)
      .mock.calls.filter(([path]) => path === '/api/bank-accounts/1/import/preview')
    expect(previewCalls).toHaveLength(1)
  })

  it('commits only the rows left checked, with any edited category', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account]) // initial list
      .mockResolvedValueOnce([]) // import history, once the card mounts
      .mockResolvedValueOnce([
        {
          date: '2026-01-01',
          rawLabel: 'CARTE NETFLIX',
          cleanedLabel: 'Netflix',
          amount: -12.5,
          categoryId: 10,
          categoryName: 'Loisirs',
        },
        {
          date: '2026-01-02',
          rawLabel: 'CARTE STEAM',
          cleanedLabel: 'Steam',
          amount: -7.99,
          categoryId: null,
          categoryName: null,
        },
      ])
      .mockResolvedValueOnce([
        {
          id: 10,
          name: 'Loisirs',
          icon: null,
          color: null,
          parentCategoryId: null,
          isSystemDefault: true,
          isOwnedByCurrentUser: false,
        },
      ])
      .mockResolvedValueOnce({
        totalRowsParsed: 1,
        newTransactionsImported: 1,
        duplicatesSkipped: 0,
        internalTransfersDetected: 0,
      })
      .mockResolvedValueOnce([]) // import history, re-fetched after the commit invalidates it

    const user = userEvent.setup()
    renderPage()

    const card = (await screen.findByText('Compte courant')).closest('li')!
    await uploadAndPreview(card, user)
    await screen.findByRole('dialog')

    // Uncheck Steam - only Netflix should be committed.
    await user.click(screen.getByLabelText('Inclure Steam'))
    await user.click(screen.getByRole('button', { name: "Confirmer l'import (1)" }))

    await waitFor(() =>
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/bank-accounts/1/import/commit',
        expect.objectContaining({
          method: 'POST',
          body: {
            fileName: 'releve.csv',
            rows: [
              {
                date: '2026-01-01',
                rawLabel: 'CARTE NETFLIX',
                cleanedLabel: 'Netflix',
                amount: -12.5,
                categoryId: 10,
              },
            ],
            fileContentBase64: Buffer.from('Date;Libellé\r\n2026-01-01;Test', 'utf-8').toString('base64'),
          },
        }),
      ),
    )
    expect(await within(card).findByText(/1 transaction\(s\) importée\(s\)/)).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('cancels the preview without committing anything', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account]) // initial list
      .mockResolvedValueOnce([]) // import history, once the card mounts
      .mockResolvedValueOnce([
        {
          date: '2026-01-01',
          rawLabel: 'CARTE EXAMPLE',
          cleanedLabel: 'Example',
          amount: -12.5,
          categoryId: null,
          categoryName: null,
        },
      ])

    const user = userEvent.setup()
    renderPage()

    const card = (await screen.findByText('Compte courant')).closest('li')!
    await uploadAndPreview(card, user)
    await screen.findByRole('dialog')

    await user.click(screen.getByRole('button', { name: 'Annuler' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(apiFetch).not.toHaveBeenCalledWith('/api/bank-accounts/1/import/commit', expect.anything())
  })

  it('shows an empty state for the import history when there are no past imports', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account])
      .mockResolvedValueOnce([]) // import history

    renderPage()

    const card = (await screen.findByText('Compte courant')).closest('li')!
    expect(await within(card).findByText("Aucun import pour l'instant.")).toBeInTheDocument()
  })

  it('lists past imports with their filename, date and counts, with no download link for a batch with no stored file', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account])
      .mockResolvedValueOnce([
        {
          id: 1,
          fileName: 'aout-2026.csv',
          importedAtUtc: '2026-08-31T10:15:00Z',
          totalRowsParsed: 12,
          newTransactionsImported: 10,
          duplicatesSkipped: 2,
          internalTransfersDetected: 1,
          hasStoredFile: false,
        },
      ])

    renderPage()

    const card = (await screen.findByText('Compte courant')).closest('li')!
    expect(await within(card).findByText(/aout-2026\.csv/)).toBeInTheDocument()
    expect(within(card).getByText(/10 importée\(s\)/)).toBeInTheDocument()
    expect(within(card).getByText(/2 doublon\(s\) ignoré\(s\)/)).toBeInTheDocument()
    expect(within(card).getByText(/1 virement\(s\) interne\(s\) détecté\(s\)/)).toBeInTheDocument()
    expect(within(card).queryByRole('button', { name: 'Télécharger' })).not.toBeInTheDocument()
  })

  it('offers a download link for a past import with a stored file, and re-downloads it on click', async () => {
    vi.mocked(apiFetch)
      .mockResolvedValueOnce([account])
      .mockResolvedValueOnce([
        {
          id: 1,
          fileName: 'aout-2026.csv',
          importedAtUtc: '2026-08-31T10:15:00Z',
          totalRowsParsed: 12,
          newTransactionsImported: 10,
          duplicatesSkipped: 2,
          internalTransfersDetected: 1,
          hasStoredFile: true,
        },
      ])
    const blob = new Blob(['csv content'], { type: 'text/csv' })
    vi.mocked(apiFetchBlob).mockResolvedValueOnce({ blob, fileName: 'aout-2026.csv' })
    // jsdom doesn't implement the Object URL API the download uses to hand the blob to
    // the browser - stubbed just enough to observe it was called correctly.
    const createObjectURL = vi.fn().mockReturnValue('blob:mock-url')
    const revokeObjectURL = vi.fn()
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL })

    const user = userEvent.setup()
    renderPage()

    const card = (await screen.findByText('Compte courant')).closest('li')!
    await user.click(await within(card).findByRole('button', { name: 'Télécharger' }))

    await waitFor(() =>
      expect(apiFetchBlob).toHaveBeenCalledWith(
        '/api/bank-accounts/1/import/history/1/file',
        expect.objectContaining({}),
      ),
    )
    expect(createObjectURL).toHaveBeenCalledWith(blob)
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:mock-url')
  })

  it('shows an error message when the API call fails', async () => {
    vi.mocked(apiFetch).mockRejectedValueOnce(new ApiError(500, 'Server error', 'Une erreur est survenue.'))

    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent('Une erreur est survenue.')
  })
})
