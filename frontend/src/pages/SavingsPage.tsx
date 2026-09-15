import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { BankAccount, SavingsGoal } from '../api/types'
import { AppHeader } from '../components/AppHeader'
import { useApiClient } from '../auth/useApiClient'
import { formatCurrency, formatDateFr } from '../utils/format'

function errorMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : 'Une erreur est survenue.'
}

function progressPercent(goal: Pick<SavingsGoal, 'currentAmount' | 'targetAmount'>): number {
  if (goal.targetAmount <= 0) {
    return 0
  }
  return Math.min(100, Math.round((goal.currentAmount / goal.targetAmount) * 100))
}

export function SavingsPage() {
  const apiClient = useApiClient()
  const queryClient = useQueryClient()

  const goalsQuery = useQuery({
    queryKey: ['savings-goals'],
    queryFn: () => apiClient<SavingsGoal[]>('/api/savings-goals'),
  })
  const accountsQuery = useQuery({
    queryKey: ['bank-accounts'],
    queryFn: () => apiClient<BankAccount[]>('/api/bank-accounts'),
  })

  function invalidateGoals() {
    return queryClient.invalidateQueries({ queryKey: ['savings-goals'] })
  }

  const accounts = accountsQuery.data ?? []

  return (
    <div className="min-h-svh bg-gray-50 dark:bg-gray-900">
      <AppHeader />

      <main className="mx-auto max-w-3xl space-y-6 p-4">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-white">Épargne</h1>

        <CreateGoalForm accounts={accounts} onCreated={invalidateGoals} />

        {goalsQuery.isPending && <p className="text-gray-600 dark:text-gray-300">Chargement…</p>}

        {goalsQuery.isError && (
          <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
            {errorMessage(goalsQuery.error)}
          </p>
        )}

        {goalsQuery.data && goalsQuery.data.length === 0 && (
          <p className="text-gray-600 dark:text-gray-300">Aucun objectif d'épargne pour l'instant.</p>
        )}

        <ul className="space-y-4">
          {goalsQuery.data?.map((goal) => (
            <li key={goal.id}>
              <GoalCard goal={goal} accounts={accounts} onChanged={invalidateGoals} />
            </li>
          ))}
        </ul>
      </main>
    </div>
  )
}

function GoalFields({
  idPrefix,
  label,
  setLabel,
  targetAmount,
  setTargetAmount,
  currentAmount,
  setCurrentAmount,
  targetDate,
  setTargetDate,
  linkedAccountId,
  setLinkedAccountId,
  accounts,
}: {
  idPrefix: string
  label: string
  setLabel: (v: string) => void
  targetAmount: string
  setTargetAmount: (v: string) => void
  currentAmount: string
  setCurrentAmount: (v: string) => void
  targetDate: string
  setTargetDate: (v: string) => void
  linkedAccountId: string
  setLinkedAccountId: (v: string) => void
  accounts: BankAccount[]
}) {
  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <div>
        <label htmlFor={`${idPrefix}-label`} className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Libellé
        </label>
        <input
          id={`${idPrefix}-label`}
          type="text"
          required
          value={label}
          onChange={(e) => setLabel(e.target.value)}
          placeholder="Fonds d'urgence"
          className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
      </div>

      <div>
        <label htmlFor={`${idPrefix}-target`} className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Montant visé
        </label>
        <input
          id={`${idPrefix}-target`}
          type="number"
          step="0.01"
          min="0.01"
          required
          value={targetAmount}
          onChange={(e) => setTargetAmount(e.target.value)}
          className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
      </div>

      <div>
        <label htmlFor={`${idPrefix}-current`} className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Montant actuel
        </label>
        <input
          id={`${idPrefix}-current`}
          type="number"
          step="0.01"
          min="0"
          required
          value={currentAmount}
          onChange={(e) => setCurrentAmount(e.target.value)}
          className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
      </div>

      <div>
        <label htmlFor={`${idPrefix}-date`} className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Échéance (optionnelle)
        </label>
        <input
          id={`${idPrefix}-date`}
          type="date"
          value={targetDate}
          onChange={(e) => setTargetDate(e.target.value)}
          className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
      </div>

      <div className="sm:col-span-2 lg:col-span-4">
        <label htmlFor={`${idPrefix}-account`} className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Compte lié (optionnel)
        </label>
        <select
          id={`${idPrefix}-account`}
          value={linkedAccountId}
          onChange={(e) => setLinkedAccountId(e.target.value)}
          className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        >
          <option value="">Aucun compte lié</option>
          {accounts.map((account) => (
            <option key={account.id} value={account.id}>
              {account.label}
            </option>
          ))}
        </select>
      </div>
    </div>
  )
}

function CreateGoalForm({ accounts, onCreated }: { accounts: BankAccount[]; onCreated: () => void }) {
  const apiClient = useApiClient()
  const [label, setLabel] = useState('')
  const [targetAmount, setTargetAmount] = useState('')
  const [currentAmount, setCurrentAmount] = useState('0')
  const [targetDate, setTargetDate] = useState('')
  const [linkedAccountId, setLinkedAccountId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await apiClient('/api/savings-goals', {
        method: 'POST',
        body: {
          label,
          targetAmount: Number(targetAmount),
          currentAmount: Number(currentAmount) || 0,
          targetDate: targetDate || null,
          linkedAccountId: linkedAccountId ? Number(linkedAccountId) : null,
        },
      })
      setLabel('')
      setTargetAmount('')
      setCurrentAmount('0')
      setTargetDate('')
      setLinkedAccountId('')
      onCreated()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800">
      <h2 className="font-medium text-gray-900 dark:text-white">Ajouter un objectif</h2>

      {error && (
        <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      <GoalFields
        idPrefix="goal-create"
        label={label}
        setLabel={setLabel}
        targetAmount={targetAmount}
        setTargetAmount={setTargetAmount}
        currentAmount={currentAmount}
        setCurrentAmount={setCurrentAmount}
        targetDate={targetDate}
        setTargetDate={setTargetDate}
        linkedAccountId={linkedAccountId}
        setLinkedAccountId={setLinkedAccountId}
        accounts={accounts}
      />

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

function GoalCard({
  goal,
  accounts,
  onChanged,
}: {
  goal: SavingsGoal
  accounts: BankAccount[]
  onChanged: () => void
}) {
  const apiClient = useApiClient()
  const [isEditing, setIsEditing] = useState(false)
  const [label, setLabel] = useState(goal.label)
  const [targetAmount, setTargetAmount] = useState(String(goal.targetAmount))
  const [currentAmount, setCurrentAmount] = useState(String(goal.currentAmount))
  const [targetDate, setTargetDate] = useState(goal.targetDate ?? '')
  const [linkedAccountId, setLinkedAccountId] = useState(goal.linkedAccountId?.toString() ?? '')
  const [error, setError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  const linkedAccount = accounts.find((a) => a.id === goal.linkedAccountId)

  async function handleSave(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsSaving(true)
    try {
      await apiClient(`/api/savings-goals/${goal.id}`, {
        method: 'PUT',
        body: {
          label,
          targetAmount: Number(targetAmount),
          currentAmount: Number(currentAmount) || 0,
          targetDate: targetDate || null,
          linkedAccountId: linkedAccountId ? Number(linkedAccountId) : null,
        },
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
    if (!confirm(`Supprimer l'objectif "${goal.label}" ?`)) {
      return
    }
    setError(null)
    try {
      await apiClient(`/api/savings-goals/${goal.id}`, { method: 'DELETE' })
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
        <GoalFields
          idPrefix={`goal-${goal.id}`}
          label={label}
          setLabel={setLabel}
          targetAmount={targetAmount}
          setTargetAmount={setTargetAmount}
          currentAmount={currentAmount}
          setCurrentAmount={setCurrentAmount}
          targetDate={targetDate}
          setTargetDate={setTargetDate}
          linkedAccountId={linkedAccountId}
          setLinkedAccountId={setLinkedAccountId}
          accounts={accounts}
        />
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

  const percent = progressPercent(goal)

  return (
    <div className="space-y-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800">
      {error && (
        <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      <div className="flex items-start justify-between">
        <div>
          <p className="font-medium text-gray-900 dark:text-white">{goal.label}</p>
          <p className="text-sm text-gray-500 dark:text-gray-400">
            {formatCurrency(goal.currentAmount)} / {formatCurrency(goal.targetAmount)}
            {goal.targetDate ? ` · échéance ${formatDateFr(goal.targetDate)}` : ''}
            {linkedAccount ? ` · ${linkedAccount.label}` : ''}
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

      <div
        role="progressbar"
        aria-valuenow={percent}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`Progression de l'objectif ${goal.label}`}
        className="h-2 w-full overflow-hidden rounded-full bg-gray-100 dark:bg-gray-700"
      >
        <div className="h-full rounded-full bg-sky-600" style={{ width: `${percent}%` }} />
      </div>
      <p className="text-xs text-gray-400 dark:text-gray-500">{percent}% atteint</p>
    </div>
  )
}
