import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { BankAccount, SavingsGoal } from '../api/types'
import { AppLayout } from '../components/AppLayout'
import { useConfirm } from '../components/confirmContext'
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
  const [isCreating, setIsCreating] = useState(false)

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
    <AppLayout>
      <div className="flex items-center justify-between">
        <h1 className="font-display text-xl font-semibold text-heading">Épargne</h1>
        {!isCreating && (
          <button
            onClick={() => setIsCreating(true)}
            className="rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover"
          >
            + Ajouter un objectif
          </button>
        )}
      </div>

      {isCreating && (
        <CreateGoalForm
          accounts={accounts}
          onCreated={() => {
            invalidateGoals()
            setIsCreating(false)
          }}
          onCancel={() => setIsCreating(false)}
        />
      )}

      {goalsQuery.isPending && <p className="text-body">Chargement…</p>}

      {goalsQuery.isError && (
        <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {errorMessage(goalsQuery.error)}
        </p>
      )}

      {goalsQuery.data && goalsQuery.data.length === 0 && (
        <p className="text-body">Aucun objectif d'épargne pour l'instant.</p>
      )}

      <ul className="space-y-4">
        {goalsQuery.data?.map((goal) => (
          <li key={goal.id}>
            <GoalCard goal={goal} accounts={accounts} onChanged={invalidateGoals} />
          </li>
        ))}
      </ul>
    </AppLayout>
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
        <label htmlFor={`${idPrefix}-label`} className="block text-sm font-medium text-body">
          Libellé
        </label>
        <input
          id={`${idPrefix}-label`}
          type="text"
          required
          value={label}
          onChange={(e) => setLabel(e.target.value)}
          placeholder="Fonds d'urgence"
          className="mt-1 w-full rounded border border-border px-3 py-2 focus:border-accent focus:outline-none bg-field text-heading"
        />
      </div>

      <div>
        <label htmlFor={`${idPrefix}-target`} className="block text-sm font-medium text-body">
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
          className="mt-1 w-full rounded border border-border px-3 py-2 focus:border-accent focus:outline-none bg-field text-heading"
        />
      </div>

      <div>
        <label htmlFor={`${idPrefix}-current`} className="block text-sm font-medium text-body">
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
          className="mt-1 w-full rounded border border-border px-3 py-2 focus:border-accent focus:outline-none bg-field text-heading"
        />
      </div>

      <div>
        <label htmlFor={`${idPrefix}-date`} className="block text-sm font-medium text-body">
          Échéance (optionnelle)
        </label>
        <input
          id={`${idPrefix}-date`}
          type="date"
          value={targetDate}
          onChange={(e) => setTargetDate(e.target.value)}
          className="mt-1 w-full rounded border border-border px-3 py-2 focus:border-accent focus:outline-none bg-field text-heading"
        />
      </div>

      <div className="sm:col-span-2 lg:col-span-4">
        <label htmlFor={`${idPrefix}-account`} className="block text-sm font-medium text-body">
          Compte lié (optionnel)
        </label>
        <select
          id={`${idPrefix}-account`}
          value={linkedAccountId}
          onChange={(e) => setLinkedAccountId(e.target.value)}
          className="mt-1 w-full rounded border border-border px-3 py-2 focus:border-accent focus:outline-none bg-field text-heading"
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

function CreateGoalForm({
  accounts,
  onCreated,
  onCancel,
}: {
  accounts: BankAccount[]
  onCreated: () => void
  onCancel: () => void
}) {
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
    <form onSubmit={handleSubmit} className="space-y-3 rounded-lg bg-surface p-4 shadow">
      <h2 className="font-display font-medium text-heading">Ajouter un objectif</h2>

      {error && (
        <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
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
  const confirm = useConfirm()
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
    if (
      !(await confirm(`Supprimer l'objectif "${goal.label}" ?`, { confirmLabel: 'Supprimer', danger: true }))
    ) {
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
        className="space-y-3 rounded-lg border border-accent/40 bg-surface p-4 shadow"
      >
        {error && (
          <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
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

  const percent = progressPercent(goal)

  return (
    <div className="space-y-3 rounded-lg bg-surface p-4 shadow">
      {error && (
        <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {error}
        </p>
      )}

      <div className="flex items-start justify-between">
        <div>
          <p className="font-medium text-heading">{goal.label}</p>
          <p className="text-sm text-muted">
            {formatCurrency(goal.currentAmount)} / {formatCurrency(goal.targetAmount)}
            {goal.targetDate ? ` · échéance ${formatDateFr(goal.targetDate)}` : ''}
            {linkedAccount ? ` · ${linkedAccount.label}` : ''}
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

      <div
        role="progressbar"
        aria-valuenow={percent}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`Progression de l'objectif ${goal.label}`}
        className="h-2 w-full overflow-hidden rounded-full bg-border"
      >
        <div className="h-full rounded-full bg-accent" style={{ width: `${percent}%` }} />
      </div>
      <p className="text-xs text-muted">{percent}% atteint</p>
    </div>
  )
}
