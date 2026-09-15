import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { BudgetLine, Category } from '../api/types'
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

  const budgetsQuery = useQuery({
    queryKey: ['budgets', month],
    queryFn: () => apiClient<BudgetLine[]>(`/api/budgets?month=${month}`),
  })

  function invalidate() {
    return queryClient.invalidateQueries({ queryKey: ['budgets', month] })
  }

  async function handleDelete(line: BudgetLine) {
    if (!confirm(`Supprimer le budget "${line.categoryName}" pour ce mois ?`)) {
      return
    }
    await apiClient(`/api/budgets/${line.id}`, { method: 'DELETE' })
    await invalidate()
  }

  const budgetedCategoryIds = new Set(budgetsQuery.data?.map((line) => line.categoryId) ?? [])
  const availableCategories = categories.filter((c) => !budgetedCategoryIds.has(c.id))
  const sortedLines = [...(budgetsQuery.data ?? [])].sort((a, b) => a.categoryName.localeCompare(b.categoryName))

  return (
    <section className="space-y-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800">
      <h2 className="font-medium text-gray-900 dark:text-white">Budget du mois</h2>
      <p className="text-sm text-gray-500 dark:text-gray-400">
        Un montant planifié pour une catégorie prime toujours sur une dépense récurrente ce mois-là.
      </p>

      {budgetsQuery.isPending && <p className="text-gray-600 dark:text-gray-300">Chargement…</p>}

      {budgetsQuery.isError && (
        <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {errorMessage(budgetsQuery.error)}
        </p>
      )}

      {budgetsQuery.data && (
        <CreateBudgetLineForm month={month} categories={availableCategories} onCreated={invalidate} />
      )}

      {budgetsQuery.data && sortedLines.length === 0 && (
        <p className="text-sm text-gray-500 dark:text-gray-400">Aucune ligne de budget pour ce mois.</p>
      )}

      {sortedLines.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-gray-200 text-xs uppercase text-gray-500 dark:border-gray-700 dark:text-gray-400">
              <tr>
                <th className="py-2">Catégorie</th>
                <th className="py-2 text-right">Planifié</th>
                <th className="py-2 text-right">Réel</th>
                <th className="py-2" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
              {sortedLines.map((line) => (
                <BudgetLineRow key={line.id} line={line} onSaved={invalidate} onDelete={() => handleDelete(line)} />
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
}: {
  month: string
  categories: Category[]
  onCreated: () => void
}) {
  const apiClient = useApiClient()
  const [categoryId, setCategoryId] = useState(categories[0]?.id.toString() ?? '')
  const [plannedAmount, setPlannedAmount] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const currentCategoryId = categories.some((c) => c.id.toString() === categoryId) ? categoryId : (categories[0]?.id.toString() ?? '')

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
    return (
      <p className="text-sm text-gray-500 dark:text-gray-400">
        Toutes les catégories ont déjà un budget pour ce mois.
      </p>
    )
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-2 border-b border-gray-100 pb-3 dark:border-gray-700">
      {error && (
        <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}
      <div className="grid gap-2 sm:grid-cols-3">
        <select
          aria-label="Catégorie du budget"
          value={currentCategoryId}
          onChange={(e) => setCategoryId(e.target.value)}
          className="rounded border border-gray-300 px-3 py-2 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        >
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </select>
        <input
          aria-label="Montant planifié"
          type="number"
          step="0.01"
          min="0.01"
          required
          value={plannedAmount}
          onChange={(e) => setPlannedAmount(e.target.value)}
          placeholder="Montant planifié"
          className="rounded border border-gray-300 px-3 py-2 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-sky-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-sky-700 disabled:opacity-50"
        >
          {isSubmitting ? 'Ajout…' : 'Ajouter la ligne'}
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
      await apiClient(`/api/budgets/${line.id}`, { method: 'PUT', body: { plannedAmount: Number(plannedAmount) } })
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
      <td className="py-2 text-gray-900 dark:text-white">{line.categoryName}</td>
      <td className="py-2 text-right">
        {isEditing ? (
          <form onSubmit={handleSave} className="flex items-center justify-end gap-2">
            {error && <span className="text-xs text-red-700 dark:text-red-400">{error}</span>}
            <input
              aria-label={`Montant planifié pour ${line.categoryName}`}
              type="number"
              step="0.01"
              min="0.01"
              autoFocus
              value={plannedAmount}
              onChange={(e) => setPlannedAmount(e.target.value)}
              className="w-24 rounded border border-gray-300 px-2 py-1 text-right text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            />
            <button
              type="submit"
              disabled={isSaving}
              className="rounded bg-sky-600 px-2 py-1 text-xs font-medium text-white hover:bg-sky-700 disabled:opacity-50"
            >
              OK
            </button>
          </form>
        ) : (
          <span className="text-gray-600 dark:text-gray-300">{formatCurrency(line.plannedAmount)}</span>
        )}
      </td>
      <td className={`py-2 text-right ${isOverBudget ? 'text-red-700 dark:text-red-400' : 'text-gray-600 dark:text-gray-300'}`}>
        {formatCurrency(line.actualAmount)}
      </td>
      <td className="py-2 text-right">
        <div className="flex justify-end gap-2">
          {!isEditing && (
            <button
              onClick={() => setIsEditing(true)}
              className="rounded border border-gray-300 px-2 py-1 text-xs hover:bg-gray-100 dark:border-gray-600 dark:hover:bg-gray-700"
            >
              Modifier
            </button>
          )}
          <button
            onClick={onDelete}
            className="rounded border border-red-300 px-2 py-1 text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
          >
            Supprimer
          </button>
        </div>
      </td>
    </tr>
  )
}
