import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { ApiError } from '../api/client'
import type { BalancePoint, BankAccount, CategoryBreakdownEntry, MonthlyComparisonEntry } from '../api/types'
import { AppLayout } from '../components/AppLayout'
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
    <div className="rounded-lg bg-surface p-4 shadow">
      <p className="text-xs font-medium text-muted">{label}</p>
      <p className="font-display mt-1 text-2xl font-semibold text-heading">{value}</p>
      {caption && <p className="mt-1 text-xs text-muted">{caption}</p>}
    </div>
  )
}

function ChartCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-lg bg-surface shadow">
      <h2 className="font-display px-4 pt-4 font-medium text-heading">{title}</h2>
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
    queryFn: () =>
      apiClient<CategoryBreakdownEntry[]>(`/api/dashboard/category-breakdown?${commonParams.toString()}`),
    placeholderData: keepPreviousData,
  })

  const monthlyParams = new URLSearchParams({ year: String(year) })
  if (bankAccountId) monthlyParams.set('bankAccountId', bankAccountId)

  const monthlyQuery = useQuery({
    queryKey: ['dashboard', 'monthly-comparison', bankAccountId, year],
    queryFn: () =>
      apiClient<MonthlyComparisonEntry[]>(`/api/dashboard/monthly-comparison?${monthlyParams.toString()}`),
    placeholderData: keepPreviousData,
  })

  const expenseTotal = breakdownQuery.data?.reduce((sum, e) => sum + e.amount, 0) ?? null
  const netTotal = balanceQuery.data?.reduce((sum, p) => sum + p.netChange, 0) ?? null
  const incomeTotal =
    isConsolidated && expenseTotal !== null && netTotal !== null ? netTotal + expenseTotal : null

  return (
    <AppLayout>
      <h1 className="font-display text-xl font-semibold text-heading">Dashboard</h1>

      <section className="flex flex-wrap items-end gap-3 rounded-lg bg-surface p-4 shadow">
        <div>
          <label htmlFor="dashboard-account" className="block text-xs font-medium text-body">
            Compte
          </label>
          <select
            id="dashboard-account"
            value={bankAccountId}
            onChange={(e) => setBankAccountId(e.target.value)}
            className="mt-1 rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
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
                  ? 'rounded bg-accent px-3 py-1.5 text-sm font-medium text-white'
                  : 'rounded border border-border px-3 py-1.5 text-sm text-body hover:bg-overlay'
              }
            >
              {label}
            </button>
          ))}
        </div>

        <div>
          <label htmlFor="dashboard-from" className="block text-xs font-medium text-body">
            Du
          </label>
          <input
            id="dashboard-from"
            type="date"
            value={range.fromDate}
            onChange={(e) => setRange((r) => ({ ...r, fromDate: e.target.value }))}
            className="mt-1 rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
          />
        </div>
        <div>
          <label htmlFor="dashboard-to" className="block text-xs font-medium text-body">
            Au
          </label>
          <input
            id="dashboard-to"
            type="date"
            value={range.toDate}
            onChange={(e) => setRange((r) => ({ ...r, toDate: e.target.value }))}
            className="mt-1 rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
          />
        </div>

        <div>
          <label htmlFor="dashboard-starting-balance" className="block text-xs font-medium text-body">
            Solde de départ (optionnel)
          </label>
          <input
            id="dashboard-starting-balance"
            type="number"
            step="0.01"
            value={startingBalanceInput}
            onChange={(e) => setStartingBalanceInput(e.target.value)}
            placeholder="0"
            className="mt-1 w-32 rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
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
        {balanceQuery.isPending && <p className="p-4 text-body">Chargement…</p>}
        {balanceQuery.isError && (
          <p role="alert" className="m-4 rounded bg-negative/10 px-3 py-2 text-sm text-negative">
            {errorMessage(balanceQuery.error)}
          </p>
        )}
        {balanceQuery.data && <BalanceEvolutionChart points={balanceQuery.data} />}
      </ChartCard>

      <ChartCard title="Dépenses par catégorie">
        {breakdownQuery.isPending && <p className="p-4 text-body">Chargement…</p>}
        {breakdownQuery.isError && (
          <p role="alert" className="m-4 rounded bg-negative/10 px-3 py-2 text-sm text-negative">
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
        {monthlyQuery.isPending && <p className="p-4 text-body">Chargement…</p>}
        {monthlyQuery.isError && (
          <p role="alert" className="m-4 rounded bg-negative/10 px-3 py-2 text-sm text-negative">
            {errorMessage(monthlyQuery.error)}
          </p>
        )}
        {monthlyQuery.data && <MonthlyComparisonChart entries={monthlyQuery.data} />}
      </ChartCard>
    </AppLayout>
  )
}
