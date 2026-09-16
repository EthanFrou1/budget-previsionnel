import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { ApiError } from '../api/client'
import type {
  BankAccount,
  Category,
  RecurrenceFrequency,
  Transaction,
  TransactionPage as TransactionPageResponse,
} from '../api/types'
import { AppLayout } from '../components/AppLayout'
import { useApiClient } from '../auth/useApiClient'
import { formatCurrency, formatDateFr } from '../utils/format'
import { CategoryRulesSection } from './CategoryRulesSection'

// Same three labels as RecurringExpensesSection's own FREQUENCY_LABELS - duplicated
// rather than exported/shared, matching this file's own presetRange precedent (extract
// only past two consumers).
const FREQUENCY_LABELS: Record<RecurrenceFrequency, string> = {
  Weekly: 'Hebdomadaire',
  Monthly: 'Mensuelle',
  Yearly: 'Annuelle',
}

const PAGE_SIZE = 50

interface Filters {
  bankAccountId: string
  categoryId: string
  fromDate: string
  toDate: string
  excludeInternalTransfers: boolean
}

const EMPTY_FILTERS: Filters = {
  bankAccountId: '',
  categoryId: '',
  fromDate: '',
  toDate: '',
  excludeInternalTransfers: false,
}

type Preset = 'month' | 'quarter'
type PeriodMode = Preset | 'custom' | null

type SortColumn = 'date' | 'bankAccount' | 'label' | 'amount' | 'category'
type SortDirection = 'asc' | 'desc'
interface Sort {
  column: SortColumn
  direction: SortDirection
}
// Mirrors the backend default (TransactionQuery's own SortBy/SortDescending defaults) so
// the Date header shows the active-sort arrow on first load instead of looking unsorted.
const DEFAULT_SORT: Sort = { column: 'date', direction: 'desc' }

// Same date-range math as DashboardPage's presetRange - not yet extracted (only two
// consumers so far; see format.ts's own extraction note for why that's the threshold
// used across this project).
function pad(n: number): string {
  return n.toString().padStart(2, '0')
}
function isoFromParts(year: number, month: number, day: number): string {
  return `${year}-${pad(month)}-${pad(day)}`
}
function presetRange(preset: Preset): { fromDate: string; toDate: string } {
  const now = new Date()
  const toDate = isoFromParts(now.getFullYear(), now.getMonth() + 1, now.getDate())
  if (preset === 'month') {
    return { fromDate: isoFromParts(now.getFullYear(), now.getMonth() + 1, 1), toDate }
  }
  const quarterStart = new Date(now.getFullYear(), now.getMonth() - 2, 1)
  return { fromDate: isoFromParts(quarterStart.getFullYear(), quarterStart.getMonth() + 1, 1), toDate }
}

function buildQueryString(filters: Filters, search: string, page: number, sort: Sort): string {
  const params = new URLSearchParams()
  if (filters.bankAccountId) params.set('bankAccountId', filters.bankAccountId)
  if (filters.categoryId) params.set('categoryId', filters.categoryId)
  if (filters.fromDate) params.set('fromDate', filters.fromDate)
  if (filters.toDate) params.set('toDate', filters.toDate)
  if (search.trim()) params.set('search', search.trim())
  if (filters.excludeInternalTransfers) params.set('excludeInternalTransfers', 'true')
  params.set('page', String(page))
  params.set('pageSize', String(PAGE_SIZE))
  params.set('sortBy', sort.column)
  params.set('sortDirection', sort.direction)
  return params.toString()
}

function errorMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : 'Une erreur est survenue.'
}

export function TransactionsPage() {
  const apiClient = useApiClient()
  const queryClient = useQueryClient()

  const [filters, setFilters] = useState<Filters>(EMPTY_FILTERS)
  const [searchInput, setSearchInput] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [page, setPage] = useState(1)
  // Null until a period button is picked - "Personnalisé" only reveals the Du/Au date
  // fields rather than applying a range itself, so the two blank inputs are the initial
  // state a user edits from, not an accidental empty filter.
  const [periodMode, setPeriodMode] = useState<PeriodMode>(null)
  const [sort, setSort] = useState<Sort>(DEFAULT_SORT)

  // Clicking the already-active column flips its direction; clicking a different column
  // switches to it starting ascending, same convention as spreadsheet header clicks.
  function toggleSort(column: SortColumn) {
    setSort((current) =>
      current.column === column
        ? { column, direction: current.direction === 'asc' ? 'desc' : 'asc' }
        : { column, direction: 'asc' },
    )
    setPage(1)
  }

  // Debounces the search box so every keystroke doesn't fire a request - the rest of the
  // filters below reset the page directly from their own onChange instead, since they
  // change rarely enough not to need debouncing.
  useEffect(() => {
    const timeout = setTimeout(() => {
      setDebouncedSearch(searchInput)
      setPage(1)
    }, 400)
    return () => clearTimeout(timeout)
  }, [searchInput])

  function updateFilters(partial: Partial<Filters>) {
    setFilters((f) => ({ ...f, ...partial }))
    setPage(1)
  }

  function applyPreset(preset: Preset) {
    setPeriodMode(preset)
    updateFilters(presetRange(preset))
  }

  const accountsQuery = useQuery({
    queryKey: ['bank-accounts'],
    queryFn: () => apiClient<BankAccount[]>('/api/bank-accounts'),
  })

  const categoriesQuery = useQuery({
    queryKey: ['categories'],
    queryFn: () => apiClient<Category[]>('/api/categories'),
  })

  const transactionsQuery = useQuery({
    queryKey: ['transactions', filters, debouncedSearch, page, sort],
    queryFn: () =>
      apiClient<TransactionPageResponse>(
        `/api/transactions?${buildQueryString(filters, debouncedSearch, page, sort)}`,
      ),
    placeholderData: keepPreviousData,
  })

  const updateCategoryMutation = useMutation({
    mutationFn: ({ transactionId, categoryId }: { transactionId: number; categoryId: number | null }) =>
      apiClient(`/api/transactions/${transactionId}/category`, { method: 'PUT', body: { categoryId } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['transactions'] }),
  })

  const updateNotesMutation = useMutation({
    mutationFn: ({ transactionId, notes }: { transactionId: number; notes: string | null }) =>
      apiClient(`/api/transactions/${transactionId}/notes`, { method: 'PUT', body: { notes } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['transactions'] }),
  })

  // Quick-create shortcut, not a persistent link: pre-fills a new RecurringExpense (amount
  // < 0) or RecurringIncome (amount > 0) from this transaction (label/amount/category/date)
  // and reuses the existing endpoint as-is. The created record isn't tied back to this
  // transaction afterwards - editing or deleting it happens on /forecast like any other
  // recurring expense/income.
  const createRecurringMutation = useMutation({
    mutationFn: ({ transaction, frequency }: { transaction: Transaction; frequency: RecurrenceFrequency }) => {
      const endpoint = transaction.amount < 0 ? '/api/recurring-expenses' : '/api/recurring-incomes'
      return apiClient(endpoint, {
        method: 'POST',
        body: {
          label: transaction.cleanedLabel ?? transaction.rawLabel,
          amount: Math.abs(transaction.amount),
          categoryId: transaction.categoryId,
          frequency,
          startDate: transaction.date,
          endDate: null,
        },
      })
    },
    onSuccess: (_data, { transaction }) =>
      queryClient.invalidateQueries({
        queryKey: [transaction.amount < 0 ? 'recurring-expenses' : 'recurring-incomes'],
      }),
  })

  const categories = [...(categoriesQuery.data ?? [])].sort((a, b) => a.name.localeCompare(b.name))
  const totalPages = transactionsQuery.data
    ? Math.max(1, Math.ceil(transactionsQuery.data.totalCount / PAGE_SIZE))
    : 1

  return (
    <AppLayout>
      <h1 className="font-display text-xl font-semibold text-heading">Transactions</h1>

      <section className="grid gap-3 rounded-lg bg-surface p-4 shadow sm:grid-cols-3 lg:grid-cols-6">
        <div>
          <label htmlFor="filter-account" className="block text-xs font-medium text-body">
            Compte
          </label>
          <select
            id="filter-account"
            value={filters.bankAccountId}
            onChange={(e) => updateFilters({ bankAccountId: e.target.value })}
            className="mt-1 w-full rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
          >
            <option value="">Tous les comptes</option>
            {accountsQuery.data?.map((account) => (
              <option key={account.id} value={account.id}>
                {account.label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="filter-category" className="block text-xs font-medium text-body">
            Catégorie
          </label>
          <select
            id="filter-category"
            value={filters.categoryId}
            onChange={(e) => updateFilters({ categoryId: e.target.value })}
            className="mt-1 w-full rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
          >
            <option value="">Toutes catégories</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="filter-period" className="block text-xs font-medium text-body">
            Période
          </label>
          <select
            id="filter-period"
            value={periodMode ?? ''}
            onChange={(e) => {
              const value = e.target.value
              if (value === 'month' || value === 'quarter') {
                applyPreset(value)
              } else if (value === 'custom') {
                setPeriodMode('custom')
              } else {
                setPeriodMode(null)
                updateFilters({ fromDate: '', toDate: '' })
              }
            }}
            className="mt-1 w-full rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
          >
            <option value="">Toutes les périodes</option>
            <option value="month">Ce mois-ci</option>
            <option value="quarter">3 derniers mois</option>
            <option value="custom">Personnalisé</option>
          </select>
        </div>

        {periodMode === 'custom' && (
          <>
            <div>
              <label htmlFor="filter-from" className="block text-xs font-medium text-body">
                Du
              </label>
              <input
                id="filter-from"
                type="date"
                value={filters.fromDate}
                onChange={(e) => updateFilters({ fromDate: e.target.value })}
                className="mt-1 w-full rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
              />
            </div>

            <div>
              <label htmlFor="filter-to" className="block text-xs font-medium text-body">
                Au
              </label>
              <input
                id="filter-to"
                type="date"
                value={filters.toDate}
                onChange={(e) => updateFilters({ toDate: e.target.value })}
                className="mt-1 w-full rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
              />
            </div>
          </>
        )}

        <div className="sm:col-span-2">
          <label htmlFor="filter-search" className="block text-xs font-medium text-body">
            Recherche
          </label>
          <input
            id="filter-search"
            type="search"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Libellé…"
            className="mt-1 w-full rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
          />
        </div>

        <label className="flex items-end gap-2 pb-1.5 text-sm text-body lg:col-span-6">
          <input
            type="checkbox"
            checked={filters.excludeInternalTransfers}
            onChange={(e) => updateFilters({ excludeInternalTransfers: e.target.checked })}
            className="h-4 w-4 rounded border-border"
          />
          Exclure les virements internes
        </label>
      </section>

      {transactionsQuery.isPending && <p className="text-body">Chargement…</p>}

      {transactionsQuery.isError && (
        <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {errorMessage(transactionsQuery.error)}
        </p>
      )}

      {transactionsQuery.data && (
        <div className="overflow-x-auto rounded-lg bg-surface shadow">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-border text-xs uppercase text-muted">
              <tr>
                <SortableHeader column="date" label="Date" sort={sort} onSort={toggleSort} />
                <SortableHeader column="bankAccount" label="Compte" sort={sort} onSort={toggleSort} />
                <SortableHeader column="label" label="Libellé" sort={sort} onSort={toggleSort} />
                <SortableHeader column="amount" label="Montant" align="right" sort={sort} onSort={toggleSort} />
                <SortableHeader column="category" label="Catégorie" sort={sort} onSort={toggleSort} />
                <th className="px-3 py-2">Note</th>
                <th className="px-3 py-2">Récurrente</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {transactionsQuery.data.items.map((transaction: Transaction) => (
                <tr key={transaction.id}>
                  <td className="whitespace-nowrap px-3 py-2 text-body">{formatDateFr(transaction.date)}</td>
                  <td className="px-3 py-2 text-body">{transaction.bankAccountLabel}</td>
                  <td className="px-3 py-2 text-heading">
                    {transaction.cleanedLabel ?? transaction.rawLabel}
                    {transaction.isInternalTransfer && (
                      <span className="ml-2 rounded bg-accent/15 px-1.5 py-0.5 text-xs text-accent">
                        Virement interne
                      </span>
                    )}
                  </td>
                  <td
                    className={`whitespace-nowrap px-3 py-2 text-right font-medium ${
                      transaction.amount < 0 ? 'text-negative' : 'text-positive'
                    }`}
                  >
                    {formatCurrency(transaction.amount)}
                  </td>
                  <td className="px-3 py-2">
                    <select
                      aria-label={`Catégorie de ${transaction.cleanedLabel ?? transaction.rawLabel}`}
                      value={transaction.categoryId?.toString() ?? ''}
                      onChange={(e) =>
                        updateCategoryMutation.mutate({
                          transactionId: transaction.id,
                          categoryId: e.target.value === '' ? null : Number(e.target.value),
                        })
                      }
                      className="w-full rounded border border-border px-2 py-1 text-sm bg-field text-heading"
                    >
                      <option value="">Non catégorisé</option>
                      {categories.map((category) => (
                        <option key={category.id} value={category.id}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td className="px-3 py-2">
                    <NotesCell
                      transaction={transaction}
                      onSave={(notes) => updateNotesMutation.mutate({ transactionId: transaction.id, notes })}
                    />
                  </td>
                  <td className="px-3 py-2">
                    <RecurringMarkCell
                      transaction={transaction}
                      onCreate={(frequency) => createRecurringMutation.mutateAsync({ transaction, frequency })}
                    />
                  </td>
                </tr>
              ))}
              {transactionsQuery.data.items.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-3 py-6 text-center text-muted">
                    Aucune transaction ne correspond à ces filtres.
                  </td>
                </tr>
              )}
            </tbody>
          </table>

          <div className="flex items-center justify-between border-t border-border px-3 py-2 text-sm text-body">
            <button
              onClick={() => setPage((p) => p - 1)}
              disabled={page <= 1}
              className="rounded border border-border px-3 py-1 hover:bg-overlay disabled:opacity-50"
            >
              Précédent
            </button>
            <span>
              Page {page} / {totalPages} ({transactionsQuery.data.totalCount} transaction(s))
            </span>
            <button
              onClick={() => setPage((p) => p + 1)}
              disabled={page >= totalPages}
              className="rounded border border-border px-3 py-1 hover:bg-overlay disabled:opacity-50"
            >
              Suivant
            </button>
          </div>
        </div>
      )}

      <CategoryRulesSection categories={categories} />
    </AppLayout>
  )
}

function SortableHeader({
  column,
  label,
  align = 'left',
  sort,
  onSort,
}: {
  column: SortColumn
  label: string
  align?: 'left' | 'right'
  sort: Sort
  onSort: (column: SortColumn) => void
}) {
  const isActive = sort.column === column
  return (
    <th
      className={`px-3 py-2 ${align === 'right' ? 'text-right' : ''}`}
      aria-sort={isActive ? (sort.direction === 'asc' ? 'ascending' : 'descending') : 'none'}
    >
      <button
        type="button"
        onClick={() => onSort(column)}
        className={`inline-flex items-center gap-1 uppercase hover:text-heading ${
          isActive ? 'text-heading' : ''
        } ${align === 'right' ? 'flex-row-reverse' : ''}`}
      >
        {label}
        <span aria-hidden="true" className="text-[10px]">
          {isActive ? (sort.direction === 'asc' ? '▲' : '▼') : '⇕'}
        </span>
      </button>
    </th>
  )
}

/**
 * Free-form personal note per transaction - never touched by import/categorization,
 * purely for the user's own reference. Local state so typing doesn't refetch on every
 * keystroke; saves on blur, and only when the trimmed value actually changed, so
 * clicking in and back out of an empty field doesn't fire a no-op PUT.
 */
function NotesCell({
  transaction,
  onSave,
}: {
  transaction: Transaction
  onSave: (notes: string | null) => void
}) {
  const [value, setValue] = useState(transaction.notes ?? '')

  useEffect(() => {
    setValue(transaction.notes ?? '')
  }, [transaction.notes])

  function handleBlur() {
    const trimmed = value.trim()
    const normalized = trimmed === '' ? null : trimmed
    if (normalized !== transaction.notes) {
      onSave(normalized)
    }
  }

  return (
    <input
      aria-label={`Note pour ${transaction.cleanedLabel ?? transaction.rawLabel}`}
      type="text"
      value={value}
      onChange={(e) => setValue(e.target.value)}
      onBlur={handleBlur}
      placeholder="Note perso…"
      className="w-full min-w-32 rounded border border-border px-2 py-1 text-sm bg-field text-heading placeholder:text-muted"
    />
  )
}

/**
 * Quick-create shortcut for turning a transaction into a RecurringExpense (negative
 * amount) or a RecurringIncome (positive amount) without retyping its label/amount/
 * category/date - not offered for internal transfers (a transfer between the user's own
 * accounts is neither). Doesn't track whether a transaction was already "used" this way
 * past the current page load (no persistent link is kept) - `isDone` only prevents an
 * accidental duplicate create in the same session.
 */
function RecurringMarkCell({
  transaction,
  onCreate,
}: {
  transaction: Transaction
  onCreate: (frequency: RecurrenceFrequency) => Promise<unknown>
}) {
  const [isOpen, setIsOpen] = useState(false)
  const [isDone, setIsDone] = useState(false)

  if (transaction.isInternalTransfer) {
    return null
  }

  if (isDone) {
    return <span className="text-xs text-positive">Ajoutée ✓</span>
  }

  const isExpense = transaction.amount < 0
  const label = transaction.cleanedLabel ?? transaction.rawLabel

  return (
    <>
      <button
        type="button"
        onClick={() => setIsOpen(true)}
        aria-label={`Marquer ${label} comme ${isExpense ? 'dépense récurrente' : 'revenu récurrent'}`}
        className="rounded border border-border px-2 py-1 text-xs text-body hover:bg-overlay"
      >
        +
      </button>
      {isOpen && (
        <RecurringMarkDialog
          transaction={transaction}
          onCreate={onCreate}
          onDone={() => {
            setIsOpen(false)
            setIsDone(true)
          }}
          onCancel={() => setIsOpen(false)}
        />
      )}
    </>
  )
}

/**
 * `fixed inset-0` takes this out of the table's normal flow entirely, so it overlays the
 * whole viewport without the table reflowing/squeezing its columns around an inline
 * form - the bug an earlier inline-in-cell version of this had.
 */
function RecurringMarkDialog({
  transaction,
  onCreate,
  onDone,
  onCancel,
}: {
  transaction: Transaction
  onCreate: (frequency: RecurrenceFrequency) => Promise<unknown>
  onDone: () => void
  onCancel: () => void
}) {
  const [frequency, setFrequency] = useState<RecurrenceFrequency>('Monthly')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const label = transaction.cleanedLabel ?? transaction.rawLabel
  const isExpense = transaction.amount < 0

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onCancel()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onCancel])

  async function handleConfirm() {
    setIsSubmitting(true)
    setError(null)
    try {
      await onCreate(frequency)
      onDone()
    } catch (err) {
      setError(errorMessage(err))
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
        aria-labelledby="recurring-mark-dialog-title"
        className="w-full max-w-sm rounded-lg border border-border bg-surface p-4 shadow-xl"
      >
        <h3 id="recurring-mark-dialog-title" className="font-display font-medium text-heading">
          Marquer comme {isExpense ? 'dépense récurrente' : 'revenu récurrent'}
        </h3>
        <p className="mt-1 text-sm text-body">
          {label} · {formatCurrency(Math.abs(transaction.amount))}
        </p>

        <div className="mt-3">
          <label htmlFor="recurring-mark-frequency" className="block text-xs font-medium text-body">
            Fréquence
          </label>
          <select
            id="recurring-mark-frequency"
            value={frequency}
            onChange={(e) => setFrequency(e.target.value as RecurrenceFrequency)}
            className="mt-1 w-full rounded border border-border px-3 py-2 text-sm bg-field text-heading"
          >
            {(Object.entries(FREQUENCY_LABELS) as [RecurrenceFrequency, string][]).map(([value, text]) => (
              <option key={value} value={value}>
                {text}
              </option>
            ))}
          </select>
        </div>

        {error && (
          <p role="alert" className="mt-3 rounded bg-negative/10 px-3 py-2 text-sm text-negative">
            {error}
          </p>
        )}

        <div className="mt-4 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="rounded border border-border px-3 py-1.5 text-sm hover:bg-overlay"
          >
            Annuler
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            disabled={isSubmitting}
            className="rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover disabled:opacity-50"
          >
            {isSubmitting ? 'Création…' : 'Créer'}
          </button>
        </div>
      </div>
    </div>
  )
}
