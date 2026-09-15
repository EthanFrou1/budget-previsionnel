import { useQuery, type UseQueryResult } from '@tanstack/react-query'
import { useState } from 'react'
import { ApiError } from '../api/client'
import type { Category, ForecastSource, MonthlyForecast } from '../api/types'
import { AppHeader } from '../components/AppHeader'
import { useApiClient } from '../auth/useApiClient'
import { formatCurrency, formatMonthFr } from '../utils/format'
import { BudgetSection } from './BudgetSection'
import { RecurringExpensesSection } from './RecurringExpensesSection'

type Mode = 'monthly' | 'annual'

function pad(n: number): string {
  return n.toString().padStart(2, '0')
}
function currentMonthValue(): string {
  const now = new Date()
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}`
}

function errorMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : 'Une erreur est survenue.'
}

const SOURCE_LABELS: Record<ForecastSource, string> = {
  Budget: 'Budget',
  RecurringExpense: 'Récurrent',
}

function SourceBadge({ source }: { source: ForecastSource }) {
  const isBudget = source === 'Budget'
  return (
    <span
      className={
        isBudget
          ? 'rounded bg-sky-50 px-1.5 py-0.5 text-xs text-sky-700 dark:bg-sky-950 dark:text-sky-300'
          : 'rounded bg-amber-50 px-1.5 py-0.5 text-xs text-amber-700 dark:bg-amber-950 dark:text-amber-300'
      }
    >
      {SOURCE_LABELS[source]}
    </span>
  )
}

/**
 * Combines Budget + RecurringExpense + Loan into one projected total for the month
 * (ForecastService's precedence rule: an explicit Budget line for a category wins over
 * a RecurringExpense guess for the same category, never both). Loan payments have no
 * CategoryId in the domain model, so they show as a separate line, not folded into the
 * category table.
 */
export function ForecastPage() {
  const apiClient = useApiClient()

  const [mode, setMode] = useState<Mode>('monthly')
  const [monthInput, setMonthInput] = useState(currentMonthValue())
  const [year, setYear] = useState(new Date().getFullYear())

  const month = `${monthInput}-01`

  const categoriesQuery = useQuery({
    queryKey: ['categories'],
    queryFn: () => apiClient<Category[]>('/api/categories'),
  })

  const monthlyQuery = useQuery({
    queryKey: ['forecast', 'monthly', month],
    queryFn: () => apiClient<MonthlyForecast>(`/api/forecast/monthly?month=${month}`),
    enabled: mode === 'monthly',
  })

  const annualQuery = useQuery({
    queryKey: ['forecast', 'annual', year],
    queryFn: () => apiClient<MonthlyForecast[]>(`/api/forecast/annual?year=${year}`),
    enabled: mode === 'annual',
  })

  const categories = categoriesQuery.data ?? []

  function showMonthDetail(monthIso: string) {
    setMonthInput(monthIso.slice(0, 7))
    setMode('monthly')
  }

  return (
    <div className="min-h-svh bg-gray-50 dark:bg-gray-900">
      <AppHeader />

      <main className="mx-auto max-w-4xl space-y-6 p-4">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-white">Prévisionnel</h1>

        <section className="flex flex-wrap items-end gap-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800">
          <div className="flex gap-1" role="group" aria-label="Vue">
            {(
              [
                ['monthly', 'Mensuel'],
                ['annual', 'Annuel'],
              ] as const
            ).map(([value, label]) => (
              <button
                key={value}
                onClick={() => setMode(value)}
                className={
                  mode === value
                    ? 'rounded bg-sky-600 px-3 py-1.5 text-sm font-medium text-white'
                    : 'rounded border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-100 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700'
                }
              >
                {label}
              </button>
            ))}
          </div>

          {mode === 'monthly' ? (
            <div>
              <label htmlFor="forecast-month" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
                Mois
              </label>
              <input
                id="forecast-month"
                type="month"
                value={monthInput}
                onChange={(e) => setMonthInput(e.target.value)}
                className="mt-1 rounded border border-gray-300 px-2 py-1.5 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              />
            </div>
          ) : (
            <div>
              <label htmlFor="forecast-year" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
                Année
              </label>
              <input
                id="forecast-year"
                type="number"
                value={year}
                onChange={(e) => setYear(Number(e.target.value) || year)}
                className="mt-1 w-24 rounded border border-gray-300 px-2 py-1.5 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              />
            </div>
          )}
        </section>

        {mode === 'monthly' && (
          <>
            <MonthlyForecastView query={monthlyQuery} />
            <BudgetSection month={month} categories={categories} />
          </>
        )}

        {mode === 'annual' && <AnnualForecastView query={annualQuery} onSelectMonth={showMonthDetail} />}

        <RecurringExpensesSection categories={categories} />
      </main>
    </div>
  )
}

function MonthlyForecastView({ query }: { query: UseQueryResult<MonthlyForecast, unknown> }) {
  return (
    <section className="rounded-lg bg-white shadow dark:bg-gray-800">
      <h2 className="px-4 pt-4 font-medium text-gray-900 dark:text-white">
        {query.data ? `Prévisionnel de ${formatMonthFr(query.data.month)}` : 'Prévisionnel'}
      </h2>

      {query.isPending && <p className="p-4 text-gray-600 dark:text-gray-300">Chargement…</p>}

      {query.isError && (
        <p role="alert" className="m-4 rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {errorMessage(query.error)}
        </p>
      )}

      {query.data && (
        <div className="overflow-x-auto p-4">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-gray-200 text-xs uppercase text-gray-500 dark:border-gray-700 dark:text-gray-400">
              <tr>
                <th className="py-2">Catégorie</th>
                <th className="py-2">Source</th>
                <th className="py-2 text-right">Montant</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
              {query.data.categoryLines.map((line, index) => (
                <tr key={`${line.categoryId ?? 'none'}-${index}`}>
                  <td className="py-2 text-gray-900 dark:text-white">{line.categoryLabel ?? 'Sans catégorie'}</td>
                  <td className="py-2">
                    <SourceBadge source={line.source} />
                  </td>
                  <td className="py-2 text-right text-gray-600 dark:text-gray-300">{formatCurrency(line.amount)}</td>
                </tr>
              ))}
              {query.data.loanPayments > 0 && (
                <tr>
                  <td className="py-2 text-gray-900 dark:text-white">Remboursements de crédits</td>
                  <td className="py-2" />
                  <td className="py-2 text-right text-gray-600 dark:text-gray-300">
                    {formatCurrency(query.data.loanPayments)}
                  </td>
                </tr>
              )}
              {query.data.categoryLines.length === 0 && query.data.loanPayments === 0 && (
                <tr>
                  <td colSpan={3} className="py-6 text-center text-gray-500 dark:text-gray-400">
                    Aucune dépense prévue ce mois-ci.
                  </td>
                </tr>
              )}
            </tbody>
            <tfoot>
              <tr className="border-t border-gray-200 font-semibold text-gray-900 dark:border-gray-700 dark:text-white">
                <td className="py-2" colSpan={2}>
                  Total
                </td>
                <td className="py-2 text-right">{formatCurrency(query.data.total)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      )}
    </section>
  )
}

function AnnualForecastView({
  query,
  onSelectMonth,
}: {
  query: UseQueryResult<MonthlyForecast[], unknown>
  onSelectMonth: (month: string) => void
}) {
  return (
    <section className="rounded-lg bg-white shadow dark:bg-gray-800">
      <h2 className="px-4 pt-4 font-medium text-gray-900 dark:text-white">Prévisionnel annuel</h2>

      {query.isPending && <p className="p-4 text-gray-600 dark:text-gray-300">Chargement…</p>}

      {query.isError && (
        <p role="alert" className="m-4 rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {errorMessage(query.error)}
        </p>
      )}

      {query.data && (
        <div className="overflow-x-auto p-4">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-gray-200 text-xs uppercase text-gray-500 dark:border-gray-700 dark:text-gray-400">
              <tr>
                <th className="py-2">Mois</th>
                <th className="py-2 text-right">Total prévu</th>
                <th className="py-2" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
              {query.data.map((forecast) => (
                <tr key={forecast.month}>
                  <td className="py-2 text-gray-900 dark:text-white">{formatMonthFr(forecast.month)}</td>
                  <td className="py-2 text-right text-gray-600 dark:text-gray-300">{formatCurrency(forecast.total)}</td>
                  <td className="py-2 text-right">
                    <button
                      onClick={() => onSelectMonth(forecast.month)}
                      className="rounded border border-gray-300 px-2 py-1 text-xs hover:bg-gray-100 dark:border-gray-600 dark:hover:bg-gray-700"
                    >
                      Détail
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t border-gray-200 font-semibold text-gray-900 dark:border-gray-700 dark:text-white">
                <td className="py-2">Total annuel</td>
                <td className="py-2 text-right">
                  {formatCurrency(query.data.reduce((sum, forecast) => sum + forecast.total, 0))}
                </td>
                <td className="py-2" />
              </tr>
            </tfoot>
          </table>
        </div>
      )}
    </section>
  )
}
