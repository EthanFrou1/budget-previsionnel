import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { BankAccount, Category, ImportCommitRequest, ImportRow, ImportSummary } from '../api/types'
import { AppLayout } from '../components/AppLayout'
import { useConfirm } from '../components/confirmContext'
import { useApiClient } from '../auth/useApiClient'
import { formatCurrency, formatDateFr } from '../utils/format'

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
  const [isCreating, setIsCreating] = useState(false)

  const accountsQuery = useQuery({
    queryKey: ['bank-accounts'],
    queryFn: () => apiClient<BankAccount[]>('/api/bank-accounts'),
  })

  function invalidateAccounts() {
    return queryClient.invalidateQueries({ queryKey: ['bank-accounts'] })
  }

  return (
    <AppLayout>
      <div className="flex items-center justify-between">
        <h1 className="font-display text-xl font-semibold text-heading">Comptes bancaires</h1>
        {!isCreating && (
          <button
            onClick={() => setIsCreating(true)}
            className="rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover"
          >
            + Ajouter un compte
          </button>
        )}
      </div>

      {isCreating && (
        <CreateAccountForm
          onCreated={() => {
            invalidateAccounts()
            setIsCreating(false)
          }}
          onCancel={() => setIsCreating(false)}
        />
      )}

      {accountsQuery.isPending && <p className="text-body">Chargement…</p>}

      {accountsQuery.isError && (
        <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {errorMessage(accountsQuery.error)}
        </p>
      )}

      {accountsQuery.data && accountsQuery.data.length === 0 && (
        <p className="text-body">Aucun compte pour l'instant.</p>
      )}

      <ul className="space-y-4">
        {accountsQuery.data?.map((account) => (
          <li key={account.id}>
            <AccountCard account={account} onChanged={invalidateAccounts} />
          </li>
        ))}
      </ul>
    </AppLayout>
  )
}

function CreateAccountForm({ onCreated, onCancel }: { onCreated: () => void; onCancel: () => void }) {
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
    <form onSubmit={handleSubmit} className="space-y-3 rounded-lg bg-surface p-4 shadow">
      <h2 className="font-display font-medium text-heading">Ajouter un compte</h2>

      {error && (
        <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {error}
        </p>
      )}

      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label htmlFor="bankName" className="block text-sm font-medium text-body">
            Banque
          </label>
          <select
            id="bankName"
            value={bankName}
            onChange={(e) => setBankName(e.target.value)}
            className="mt-1 w-full rounded border border-border px-3 py-2 focus:border-accent focus:outline-none bg-field text-heading"
          >
            {SUPPORTED_BANKS.map((bank) => (
              <option key={bank} value={bank}>
                {bank}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="label" className="block text-sm font-medium text-body">
            Libellé
          </label>
          <input
            id="label"
            type="text"
            required
            value={label}
            onChange={(e) => setLabel(e.target.value)}
            placeholder="Compte courant"
            className="mt-1 w-full rounded border border-border px-3 py-2 focus:border-accent focus:outline-none bg-field text-heading"
          />
        </div>

        <div>
          <label htmlFor="iban" className="block text-sm font-medium text-body">
            IBAN (optionnel)
          </label>
          <input
            id="iban"
            type="text"
            value={iban}
            onChange={(e) => setIban(e.target.value)}
            className="mt-1 w-full rounded border border-border px-3 py-2 focus:border-accent focus:outline-none bg-field text-heading"
          />
        </div>
      </div>

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-accent px-4 py-2 font-medium text-white hover:bg-accent-hover disabled:opacity-50"
        >
          {isSubmitting ? 'Ajout…' : 'Ajouter'}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="rounded border border-border px-4 py-2 font-medium text-body hover:bg-overlay"
        >
          Annuler
        </button>
      </div>
    </form>
  )
}

function AccountCard({ account, onChanged }: { account: BankAccount; onChanged: () => void }) {
  const apiClient = useApiClient()
  const confirm = useConfirm()
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
    if (
      !(await confirm(`Supprimer le compte "${account.label}" et toutes ses transactions ?`, {
        confirmLabel: 'Supprimer',
        danger: true,
      }))
    ) {
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
        className="space-y-3 rounded-lg border border-accent/40 bg-surface p-4 shadow"
      >
        {error && (
          <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
            {error}
          </p>
        )}
        <div className="grid gap-3 sm:grid-cols-3">
          <div>
            <label htmlFor={`account-${account.id}-bank`} className="block text-sm font-medium text-body">
              Banque
            </label>
            <select
              id={`account-${account.id}-bank`}
              value={bankName}
              onChange={(e) => setBankName(e.target.value)}
              className="mt-1 w-full rounded border border-border px-3 py-2 bg-field text-heading"
            >
              {SUPPORTED_BANKS.map((bank) => (
                <option key={bank} value={bank}>
                  {bank}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor={`account-${account.id}-label`} className="block text-sm font-medium text-body">
              Libellé
            </label>
            <input
              id={`account-${account.id}-label`}
              type="text"
              required
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              className="mt-1 w-full rounded border border-border px-3 py-2 bg-field text-heading"
            />
          </div>
          <div>
            <label htmlFor={`account-${account.id}-iban`} className="block text-sm font-medium text-body">
              IBAN (optionnel)
            </label>
            <input
              id={`account-${account.id}-iban`}
              type="text"
              value={iban}
              onChange={(e) => setIban(e.target.value)}
              className="mt-1 w-full rounded border border-border px-3 py-2 bg-field text-heading"
            />
          </div>
        </div>
        <div className="flex gap-2">
          <button
            type="submit"
            disabled={isSaving}
            className="rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover disabled:opacity-50"
          >
            {isSaving ? 'Enregistrement…' : 'Enregistrer'}
          </button>
          <button
            type="button"
            onClick={() => setIsEditing(false)}
            className="rounded border border-border px-3 py-1.5 text-sm hover:bg-overlay"
          >
            Annuler
          </button>
        </div>
      </form>
    )
  }

  return (
    <div className="space-y-3 rounded-lg bg-surface p-4 shadow">
      {error && (
        <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {error}
        </p>
      )}

      <div className="flex items-start justify-between">
        <div>
          <p className="font-medium text-heading">{account.label}</p>
          <p className="text-sm text-muted">
            {account.bankName}
            {account.iban ? ` · ${account.iban}` : ''}
          </p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => setIsEditing(true)}
            className="rounded border border-border px-3 py-1 text-sm hover:bg-overlay"
          >
            Modifier
          </button>
          <button
            onClick={handleDelete}
            className="rounded border border-negative/40 px-3 py-1 text-sm text-negative hover:bg-negative/10"
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
  const queryClient = useQueryClient()
  const formRef = useRef<HTMLFormElement | null>(null)
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [summary, setSummary] = useState<ImportSummary | null>(null)
  const [previewRows, setPreviewRows] = useState<ImportRow[] | null>(null)
  const [isPreviewing, setIsPreviewing] = useState(false)
  // A ref, not just the isPreviewing state: a fast double-click can fire this handler
  // twice before React re-renders the button's disabled attribute, racing two previews
  // and showing both a stale error and a stale dialog at once. Checked synchronously,
  // before the state update even schedules, so the second click is a genuine no-op.
  const isSubmittingRef = useRef(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!file || isSubmittingRef.current) {
      return
    }
    isSubmittingRef.current = true
    setError(null)
    setSummary(null)
    setIsPreviewing(true)
    try {
      const formData = new FormData()
      formData.append('file', file)
      const rows = await apiClient<ImportRow[]>(`/api/bank-accounts/${accountId}/import/preview`, {
        method: 'POST',
        body: formData,
      })
      setPreviewRows(rows)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      isSubmittingRef.current = false
      setIsPreviewing(false)
    }
  }

  function handleCommitted(result: ImportSummary) {
    setPreviewRows(null)
    setSummary(result)
    setFile(null)
    formRef.current?.reset()
    queryClient.invalidateQueries({ queryKey: ['transactions'] })
  }

  return (
    <form ref={formRef} onSubmit={handleSubmit} className="border-t border-border pt-3">
      <label htmlFor={`import-${accountId}`} className="block text-sm font-medium text-body">
        Importer un relevé CSV
      </label>
      <div className="mt-1 flex flex-wrap items-center gap-2">
        <input
          id={`import-${accountId}`}
          type="file"
          accept=".csv,text/csv"
          onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          className="text-sm text-body file:mr-3 file:cursor-pointer file:rounded file:border-0 file:bg-accent file:px-3 file:py-1.5 file:text-sm file:font-medium file:text-white hover:file:bg-accent-hover"
        />
        <button
          type="submit"
          disabled={!file || isPreviewing}
          className="rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover disabled:opacity-50"
        >
          {isPreviewing ? 'Analyse…' : 'Aperçu'}
        </button>
      </div>

      {error && (
        <p role="alert" className="mt-2 rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {error}
        </p>
      )}

      {summary && (
        <p className="mt-2 rounded bg-positive/10 px-3 py-2 text-sm text-positive">
          {summary.newTransactionsImported} transaction(s) importée(s), {summary.duplicatesSkipped} doublon(s)
          ignoré(s), {summary.internalTransfersDetected} virement(s) interne(s) détecté(s).
        </p>
      )}

      {previewRows && (
        <ImportPreviewDialog
          accountId={accountId}
          rows={previewRows}
          onCancel={() => setPreviewRows(null)}
          onCommitted={handleCommitted}
        />
      )}
    </form>
  )
}

interface ImportPreviewEntry {
  row: ImportRow
  included: boolean
  categoryId: number | null
}

/**
 * Nothing is persisted until "Confirmer l'import" - the file is only ever parsed,
 * deduplicated against existing transactions and auto-categorized (same two-tier
 * resolution as before: bank-suggested category, then the user's own CategoryRule
 * matches) on the backend's /preview endpoint. Excluding a row here means it's simply
 * never included in the /commit request, not that anything gets created and deleted.
 */
function ImportPreviewDialog({
  accountId,
  rows,
  onCancel,
  onCommitted,
}: {
  accountId: number
  rows: ImportRow[]
  onCancel: () => void
  onCommitted: (summary: ImportSummary) => void
}) {
  const apiClient = useApiClient()
  const [entries, setEntries] = useState<ImportPreviewEntry[]>(() =>
    rows.map((row) => ({ row, included: true, categoryId: row.categoryId })),
  )
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const categoriesQuery = useQuery({
    queryKey: ['categories'],
    queryFn: () => apiClient<Category[]>('/api/categories'),
  })
  const categories = categoriesQuery.data ?? []
  const includedCount = entries.filter((e) => e.included).length

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onCancel()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onCancel])

  function toggleIncluded(index: number) {
    setEntries((prev) => prev.map((e, i) => (i === index ? { ...e, included: !e.included } : e)))
  }

  function setCategoryId(index: number, categoryId: number | null) {
    setEntries((prev) => prev.map((e, i) => (i === index ? { ...e, categoryId } : e)))
  }

  async function handleConfirm() {
    setIsSubmitting(true)
    setError(null)
    try {
      const body: ImportCommitRequest = {
        rows: entries
          .filter((e) => e.included)
          .map((e) => ({
            date: e.row.date,
            rawLabel: e.row.rawLabel,
            cleanedLabel: e.row.cleanedLabel,
            amount: e.row.amount,
            categoryId: e.categoryId,
          })),
      }
      const summary = await apiClient<ImportSummary>(`/api/bank-accounts/${accountId}/import/commit`, {
        method: 'POST',
        body,
      })
      onCommitted(summary)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4"
      onClick={(e) => {
        if (e.target === e.currentTarget) onCancel()
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="import-preview-dialog-title"
        className="flex max-h-[85vh] w-full max-w-3xl flex-col rounded-lg border border-border bg-surface shadow-xl"
      >
        <div className="border-b border-border p-4">
          <h3 id="import-preview-dialog-title" className="font-display font-medium text-heading">
            Vérifier avant import
          </h3>
          <p className="mt-1 text-sm text-body">
            {rows.length === 0
              ? 'Aucune nouvelle ligne à importer (tout est déjà présent sur ce compte).'
              : `Décochez les lignes à ignorer, ajustez la catégorie si besoin, puis confirmez.`}
          </p>
        </div>

        {rows.length > 0 && (
          <div className="overflow-y-auto p-4">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-border text-xs uppercase text-muted">
                <tr>
                  <th className="w-8 py-2" />
                  <th className="py-2">Date</th>
                  <th className="py-2">Libellé</th>
                  <th className="py-2 text-right">Montant</th>
                  <th className="py-2">Catégorie</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {entries.map((entry, index) => (
                  <tr key={index} className={entry.included ? undefined : 'opacity-50'}>
                    <td className="py-2">
                      <input
                        type="checkbox"
                        checked={entry.included}
                        onChange={() => toggleIncluded(index)}
                        aria-label={`Inclure ${entry.row.cleanedLabel ?? entry.row.rawLabel}`}
                        className="h-4 w-4 rounded border-border"
                      />
                    </td>
                    <td className="whitespace-nowrap py-2 text-body">{formatDateFr(entry.row.date)}</td>
                    <td className="py-2 text-heading">{entry.row.cleanedLabel ?? entry.row.rawLabel}</td>
                    <td
                      className={`whitespace-nowrap py-2 text-right font-medium ${
                        entry.row.amount < 0 ? 'text-negative' : 'text-positive'
                      }`}
                    >
                      {formatCurrency(entry.row.amount)}
                    </td>
                    <td className="py-2">
                      <select
                        aria-label={`Catégorie de ${entry.row.cleanedLabel ?? entry.row.rawLabel}`}
                        value={entry.categoryId?.toString() ?? ''}
                        disabled={!entry.included}
                        onChange={(e) =>
                          setCategoryId(index, e.target.value === '' ? null : Number(e.target.value))
                        }
                        className="w-full rounded border border-border px-2 py-1 text-sm bg-field text-heading disabled:opacity-50"
                      >
                        <option value="">Non catégorisé</option>
                        {categories.map((category) => (
                          <option key={category.id} value={category.id}>
                            {category.name}
                          </option>
                        ))}
                      </select>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {error && (
          <p role="alert" className="mx-4 rounded bg-negative/10 px-3 py-2 text-sm text-negative">
            {error}
          </p>
        )}

        <div className="flex items-center justify-between gap-2 border-t border-border p-4">
          <span className="text-sm text-muted">
            {rows.length > 0 ? `${includedCount} / ${rows.length} sélectionnée(s)` : null}
          </span>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={onCancel}
              className="rounded border border-border px-3 py-1.5 text-sm hover:bg-overlay"
            >
              Annuler
            </button>
            {rows.length > 0 && (
              <button
                type="button"
                onClick={handleConfirm}
                disabled={isSubmitting}
                className="rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover disabled:opacity-50"
              >
                {isSubmitting ? 'Import…' : `Confirmer l'import (${includedCount})`}
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
