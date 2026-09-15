import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { Category, RecurrenceFrequency, RecurringExpense } from '../api/types'
import { useApiClient } from '../auth/useApiClient'
import { formatCurrency, formatDateFr } from '../utils/format'

const FREQUENCY_LABELS: Record<RecurrenceFrequency, string> = {
  Weekly: 'Hebdomadaire',
  Monthly: 'Mensuelle',
  Yearly: 'Annuelle',
}

function errorMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : 'Une erreur est survenue.'
}

/**
 * RecurringExpense CRUD (backend since Lot 6, never surfaced by a screen until this
 * lot): fills the forecast for any category without an explicit Budget entry for that
 * month (ForecastService's precedence rule). Managed here rather than its own route -
 * it only matters in the context of the forecast it feeds.
 */
export function RecurringExpensesSection({ categories }: { categories: Category[] }) {
  const apiClient = useApiClient()
  const queryClient = useQueryClient()

  const expensesQuery = useQuery({
    queryKey: ['recurring-expenses'],
    queryFn: () => apiClient<RecurringExpense[]>('/api/recurring-expenses'),
  })

  function invalidate() {
    return queryClient.invalidateQueries({ queryKey: ['recurring-expenses'] })
  }

  async function handleDelete(expense: RecurringExpense) {
    if (!confirm(`Supprimer la dépense récurrente "${expense.label}" ?`)) {
      return
    }
    await apiClient(`/api/recurring-expenses/${expense.id}`, { method: 'DELETE' })
    await invalidate()
  }

  return (
    <section className="space-y-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800">
      <h2 className="font-medium text-gray-900 dark:text-white">Dépenses récurrentes</h2>
      <p className="text-sm text-gray-500 dark:text-gray-400">
        Comblent le prévisionnel des catégories sans budget explicite pour le mois affiché.
      </p>

      <RecurringExpenseForm categories={categories} onSaved={invalidate} />

      {expensesQuery.isPending && <p className="text-gray-600 dark:text-gray-300">Chargement…</p>}

      {expensesQuery.data && expensesQuery.data.length === 0 && (
        <p className="text-sm text-gray-500 dark:text-gray-400">Aucune dépense récurrente pour l'instant.</p>
      )}

      {expensesQuery.data && expensesQuery.data.length > 0 && (
        <ul className="divide-y divide-gray-100 dark:divide-gray-700">
          {expensesQuery.data.map((expense) => (
            <ExpenseRow
              key={expense.id}
              expense={expense}
              categories={categories}
              onSaved={invalidate}
              onDelete={() => handleDelete(expense)}
            />
          ))}
        </ul>
      )}
    </section>
  )
}

function RecurringExpenseForm({
  categories,
  expense,
  onSaved,
  onCancel,
}: {
  categories: Category[]
  expense?: RecurringExpense
  onSaved: () => void
  onCancel?: () => void
}) {
  const apiClient = useApiClient()
  const [label, setLabel] = useState(expense?.label ?? '')
  const [amount, setAmount] = useState(expense ? String(expense.amount) : '')
  const [categoryId, setCategoryId] = useState(expense?.categoryId?.toString() ?? '')
  const [frequency, setFrequency] = useState<RecurrenceFrequency>(expense?.frequency ?? 'Monthly')
  const [startDate, setStartDate] = useState(expense?.startDate ?? '')
  const [endDate, setEndDate] = useState(expense?.endDate ?? '')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      const body = {
        label,
        amount: Number(amount),
        categoryId: categoryId ? Number(categoryId) : null,
        frequency,
        startDate,
        endDate: endDate || null,
      }
      if (expense) {
        await apiClient(`/api/recurring-expenses/${expense.id}`, { method: 'PUT', body })
      } else {
        await apiClient('/api/recurring-expenses', { method: 'POST', body })
        setLabel('')
        setAmount('')
        setCategoryId('')
        setFrequency('Monthly')
        setStartDate('')
        setEndDate('')
      }
      onSaved()
      onCancel?.()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-2 border-b border-gray-100 pb-3 dark:border-gray-700">
      {error && (
        <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}
      <div className="grid gap-2 sm:grid-cols-3 lg:grid-cols-6">
        <input
          aria-label="Libellé de la dépense récurrente"
          type="text"
          required
          placeholder="Loyer"
          value={label}
          onChange={(e) => setLabel(e.target.value)}
          className="rounded border border-gray-300 px-3 py-2 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white sm:col-span-2"
        />
        <input
          aria-label="Montant"
          type="number"
          step="0.01"
          min="0.01"
          required
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          className="rounded border border-gray-300 px-3 py-2 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
        <select
          aria-label="Catégorie de la dépense récurrente"
          value={categoryId}
          onChange={(e) => setCategoryId(e.target.value)}
          className="rounded border border-gray-300 px-3 py-2 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        >
          <option value="">Sans catégorie</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </select>
        <select
          aria-label="Fréquence"
          value={frequency}
          onChange={(e) => setFrequency(e.target.value as RecurrenceFrequency)}
          className="rounded border border-gray-300 px-3 py-2 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        >
          {(Object.entries(FREQUENCY_LABELS) as [RecurrenceFrequency, string][]).map(([value, text]) => (
            <option key={value} value={value}>
              {text}
            </option>
          ))}
        </select>
        <div className="flex gap-2 sm:col-span-2 lg:col-span-2">
          <input
            aria-label="Date de début"
            type="date"
            required
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
            className="w-full rounded border border-gray-300 px-3 py-2 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          />
          <input
            aria-label="Date de fin (optionnelle)"
            type="date"
            value={endDate}
            onChange={(e) => setEndDate(e.target.value)}
            className="w-full rounded border border-gray-300 px-3 py-2 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          />
        </div>
      </div>
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-sky-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-sky-700 disabled:opacity-50"
        >
          {isSubmitting ? 'Enregistrement…' : expense ? 'Enregistrer' : 'Ajouter la dépense'}
        </button>
        {onCancel && (
          <button
            type="button"
            onClick={onCancel}
            className="rounded border border-gray-300 px-3 py-1.5 text-sm hover:bg-gray-100 dark:border-gray-600 dark:hover:bg-gray-700"
          >
            Annuler
          </button>
        )}
      </div>
    </form>
  )
}

function ExpenseRow({
  expense,
  categories,
  onSaved,
  onDelete,
}: {
  expense: RecurringExpense
  categories: Category[]
  onSaved: () => void
  onDelete: () => void
}) {
  const [isEditing, setIsEditing] = useState(false)

  if (isEditing) {
    return (
      <li className="py-2">
        <RecurringExpenseForm
          categories={categories}
          expense={expense}
          onSaved={onSaved}
          onCancel={() => setIsEditing(false)}
        />
      </li>
    )
  }

  return (
    <li className="flex items-center justify-between gap-2 py-2 text-sm">
      <span className="text-gray-700 dark:text-gray-300">
        <span className="font-medium text-gray-900 dark:text-white">{expense.label}</span> —{' '}
        {formatCurrency(expense.amount)} ({FREQUENCY_LABELS[expense.frequency]}) ·{' '}
        {expense.categoryName ?? 'Sans catégorie'} · depuis {formatDateFr(expense.startDate)}
        {expense.endDate ? ` jusqu'au ${formatDateFr(expense.endDate)}` : ''}
      </span>
      <span className="flex shrink-0 gap-2">
        <button
          onClick={() => setIsEditing(true)}
          className="rounded border border-gray-300 px-2 py-1 text-xs hover:bg-gray-100 dark:border-gray-600 dark:hover:bg-gray-700"
        >
          Modifier
        </button>
        <button
          onClick={onDelete}
          className="rounded border border-red-300 px-2 py-1 text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
        >
          Supprimer
        </button>
      </span>
    </li>
  )
}
