import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { Category, RecurrenceFrequency, RecurringExpense } from '../api/types'
import { useConfirm } from '../components/confirmContext'
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
  const confirm = useConfirm()
  const [isCreating, setIsCreating] = useState(false)

  const expensesQuery = useQuery({
    queryKey: ['recurring-expenses'],
    queryFn: () => apiClient<RecurringExpense[]>('/api/recurring-expenses'),
  })

  function invalidate() {
    return queryClient.invalidateQueries({ queryKey: ['recurring-expenses'] })
  }

  async function handleDelete(expense: RecurringExpense) {
    if (
      !(await confirm(`Supprimer la dépense récurrente "${expense.label}" ?`, {
        confirmLabel: 'Supprimer',
        danger: true,
      }))
    ) {
      return
    }
    await apiClient(`/api/recurring-expenses/${expense.id}`, { method: 'DELETE' })
    await invalidate()
  }

  return (
    <section className="space-y-3 rounded-lg bg-surface p-4 shadow">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-display font-medium text-heading">Dépenses récurrentes</h2>
          <p className="text-sm text-muted">
            Comblent le prévisionnel des catégories sans budget explicite pour le mois affiché.
          </p>
        </div>
        {!isCreating && (
          <button
            onClick={() => setIsCreating(true)}
            className="shrink-0 rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover"
          >
            + Ajouter une dépense
          </button>
        )}
      </div>

      {isCreating && (
        <RecurringExpenseForm
          categories={categories}
          onSaved={invalidate}
          onCancel={() => setIsCreating(false)}
        />
      )}

      {expensesQuery.isPending && <p className="text-body">Chargement…</p>}

      {expensesQuery.data && expensesQuery.data.length === 0 && (
        <p className="text-sm text-muted">Aucune dépense récurrente pour l'instant.</p>
      )}

      {expensesQuery.data && expensesQuery.data.length > 0 && (
        <ul className="divide-y divide-border">
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
  // This form doubles as an inline per-row editor (ExpenseRow), and more than one row
  // can be in edit mode at once - ids scoped per expense (or "new" for the single create
  // instance) so two open forms never collide.
  const fieldId = (suffix: string) => `recurring-expense-${expense?.id ?? 'new'}-${suffix}`

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
    <form onSubmit={handleSubmit} className="space-y-2 border-b border-border pb-3">
      {error && (
        <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {error}
        </p>
      )}
      <div className="grid gap-2 sm:grid-cols-3 lg:grid-cols-6">
        <div className="sm:col-span-2">
          <label htmlFor={fieldId('label')} className="block text-xs font-medium text-body">
            Libellé
          </label>
          <input
            id={fieldId('label')}
            type="text"
            required
            placeholder="Loyer"
            value={label}
            onChange={(e) => setLabel(e.target.value)}
            className="mt-1 w-full rounded border border-border px-3 py-2 text-sm bg-field text-heading"
          />
        </div>
        <div>
          <label htmlFor={fieldId('amount')} className="block text-xs font-medium text-body">
            Montant
          </label>
          <input
            id={fieldId('amount')}
            type="number"
            step="0.01"
            min="0.01"
            required
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            className="mt-1 w-full rounded border border-border px-3 py-2 text-sm bg-field text-heading"
          />
        </div>
        <div>
          <label htmlFor={fieldId('category')} className="block text-xs font-medium text-body">
            Catégorie
          </label>
          <select
            id={fieldId('category')}
            value={categoryId}
            onChange={(e) => setCategoryId(e.target.value)}
            className="mt-1 w-full rounded border border-border px-3 py-2 text-sm bg-field text-heading"
          >
            <option value="">Sans catégorie</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor={fieldId('frequency')} className="block text-xs font-medium text-body">
            Fréquence
          </label>
          <select
            id={fieldId('frequency')}
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
        <div className="flex gap-2 sm:col-span-2 lg:col-span-2">
          <div className="w-full">
            <label htmlFor={fieldId('start-date')} className="block text-xs font-medium text-body">
              Date de début
            </label>
            <input
              id={fieldId('start-date')}
              type="date"
              required
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              className="mt-1 w-full rounded border border-border px-3 py-2 text-sm bg-field text-heading"
            />
          </div>
          <div className="w-full">
            <label htmlFor={fieldId('end-date')} className="block text-xs font-medium text-body">
              Date de fin (optionnelle)
            </label>
            <input
              id={fieldId('end-date')}
              type="date"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              className="mt-1 w-full rounded border border-border px-3 py-2 text-sm bg-field text-heading"
            />
          </div>
        </div>
      </div>
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover disabled:opacity-50"
        >
          {isSubmitting ? 'Enregistrement…' : expense ? 'Enregistrer' : 'Ajouter la dépense'}
        </button>
        {onCancel && (
          <button
            type="button"
            onClick={onCancel}
            className="rounded border border-border px-3 py-1.5 text-sm hover:bg-overlay"
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
      <span className="text-body">
        <span className="font-medium text-heading">{expense.label}</span> — {formatCurrency(expense.amount)} (
        {FREQUENCY_LABELS[expense.frequency]}) · {expense.categoryName ?? 'Sans catégorie'} · depuis{' '}
        {formatDateFr(expense.startDate)}
        {expense.endDate ? ` jusqu'au ${formatDateFr(expense.endDate)}` : ''}
      </span>
      <span className="flex shrink-0 gap-2">
        <button
          onClick={() => setIsEditing(true)}
          className="rounded border border-border px-2 py-1 text-xs hover:bg-overlay"
        >
          Modifier
        </button>
        <button
          onClick={onDelete}
          className="rounded border border-negative/40 px-2 py-1 text-xs text-negative hover:bg-negative/10"
        >
          Supprimer
        </button>
      </span>
    </li>
  )
}
