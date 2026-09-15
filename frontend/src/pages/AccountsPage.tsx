import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { BankAccount, ImportSummary } from '../api/types'
import { AppHeader } from '../components/AppHeader'
import { useApiClient } from '../auth/useApiClient'

// Only bank with a registered IBankStatementParser on the backend
// (BoursoBankCsvParser.BankName) - importing against any other value fails with a 400
// "No statement parser is registered for bank '...'" from BankStatementImportService.
// A <select> limited to this list keeps that failure mode from ever being reachable
// through this form; free-form bank names would need a real multi-parser backend first.
const SUPPORTED_BANKS = ['BoursoBank']

function errorMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : 'Une erreur est survenue.'
}

export function AccountsPage() {
  const apiClient = useApiClient()
  const queryClient = useQueryClient()

  const accountsQuery = useQuery({
    queryKey: ['bank-accounts'],
    queryFn: () => apiClient<BankAccount[]>('/api/bank-accounts'),
  })

  function invalidateAccounts() {
    return queryClient.invalidateQueries({ queryKey: ['bank-accounts'] })
  }

  return (
    <div className="min-h-svh bg-gray-50 dark:bg-gray-900">
      <AppHeader />

      <main className="mx-auto max-w-3xl space-y-6 p-4">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-white">Comptes bancaires</h1>

        <CreateAccountForm onCreated={invalidateAccounts} />

        {accountsQuery.isPending && <p className="text-gray-600 dark:text-gray-300">Chargement…</p>}

        {accountsQuery.isError && (
          <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
            {errorMessage(accountsQuery.error)}
          </p>
        )}

        {accountsQuery.data && accountsQuery.data.length === 0 && (
          <p className="text-gray-600 dark:text-gray-300">Aucun compte pour l'instant.</p>
        )}

        <ul className="space-y-4">
          {accountsQuery.data?.map((account) => (
            <li key={account.id}>
              <AccountCard account={account} onChanged={invalidateAccounts} />
            </li>
          ))}
        </ul>
      </main>
    </div>
  )
}

function CreateAccountForm({ onCreated }: { onCreated: () => void }) {
  const apiClient = useApiClient()
  const [bankName, setBankName] = useState(SUPPORTED_BANKS[0])
  const [label, setLabel] = useState('')
  const [iban, setIban] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await apiClient('/api/bank-accounts', {
        method: 'POST',
        body: { bankName, label, iban: iban.trim() === '' ? null : iban.trim() },
      })
      setLabel('')
      setIban('')
      onCreated()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="space-y-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800"
    >
      <h2 className="font-medium text-gray-900 dark:text-white">Ajouter un compte</h2>

      {error && (
        <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label htmlFor="bankName" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Banque
          </label>
          <select
            id="bankName"
            value={bankName}
            onChange={(e) => setBankName(e.target.value)}
            className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          >
            {SUPPORTED_BANKS.map((bank) => (
              <option key={bank} value={bank}>
                {bank}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="label" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Libellé
          </label>
          <input
            id="label"
            type="text"
            required
            value={label}
            onChange={(e) => setLabel(e.target.value)}
            placeholder="Compte courant"
            className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          />
        </div>

        <div>
          <label htmlFor="iban" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            IBAN (optionnel)
          </label>
          <input
            id="iban"
            type="text"
            value={iban}
            onChange={(e) => setIban(e.target.value)}
            className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          />
        </div>
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className="rounded bg-sky-600 px-4 py-2 font-medium text-white hover:bg-sky-700 disabled:opacity-50"
      >
        {isSubmitting ? 'Ajout…' : 'Ajouter'}
      </button>
    </form>
  )
}

function AccountCard({ account, onChanged }: { account: BankAccount; onChanged: () => void }) {
  const apiClient = useApiClient()
  const [isEditing, setIsEditing] = useState(false)
  const [bankName, setBankName] = useState(account.bankName)
  const [label, setLabel] = useState(account.label)
  const [iban, setIban] = useState(account.iban ?? '')
  const [error, setError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  async function handleSave(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsSaving(true)
    try {
      await apiClient(`/api/bank-accounts/${account.id}`, {
        method: 'PUT',
        body: { bankName, label, iban: iban.trim() === '' ? null : iban.trim() },
      })
      setIsEditing(false)
      onChanged()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleDelete() {
    if (!confirm(`Supprimer le compte "${account.label}" et toutes ses transactions ?`)) {
      return
    }
    setError(null)
    try {
      await apiClient(`/api/bank-accounts/${account.id}`, { method: 'DELETE' })
      onChanged()
    } catch (err) {
      setError(errorMessage(err))
    }
  }

  if (isEditing) {
    return (
      <form
        onSubmit={handleSave}
        className="space-y-3 rounded-lg border border-sky-200 bg-white p-4 shadow dark:border-sky-800 dark:bg-gray-800"
      >
        {error && (
          <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
            {error}
          </p>
        )}
        <div className="grid gap-3 sm:grid-cols-3">
          <select
            aria-label="Banque"
            value={bankName}
            onChange={(e) => setBankName(e.target.value)}
            className="rounded border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          >
            {SUPPORTED_BANKS.map((bank) => (
              <option key={bank} value={bank}>
                {bank}
              </option>
            ))}
          </select>
          <input
            aria-label="Libellé"
            type="text"
            required
            value={label}
            onChange={(e) => setLabel(e.target.value)}
            className="rounded border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          />
          <input
            aria-label="IBAN"
            type="text"
            value={iban}
            onChange={(e) => setIban(e.target.value)}
            className="rounded border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          />
        </div>
        <div className="flex gap-2">
          <button
            type="submit"
            disabled={isSaving}
            className="rounded bg-sky-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-sky-700 disabled:opacity-50"
          >
            {isSaving ? 'Enregistrement…' : 'Enregistrer'}
          </button>
          <button
            type="button"
            onClick={() => setIsEditing(false)}
            className="rounded border border-gray-300 px-3 py-1.5 text-sm hover:bg-gray-100 dark:border-gray-600 dark:hover:bg-gray-700"
          >
            Annuler
          </button>
        </div>
      </form>
    )
  }

  return (
    <div className="space-y-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800">
      {error && (
        <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      <div className="flex items-start justify-between">
        <div>
          <p className="font-medium text-gray-900 dark:text-white">{account.label}</p>
          <p className="text-sm text-gray-500 dark:text-gray-400">
            {account.bankName}
            {account.iban ? ` · ${account.iban}` : ''}
          </p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => setIsEditing(true)}
            className="rounded border border-gray-300 px-3 py-1 text-sm hover:bg-gray-100 dark:border-gray-600 dark:hover:bg-gray-700"
          >
            Modifier
          </button>
          <button
            onClick={handleDelete}
            className="rounded border border-red-300 px-3 py-1 text-sm text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
          >
            Supprimer
          </button>
        </div>
      </div>

      <ImportCsvForm accountId={account.id} />
    </div>
  )
}

function ImportCsvForm({ accountId }: { accountId: number }) {
  const apiClient = useApiClient()
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [summary, setSummary] = useState<ImportSummary | null>(null)
  const [isImporting, setIsImporting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!file) {
      return
    }
    setError(null)
    setSummary(null)
    setIsImporting(true)
    try {
      const formData = new FormData()
      formData.append('file', file)
      const result = await apiClient<ImportSummary>(`/api/bank-accounts/${accountId}/import`, {
        method: 'POST',
        body: formData,
      })
      setSummary(result)
      setFile(null)
      event.currentTarget.reset()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setIsImporting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="border-t border-gray-100 pt-3 dark:border-gray-700">
      <label htmlFor={`import-${accountId}`} className="block text-sm font-medium text-gray-700 dark:text-gray-300">
        Importer un relevé CSV
      </label>
      <div className="mt-1 flex flex-wrap items-center gap-2">
        <input
          id={`import-${accountId}`}
          type="file"
          accept=".csv,text/csv"
          onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          className="text-sm text-gray-600 dark:text-gray-300"
        />
        <button
          type="submit"
          disabled={!file || isImporting}
          className="rounded bg-sky-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-sky-700 disabled:opacity-50"
        >
          {isImporting ? 'Import…' : 'Importer'}
        </button>
      </div>

      {error && (
        <p role="alert" className="mt-2 rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      {summary && (
        <p className="mt-2 rounded bg-green-50 px-3 py-2 text-sm text-green-700 dark:bg-green-950 dark:text-green-300">
          {summary.totalRowsParsed} ligne(s) lue(s), {summary.newTransactionsImported} importée(s),{' '}
          {summary.duplicatesSkipped} doublon(s) ignoré(s), {summary.internalTransfersDetected} virement(s)
          interne(s) détecté(s).
        </p>
      )}
    </form>
  )
}
