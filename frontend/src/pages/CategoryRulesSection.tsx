import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { Category, CategoryRule } from '../api/types'
import { useConfirm } from '../components/confirmContext'
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
  const confirm = useConfirm()
  const [isCreating, setIsCreating] = useState(false)

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
    if (
      !(await confirm('Supprimer cette règle de catégorisation ?', {
        confirmLabel: 'Supprimer',
        danger: true,
      }))
    ) {
      return
    }
    await apiClient(`/api/category-rules/${ruleId}`, { method: 'DELETE' })
    await invalidateRules()
  }

  return (
    <section className="space-y-3 rounded-lg bg-surface p-4 shadow">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-display font-medium text-heading">Règles de catégorisation</h2>
          <p className="text-sm text-muted">
            Appliquées aux prochains imports quand la banque ne suggère pas de catégorie exacte.
          </p>
        </div>
        {!isCreating && (
          <button
            onClick={() => setIsCreating(true)}
            className="shrink-0 rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover"
          >
            + Ajouter une règle
          </button>
        )}
      </div>

      {isCreating && (
        <CreateRuleForm
          categories={categories}
          onCreated={() => {
            invalidateRules()
            setIsCreating(false)
          }}
          onCancel={() => setIsCreating(false)}
        />
      )}

      {rulesQuery.isPending && <p className="text-body">Chargement…</p>}

      {rulesQuery.data && rulesQuery.data.length === 0 && (
        <p className="text-sm text-muted">Aucune règle pour l'instant.</p>
      )}

      {rulesQuery.data && rulesQuery.data.length > 0 && (
        <ul className="divide-y divide-border">
          {[...rulesQuery.data]
            .sort((a, b) => a.priority - b.priority)
            .map((rule) => (
              <li key={rule.id} className="flex items-center justify-between gap-2 py-2 text-sm">
                <span className="text-body">
                  <span className="font-mono">"{rule.matchPattern}"</span> → {categoryName(rule.categoryId)}{' '}
                  <span className="text-muted">(priorité {rule.priority})</span>
                </span>
                <button
                  onClick={() => handleDelete(rule.id)}
                  className="rounded border border-negative/40 px-2 py-1 text-xs text-negative hover:bg-negative/10"
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

function CreateRuleForm({
  categories,
  onCreated,
  onCancel,
}: {
  categories: Category[]
  onCreated: () => void
  onCancel: () => void
}) {
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
    <form onSubmit={handleSubmit} className="space-y-2 border-b border-border pb-3">
      {error && (
        <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {error}
        </p>
      )}
      <div className="grid gap-2 sm:grid-cols-4">
        <div className="sm:col-span-2">
          <label htmlFor="rule-pattern" className="block text-xs font-medium text-body">
            Motif à rechercher
          </label>
          <input
            id="rule-pattern"
            type="text"
            required
            placeholder="Ex : NETFLIX"
            value={matchPattern}
            onChange={(e) => setMatchPattern(e.target.value)}
            className="mt-1 w-full rounded border border-border px-3 py-2 text-sm bg-field text-heading"
          />
        </div>
        <div>
          <label htmlFor="rule-category" className="block text-xs font-medium text-body">
            Catégorie
          </label>
          <select
            id="rule-category"
            value={categoryId}
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
          <label htmlFor="rule-priority" className="block text-xs font-medium text-body">
            Priorité
          </label>
          <input
            id="rule-priority"
            type="number"
            value={priority}
            onChange={(e) => setPriority(e.target.value)}
            className="mt-1 w-full rounded border border-border px-3 py-2 text-sm bg-field text-heading"
          />
          <p className="mt-1 text-xs text-muted">Le plus petit numéro est testé en premier.</p>
        </div>
      </div>
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover disabled:opacity-50"
        >
          {isSubmitting ? 'Ajout…' : 'Ajouter la règle'}
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
