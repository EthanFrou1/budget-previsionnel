import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { Category, CategoryRule } from '../api/types'
import { useApiClient } from '../auth/useApiClient'

function errorMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : 'Une erreur est survenue.'
}

/**
 * CategoryRule engine (Lot 4): matches a transaction's label against MatchPattern when no
 * exact bank-category mapping applies at import time. Managed here rather than a separate
 * route - rules only make sense in the context of the transactions they'll categorize.
 */
export function CategoryRulesSection({ categories }: { categories: Category[] }) {
  const apiClient = useApiClient()
  const queryClient = useQueryClient()

  const rulesQuery = useQuery({
    queryKey: ['category-rules'],
    queryFn: () => apiClient<CategoryRule[]>('/api/category-rules'),
  })

  function invalidateRules() {
    return queryClient.invalidateQueries({ queryKey: ['category-rules'] })
  }

  function categoryName(categoryId: number): string {
    return categories.find((c) => c.id === categoryId)?.name ?? `Catégorie #${categoryId}`
  }

  async function handleDelete(ruleId: number) {
    if (!confirm('Supprimer cette règle de catégorisation ?')) {
      return
    }
    await apiClient(`/api/category-rules/${ruleId}`, { method: 'DELETE' })
    await invalidateRules()
  }

  return (
    <section className="space-y-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800">
      <h2 className="font-medium text-gray-900 dark:text-white">Règles de catégorisation</h2>
      <p className="text-sm text-gray-500 dark:text-gray-400">
        Appliquées aux prochains imports quand la banque ne suggère pas de catégorie exacte.
      </p>

      <CreateRuleForm categories={categories} onCreated={invalidateRules} />

      {rulesQuery.isPending && <p className="text-gray-600 dark:text-gray-300">Chargement…</p>}

      {rulesQuery.data && rulesQuery.data.length === 0 && (
        <p className="text-sm text-gray-500 dark:text-gray-400">Aucune règle pour l'instant.</p>
      )}

      {rulesQuery.data && rulesQuery.data.length > 0 && (
        <ul className="divide-y divide-gray-100 dark:divide-gray-700">
          {[...rulesQuery.data]
            .sort((a, b) => a.priority - b.priority)
            .map((rule) => (
              <li key={rule.id} className="flex items-center justify-between gap-2 py-2 text-sm">
                <span className="text-gray-700 dark:text-gray-300">
                  <span className="font-mono">"{rule.matchPattern}"</span> → {categoryName(rule.categoryId)}{' '}
                  <span className="text-gray-400 dark:text-gray-500">(priorité {rule.priority})</span>
                </span>
                <button
                  onClick={() => handleDelete(rule.id)}
                  className="rounded border border-red-300 px-2 py-1 text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
                >
                  Supprimer
                </button>
              </li>
            ))}
        </ul>
      )}
    </section>
  )
}

function CreateRuleForm({ categories, onCreated }: { categories: Category[]; onCreated: () => void }) {
  const apiClient = useApiClient()
  const [matchPattern, setMatchPattern] = useState('')
  const [categoryId, setCategoryId] = useState(categories[0]?.id.toString() ?? '')
  const [priority, setPriority] = useState('100')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!categoryId) {
      setError('Choisis une catégorie.')
      return
    }
    setError(null)
    setIsSubmitting(true)
    try {
      await apiClient('/api/category-rules', {
        method: 'POST',
        body: { matchPattern, categoryId: Number(categoryId), priority: Number(priority) || 0 },
      })
      setMatchPattern('')
      onCreated()
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
      <div className="grid gap-2 sm:grid-cols-4">
        <input
          aria-label="Motif à rechercher dans le libellé"
          type="text"
          required
          placeholder="Motif (ex : NETFLIX)"
          value={matchPattern}
          onChange={(e) => setMatchPattern(e.target.value)}
          className="rounded border border-gray-300 px-3 py-2 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white sm:col-span-2"
        />
        <select
          aria-label="Catégorie de la règle"
          value={categoryId}
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
          aria-label="Priorité"
          type="number"
          value={priority}
          onChange={(e) => setPriority(e.target.value)}
          className="rounded border border-gray-300 px-3 py-2 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
      </div>
      <button
        type="submit"
        disabled={isSubmitting}
        className="rounded bg-sky-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-sky-700 disabled:opacity-50"
      >
        {isSubmitting ? 'Ajout…' : 'Ajouter la règle'}
      </button>
    </form>
  )
}
