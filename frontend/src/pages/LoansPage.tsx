import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { Loan } from '../api/types'
import { AppHeader } from '../components/AppHeader'
import { useApiClient } from '../auth/useApiClient'
import { formatCurrency, formatDateFr, formatPercent } from '../utils/format'

function errorMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : 'Une erreur est survenue.'
}

function repaidPercent(loan: Pick<Loan, 'principalAmount' | 'remainingAmount'>): number {
  if (loan.principalAmount <= 0) {
    return 0
  }
  const repaid = loan.principalAmount - loan.remainingAmount
  return Math.min(100, Math.max(0, Math.round((repaid / loan.principalAmount) * 100)))
}

export function LoansPage() {
  const apiClient = useApiClient()
  const queryClient = useQueryClient()

  const loansQuery = useQuery({
    queryKey: ['loans'],
    queryFn: () => apiClient<Loan[]>('/api/loans'),
  })

  function invalidateLoans() {
    return queryClient.invalidateQueries({ queryKey: ['loans'] })
  }

  return (
    <div className="min-h-svh bg-gray-50 dark:bg-gray-900">
      <AppHeader />

      <main className="mx-auto max-w-3xl space-y-6 p-4">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-white">Crédits</h1>

        <CreateLoanForm onCreated={invalidateLoans} />

        {loansQuery.isPending && <p className="text-gray-600 dark:text-gray-300">Chargement…</p>}

        {loansQuery.isError && (
          <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
            {errorMessage(loansQuery.error)}
          </p>
        )}

        {loansQuery.data && loansQuery.data.length === 0 && (
          <p className="text-gray-600 dark:text-gray-300">Aucun crédit pour l'instant.</p>
        )}

        <ul className="space-y-4">
          {loansQuery.data?.map((loan) => (
            <li key={loan.id}>
              <LoanCard loan={loan} onChanged={invalidateLoans} />
            </li>
          ))}
        </ul>
      </main>
    </div>
  )
}

function LoanFields({
  idPrefix,
  label,
  setLabel,
  principalAmount,
  setPrincipalAmount,
  remainingAmount,
  setRemainingAmount,
  interestRate,
  setInterestRate,
  monthlyPayment,
  setMonthlyPayment,
  endDate,
  setEndDate,
}: {
  idPrefix: string
  label: string
  setLabel: (v: string) => void
  principalAmount: string
  setPrincipalAmount: (v: string) => void
  remainingAmount: string
  setRemainingAmount: (v: string) => void
  interestRate: string
  setInterestRate: (v: string) => void
  monthlyPayment: string
  setMonthlyPayment: (v: string) => void
  endDate: string
  setEndDate: (v: string) => void
}) {
  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
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
          placeholder="Prêt immobilier"
          className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
      </div>

      <div>
        <label htmlFor={`${idPrefix}-principal`} className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Montant emprunté
        </label>
        <input
          id={`${idPrefix}-principal`}
          type="number"
          step="0.01"
          min="0.01"
          required
          value={principalAmount}
          onChange={(e) => setPrincipalAmount(e.target.value)}
          className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
      </div>

      <div>
        <label htmlFor={`${idPrefix}-remaining`} className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Capital restant dû
        </label>
        <input
          id={`${idPrefix}-remaining`}
          type="number"
          step="0.01"
          min="0"
          required
          value={remainingAmount}
          onChange={(e) => setRemainingAmount(e.target.value)}
          className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
      </div>

      <div>
        <label htmlFor={`${idPrefix}-rate`} className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Taux d'intérêt (%)
        </label>
        <input
          id={`${idPrefix}-rate`}
          type="number"
          step="0.01"
          min="0"
          required
          value={interestRate}
          onChange={(e) => setInterestRate(e.target.value)}
          className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
      </div>

      <div>
        <label htmlFor={`${idPrefix}-payment`} className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Mensualité
        </label>
        <input
          id={`${idPrefix}-payment`}
          type="number"
          step="0.01"
          min="0.01"
          required
          value={monthlyPayment}
          onChange={(e) => setMonthlyPayment(e.target.value)}
          className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
      </div>

      <div>
        <label htmlFor={`${idPrefix}-end-date`} className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Échéance finale
        </label>
        <input
          id={`${idPrefix}-end-date`}
          type="date"
          required
          value={endDate}
          onChange={(e) => setEndDate(e.target.value)}
          className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
        />
      </div>
    </div>
  )
}

function CreateLoanForm({ onCreated }: { onCreated: () => void }) {
  const apiClient = useApiClient()
  const [label, setLabel] = useState('')
  const [principalAmount, setPrincipalAmount] = useState('')
  const [remainingAmount, setRemainingAmount] = useState('')
  const [interestRate, setInterestRate] = useState('0')
  const [monthlyPayment, setMonthlyPayment] = useState('')
  const [endDate, setEndDate] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await apiClient('/api/loans', {
        method: 'POST',
        body: {
          label,
          principalAmount: Number(principalAmount),
          remainingAmount: Number(remainingAmount),
          interestRate: Number(interestRate) || 0,
          monthlyPayment: Number(monthlyPayment),
          endDate,
        },
      })
      setLabel('')
      setPrincipalAmount('')
      setRemainingAmount('')
      setInterestRate('0')
      setMonthlyPayment('')
      setEndDate('')
      onCreated()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800">
      <h2 className="font-medium text-gray-900 dark:text-white">Ajouter un crédit</h2>

      {error && (
        <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      <LoanFields
        idPrefix="loan-create"
        label={label}
        setLabel={setLabel}
        principalAmount={principalAmount}
        setPrincipalAmount={setPrincipalAmount}
        remainingAmount={remainingAmount}
        setRemainingAmount={setRemainingAmount}
        interestRate={interestRate}
        setInterestRate={setInterestRate}
        monthlyPayment={monthlyPayment}
        setMonthlyPayment={setMonthlyPayment}
        endDate={endDate}
        setEndDate={setEndDate}
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

function LoanCard({ loan, onChanged }: { loan: Loan; onChanged: () => void }) {
  const apiClient = useApiClient()
  const [isEditing, setIsEditing] = useState(false)
  const [label, setLabel] = useState(loan.label)
  const [principalAmount, setPrincipalAmount] = useState(String(loan.principalAmount))
  const [remainingAmount, setRemainingAmount] = useState(String(loan.remainingAmount))
  const [interestRate, setInterestRate] = useState(String(loan.interestRate))
  const [monthlyPayment, setMonthlyPayment] = useState(String(loan.monthlyPayment))
  const [endDate, setEndDate] = useState(loan.endDate)
  const [error, setError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  async function handleSave(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsSaving(true)
    try {
      await apiClient(`/api/loans/${loan.id}`, {
        method: 'PUT',
        body: {
          label,
          principalAmount: Number(principalAmount),
          remainingAmount: Number(remainingAmount),
          interestRate: Number(interestRate) || 0,
          monthlyPayment: Number(monthlyPayment),
          endDate,
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
    if (!confirm(`Supprimer le crédit "${loan.label}" ?`)) {
      return
    }
    setError(null)
    try {
      await apiClient(`/api/loans/${loan.id}`, { method: 'DELETE' })
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
        <LoanFields
          idPrefix={`loan-${loan.id}`}
          label={label}
          setLabel={setLabel}
          principalAmount={principalAmount}
          setPrincipalAmount={setPrincipalAmount}
          remainingAmount={remainingAmount}
          setRemainingAmount={setRemainingAmount}
          interestRate={interestRate}
          setInterestRate={setInterestRate}
          monthlyPayment={monthlyPayment}
          setMonthlyPayment={setMonthlyPayment}
          endDate={endDate}
          setEndDate={setEndDate}
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

  const percent = repaidPercent(loan)

  return (
    <div className="space-y-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800">
      {error && (
        <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      <div className="flex items-start justify-between">
        <div>
          <p className="font-medium text-gray-900 dark:text-white">{loan.label}</p>
          <p className="text-sm text-gray-500 dark:text-gray-400">
            Restant dû {formatCurrency(loan.remainingAmount)} / {formatCurrency(loan.principalAmount)} · Mensualité{' '}
            {formatCurrency(loan.monthlyPayment)} · {formatPercent(loan.interestRate)} · échéance{' '}
            {formatDateFr(loan.endDate)}
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
        aria-label={`Remboursement du crédit ${loan.label}`}
        className="h-2 w-full overflow-hidden rounded-full bg-gray-100 dark:bg-gray-700"
      >
        <div className="h-full rounded-full bg-sky-600" style={{ width: `${percent}%` }} />
      </div>
      <p className="text-xs text-gray-400 dark:text-gray-500">{percent}% remboursé</p>
    </div>
  )
}
