import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { ApiError } from '../api/client'
import type { BalancePoint, BankAccount, CategoryBreakdownEntry, MonthlyComparisonEntry } from '../api/types'
import { AppHeader } from '../components/AppHeader'
import { BalanceEvolutionChart } from '../components/charts/BalanceEvolutionChart'
import { CategoryBreakdownChart } from '../components/charts/CategoryBreakdownChart'
import { MonthlyComparisonChart } from '../components/charts/MonthlyComparisonChart'
import { useApiClient } from '../auth/useApiClient'
import { formatCurrency } from '../utils/format'

type Preset = 'month' | 'quarter' | 'year'

function pad(n: number): string {
  return n.toString().padStart(2, '0')
}
function isoFromParts(year: number, month: number, day: number): string {
  return `${year}-${pad(month)}-${pad(day)}`
}
function today(): Date {
  return new Date()
}

function presetRange(preset: Preset): { fromDate: string; toDate: string } {
  const now = today()
  const toDate = isoFromParts(now.getFullYear(), now.getMonth() + 1, now.getDate())
  if (preset === 'month') {
    return { fromDate: isoFromParts(now.getFullYear(), now.getMonth() + 1, 1), toDate }
  }
  if (preset === 'quarter') {
    const quarterStart = new Date(now.getFullYear(), now.getMonth() - 2, 1)
    return { fromDate: isoFromParts(quarterStart.getFullYear(), quarterStart.getMonth() + 1, 1), toDate }
  }
  return { fromDate: isoFromParts(now.getFullYear(), 1, 1), toDate }
}

function errorMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : 'Une erreur est survenue.'
}

function StatTile({ label, value, caption }: { label: string; value: string; caption?: string }) {
  return (
    <div className="rounded-lg bg-white p-4 shadow dark:bg-gray-800">
      <p className="text-xs font-medium text-gray-500 dark:text-gray-400">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-gray-900 dark:text-white">{value}</p>
      {caption && <p className="mt-1 text-xs text-gray-400 dark:text-gray-500">{caption}</p>}
    </div>
  )
}

function ChartCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-lg bg-white shadow dark:bg-gray-800">
      <h2 className="px-4 pt-4 font-medium text-gray-900 dark:text-white">{title}</h2>
      {children}
    </section>
  )
}

export function DashboardPage() {
  const apiClient = useApiClient()

  const [preset, setPreset] = useState<Preset>('year')
  const [range, setRange] = useState(() => presetRange('year'))
  const [bankAccountId, setBankAccountId] = useState('')
  const [startingBalanceInput, setStartingBalanceInput] = useState('')

  function applyPreset(next: Preset) {
    setPreset(next)
    setRange(presetRange(next))
  }

  const startingBalance = Number(startingBalanceInput) || 0
  const year = Number(range.toDate.split('-')[0])
  const isConsolidated = bankAccountId === ''

  const accountsQuery = useQuery({
    queryKey: ['bank-accounts'],
    queryFn: () => apiClient<BankAccount[]>('/api/bank-accounts'),
  })

  const commonParams = new URLSearchParams({ fromDate: range.fromDate, toDate: range.toDate })
  if (bankAccountId) commonParams.set('bankAccountId', bankAccountId)

  const balanceParams = new URLSearchParams(commonParams)
  balanceParams.set('startingBalance', String(startingBalance))

  const balanceQuery = useQuery({
    queryKey: ['dashboard', 'balance-evolution', bankAccountId, range, startingBalance],
    queryFn: () => apiClient<BalancePoint[]>(`/api/dashboard/balance-evolution?${balanceParams.toString()}`),
    placeholderData: keepPreviousData,
  })

  const breakdownQuery = useQuery({
    queryKey: ['dashboard', 'category-breakdown', bankAccountId, range],
    queryFn: () => apiClient<CategoryBreakdownEntry[]>(`/api/dashboard/category-breakdown?${commonParams.toString()}`),
    placeholderData: keepPreviousData,
  })

  const monthlyParams = new URLSearchParams({ year: String(year) })
  if (bankAccountId) monthlyParams.set('bankAccountId', bankAccountId)

  const monthlyQuery = useQuery({
    queryKey: ['dashboard', 'monthly-comparison', bankAccountId, year],
    queryFn: () => apiClient<MonthlyComparisonEntry[]>(`/api/dashboard/monthly-comparison?${monthlyParams.toString()}`),
    placeholderData: keepPreviousData,
  })

  const expenseTotal = breakdownQuery.data?.reduce((sum, e) => sum + e.amount, 0) ?? null
  const netTotal = balanceQuery.data?.reduce((sum, p) => sum + p.netChange, 0) ?? null
  const incomeTotal = isConsolidated && expenseTotal !== null && netTotal !== null ? netTotal + expenseTotal : null

  return (
    <div className="min-h-svh bg-gray-50 dark:bg-gray-900">
      <AppHeader />

      <main className="mx-auto max-w-5xl space-y-6 p-4">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-white">Dashboard</h1>

        <section className="flex flex-wrap items-end gap-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800">
          <div>
            <label htmlFor="dashboard-account" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
              Compte
            </label>
            <select
              id="dashboard-account"
              value={bankAccountId}
              onChange={(e) => setBankAccountId(e.target.value)}
              className="mt-1 rounded border border-gray-300 px-2 py-1.5 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            >
              <option value="">Tous les comptes</option>
              {accountsQuery.data?.map((account) => (
                <option key={account.id} value={account.id}>
                  {account.label}
                </option>
              ))}
            </select>
          </div>

          <div className="flex gap-1" role="group" aria-label="Période">
            {(
              [
                ['month', 'Ce mois-ci'],
                ['quarter', '3 derniers mois'],
                ['year', 'Cette année'],
              ] as const
            ).map(([value, label]) => (
              <button
                key={value}
                onClick={() => applyPreset(value)}
                className={
                  preset === value
                    ? 'rounded bg-sky-600 px-3 py-1.5 text-sm font-medium text-white'
                    : 'rounded border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-100 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700'
                }
              >
                {label}
              </button>
            ))}
          </div>

          <div>
            <label htmlFor="dashboard-from" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
              Du
            </label>
            <input
              id="dashboard-from"
              type="date"
              value={range.fromDate}
              onChange={(e) => setRange((r) => ({ ...r, fromDate: e.target.value }))}
              className="mt-1 rounded border border-gray-300 px-2 py-1.5 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            />
          </div>
          <div>
            <label htmlFor="dashboard-to" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
              Au
            </label>
            <input
              id="dashboard-to"
              type="date"
              value={range.toDate}
              onChange={(e) => setRange((r) => ({ ...r, toDate: e.target.value }))}
              className="mt-1 rounded border border-gray-300 px-2 py-1.5 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            />
          </div>

          <div>
            <label htmlFor="dashboard-starting-balance" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
              Solde de départ (optionnel)
            </label>
            <input
              id="dashboard-starting-balance"
              type="number"
              step="0.01"
              value={startingBalanceInput}
              onChange={(e) => setStartingBalanceInput(e.target.value)}
              placeholder="0"
              className="mt-1 w-32 rounded border border-gray-300 px-2 py-1.5 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            />
          </div>
        </section>

        <div className="grid gap-4 sm:grid-cols-3">
          {incomeTotal !== null && <StatTile label="Revenus" value={formatCurrency(incomeTotal)} />}
          {expenseTotal !== null && <StatTile label="Dépenses" value={formatCurrency(expenseTotal)} />}
          {netTotal !== null && (
            <StatTile
              label="Variation du solde"
              value={formatCurrency(netTotal)}
              caption={!isConsolidated ? 'Inclut les virements internes vus depuis ce compte' : undefined}
            />
          )}
        </div>

        <ChartCard title="Évolution du solde cumulé">
          {balanceQuery.isPending && <p className="p-4 text-gray-600 dark:text-gray-300">Chargement…</p>}
          {balanceQuery.isError && (
            <p role="alert" className="m-4 rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
              {errorMessage(balanceQuery.error)}
            </p>
          )}
          {balanceQuery.data && <BalanceEvolutionChart points={balanceQuery.data} />}
        </ChartCard>

        <ChartCard title="Dépenses par catégorie">
          {breakdownQuery.isPending && <p className="p-4 text-gray-600 dark:text-gray-300">Chargement…</p>}
          {breakdownQuery.isError && (
            <p role="alert" className="m-4 rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
              {errorMessage(breakdownQuery.error)}
            </p>
          )}
          {breakdownQuery.data && (
            <div className="p-4">
              <CategoryBreakdownChart entries={breakdownQuery.data} />
            </div>
          )}
        </ChartCard>

        <ChartCard title={`Comparatif mensuel ${year}`}>
          {monthlyQuery.isPending && <p className="p-4 text-gray-600 dark:text-gray-300">Chargement…</p>}
          {monthlyQuery.isError && (
            <p role="alert" className="m-4 rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
              {errorMessage(monthlyQuery.error)}
            </p>
          )}
          {monthlyQuery.data && <MonthlyComparisonChart entries={monthlyQuery.data} />}
        </ChartCard>
      </main>
    </div>
  )
}
