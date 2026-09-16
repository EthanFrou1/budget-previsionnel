import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { BudgetLine, Category } from '../api/types'
import { useConfirm } from '../components/confirmContext'
import { useApiClient } from '../auth/useApiClient'
import { formatCurrency } from '../utils/format'

function errorMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : 'Une erreur est survenue.'
}

/**
 * Budget CRUD (backend since Lot 7, never surfaced by a screen until this lot): an
 * explicit planned amount for a category/month, which ForecastService prefers over a
 * RecurringExpense guess for the same category. One line per (month, category) - the
 * API 409s on a duplicate, so the "add" form only offers categories not already
 * budgeted for the displayed month.
 */
export function BudgetSection({ month, categories }: { month: string; categories: Category[] }) {
  const apiClient = useApiClient()
  const queryClient = useQueryClient()
  const confirm = useConfirm()
  const [isCreating, setIsCreating] = useState(false)

  const budgetsQuery = useQuery({
    queryKey: ['budgets', month],
    queryFn: () => apiClient<BudgetLine[]>(`/api/budgets?month=${month}`),
  })

  function invalidate() {
    return queryClient.invalidateQueries({ queryKey: ['budgets', month] })
  }

  async function handleDelete(line: BudgetLine) {
    if (
      !(await confirm(`Supprimer le budget "${line.categoryName}" pour ce mois ?`, {
        confirmLabel: 'Supprimer',
        danger: true,
      }))
    ) {
      return
    }
    await apiClient(`/api/budgets/${line.id}`, { method: 'DELETE' })
    await invalidate()
  }

  const budgetedCategoryIds = new Set(budgetsQuery.data?.map((line) => line.categoryId) ?? [])
  const availableCategories = categories.filter((c) => !budgetedCategoryIds.has(c.id))
  const sortedLines = [...(budgetsQuery.data ?? [])].sort((a, b) =>
    a.categoryName.localeCompare(b.categoryName),
  )

  return (
    <section className="space-y-3 rounded-lg bg-surface p-4 shadow">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-display font-medium text-heading">Budget du mois</h2>
          <p className="text-sm text-muted">
            Un montant planifié pour une catégorie prime toujours sur une dépense récurrente ce mois-là.
          </p>
        </div>
        {!isCreating && availableCategories.length > 0 && (
          <button
            onClick={() => setIsCreating(true)}
            className="shrink-0 rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover"
          >
            + Ajouter une ligne
          </button>
        )}
      </div>

      {budgetsQuery.isPending && <p className="text-body">Chargement…</p>}

      {budgetsQuery.isError && (
        <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {errorMessage(budgetsQuery.error)}
        </p>
      )}

      {budgetsQuery.data && isCreating && (
        <CreateBudgetLineForm
          month={month}
          categories={availableCategories}
          onCreated={() => {
            invalidate()
            setIsCreating(false)
          }}
          onCancel={() => setIsCreating(false)}
        />
      )}

      {budgetsQuery.data && sortedLines.length === 0 && (
        <p className="text-sm text-muted">Aucune ligne de budget pour ce mois.</p>
      )}

      {sortedLines.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-border text-xs uppercase text-muted">
              <tr>
                <th className="py-2">Catégorie</th>
                <th className="py-2 text-right">Planifié</th>
                <th className="py-2 text-right">Réel</th>
                <th className="py-2" />
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {sortedLines.map((line) => (
                <BudgetLineRow
                  key={line.id}
                  line={line}
                  onSaved={invalidate}
                  onDelete={() => handleDelete(line)}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

function CreateBudgetLineForm({
  month,
  categories,
  onCreated,
  onCancel,
}: {
  month: string
  categories: Category[]
  onCreated: () => void
  onCancel: () => void
}) {
  const apiClient = useApiClient()
  const [categoryId, setCategoryId] = useState(categories[0]?.id.toString() ?? '')
  const [plannedAmount, setPlannedAmount] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const currentCategoryId = categories.some((c) => c.id.toString() === categoryId)
    ? categoryId
    : (categories[0]?.id.toString() ?? '')

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!currentCategoryId) {
      setError('Toutes les catégories ont déjà un budget ce mois-ci.')
      return
    }
    setError(null)
    setIsSubmitting(true)
    try {
      await apiClient('/api/budgets', {
        method: 'POST',
        body: { month, categoryId: Number(currentCategoryId), plannedAmount: Number(plannedAmount) },
      })
      setPlannedAmount('')
      onCreated()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  if (categories.length === 0) {
    return <p className="text-sm text-muted">Toutes les catégories ont déjà un budget pour ce mois.</p>
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-2 border-b border-border pb-3">
      {error && (
        <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {error}
        </p>
      )}
      <div className="grid gap-2 sm:grid-cols-2">
        <div>
          <label htmlFor="budget-category" className="block text-xs font-medium text-body">
            Catégorie
          </label>
          <select
            id="budget-category"
            value={currentCategoryId}
            onChange={(e) => setCategoryId(e.target.value)}
            className="mt-1 w-full rounded border border-border px-3 py-2 text-sm bg-field text-heading"
          >
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="budget-planned-amount" className="block text-xs font-medium text-body">
            Montant planifié
          </label>
          <input
            id="budget-planned-amount"
            type="number"
            step="0.01"
            min="0.01"
            required
            value={plannedAmount}
            onChange={(e) => setPlannedAmount(e.target.value)}
            className="mt-1 w-full rounded border border-border px-3 py-2 text-sm bg-field text-heading"
          />
        </div>
      </div>
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover disabled:opacity-50"
        >
          {isSubmitting ? 'Ajout…' : 'Ajouter la ligne'}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="rounded border border-border px-3 py-1.5 text-sm text-body hover:bg-overlay"
        >
          Annuler
        </button>
      </div>
    </form>
  )
}

function BudgetLineRow({
  line,
  onSaved,
  onDelete,
}: {
  line: BudgetLine
  onSaved: () => void
  onDelete: () => void
}) {
  const apiClient = useApiClient()
  const [isEditing, setIsEditing] = useState(false)
  const [plannedAmount, setPlannedAmount] = useState(String(line.plannedAmount))
  const [error, setError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  async function handleSave(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsSaving(true)
    try {
      await apiClient(`/api/budgets/${line.id}`, {
        method: 'PUT',
        body: { plannedAmount: Number(plannedAmount) },
      })
      setIsEditing(false)
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setIsSaving(false)
    }
  }

  const isOverBudget = line.actualAmount > line.plannedAmount

  return (
    <tr>
      <td className="py-2 text-heading">{line.categoryName}</td>
      <td className="py-2 text-right">
        {isEditing ? (
          <form onSubmit={handleSave} className="flex items-center justify-end gap-2">
            {error && <span className="text-xs text-negative">{error}</span>}
            <input
              aria-label={`Montant planifié pour ${line.categoryName}`}
              type="number"
              step="0.01"
              min="0.01"
              autoFocus
              value={plannedAmount}
              onChange={(e) => setPlannedAmount(e.target.value)}
              className="w-24 rounded border border-border px-2 py-1 text-right text-sm bg-field text-heading"
            />
            <button
              type="submit"
              disabled={isSaving}
              className="rounded bg-accent px-2 py-1 text-xs font-medium text-white hover:bg-accent-hover disabled:opacity-50"
            >
              OK
            </button>
          </form>
        ) : (
          <span className="text-body">{formatCurrency(line.plannedAmount)}</span>
        )}
      </td>
      <td className={`py-2 text-right ${isOverBudget ? 'text-negative' : 'text-body'}`}>
        {formatCurrency(line.actualAmount)}
      </td>
      <td className="py-2 text-right">
        <div className="flex justify-end gap-2">
          {!isEditing && (
            <button
              onClick={() => setIsEditing(true)}
              className="rounded border border-border px-2 py-1 text-xs hover:bg-overlay"
            >
              Modifier
            </button>
          )}
          <button
            onClick={onDelete}
            className="rounded border border-negative/40 px-2 py-1 text-xs text-negative hover:bg-negative/10"
          >
            Supprimer
          </button>
        </div>
      </td>
    </tr>
  )
}
