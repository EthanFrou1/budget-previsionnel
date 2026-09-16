import { useQuery, type UseQueryResult } from '@tanstack/react-query'
import { useState } from 'react'
import { ApiError } from '../api/client'
import type {
  Category,
  CalendarEntry,
  CalendarEntryType,
  ForecastSource,
  MonthlyForecast,
} from '../api/types'
import { AppLayout } from '../components/AppLayout'
import { useApiClient } from '../auth/useApiClient'
import { formatCurrency, formatDateFr, formatMonthFr } from '../utils/format'
import { BudgetSection } from './BudgetSection'
import { RecurringExpensesSection } from './RecurringExpensesSection'
import { RecurringIncomesSection } from './RecurringIncomesSection'

type Mode = 'monthly' | 'annual' | 'calendar'
type CalendarGranularity = 'month' | 'year'

function pad(n: number): string {
  return n.toString().padStart(2, '0')
}
function currentMonthValue(): string {
  const now = new Date()
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}`
}

// Shifts a "yyyy-MM" value by whole months, going through Date's own month-overflow
// rollover (month 13 becomes January of the next year) rather than hand-rolling it.
function shiftMonthValue(monthValue: string, delta: number): string {
  const [year, month] = monthValue.split('-').map(Number)
  const shifted = new Date(year, month - 1 + delta, 1)
  return `${shifted.getFullYear()}-${pad(shifted.getMonth() + 1)}`
}

const WEEKDAY_LABELS = ['Lun', 'Mar', 'Mer', 'Jeu', 'Ven', 'Sam', 'Dim']

interface MonthCell {
  date: string | null
  day: number
}

// Monday-start grid (French convention) padded with blank leading/trailing cells so the
// month always renders as complete weeks. Cells outside the month carry no data - the API
// only returns occurrences for the requested month, so an adjacent month's entries never
// appear on its overflow days here.
function buildMonthCells(monthValue: string): MonthCell[] {
  const [yearStr, monthStr] = monthValue.split('-')
  const year = Number(yearStr)
  const monthIndex = Number(monthStr) - 1
  const daysInMonth = new Date(year, monthIndex + 1, 0).getDate()
  const firstWeekday = (new Date(year, monthIndex, 1).getDay() + 6) % 7 // Monday = 0

  const cells: MonthCell[] = []
  for (let i = 0; i < firstWeekday; i++) {
    cells.push({ date: null, day: 0 })
  }
  for (let day = 1; day <= daysInMonth; day++) {
    cells.push({ date: `${yearStr}-${monthStr}-${pad(day)}`, day })
  }
  while (cells.length % 7 !== 0) {
    cells.push({ date: null, day: 0 })
  }
  return cells
}

const CALENDAR_TYPE_LABELS: Record<CalendarEntryType, string> = {
  RecurringExpense: 'Abonnement',
  Loan: 'Crédit',
  RecurringIncome: 'Revenu',
}

function calendarEntryClasses(type: CalendarEntryType): string {
  if (type === 'Loan') return 'bg-accent/15 text-accent'
  if (type === 'RecurringIncome') return 'bg-positive/15 text-positive'
  return 'bg-info/15 text-info'
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
          ? 'rounded bg-accent/15 px-1.5 py-0.5 text-xs text-accent'
          : 'rounded bg-info/15 px-1.5 py-0.5 text-xs text-info'
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
  const [calendarView, setCalendarView] = useState<CalendarGranularity>('month')
  const [monthInput, setMonthInput] = useState(currentMonthValue())
  const [year, setYear] = useState(new Date().getFullYear())

  const month = `${monthInput}-01`
  const usesMonthControl = mode === 'monthly' || (mode === 'calendar' && calendarView === 'month')

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

  const calendarMonthlyQuery = useQuery({
    queryKey: ['calendar', 'monthly', month],
    queryFn: () => apiClient<CalendarEntry[]>(`/api/calendar/monthly?month=${month}`),
    enabled: mode === 'calendar' && calendarView === 'month',
  })

  const calendarAnnualQuery = useQuery({
    queryKey: ['calendar', 'annual', year],
    queryFn: () => apiClient<CalendarEntry[]>(`/api/calendar/annual?year=${year}`),
    enabled: mode === 'calendar' && calendarView === 'year',
  })

  const categories = categoriesQuery.data ?? []

  function showMonthDetail(monthIso: string) {
    setMonthInput(monthIso.slice(0, 7))
    setMode('monthly')
  }

  return (
    <AppLayout>
      <h1 className="font-display text-xl font-semibold text-heading">Prévisionnel</h1>

      <section className="flex flex-wrap items-end gap-3 rounded-lg bg-surface p-4 shadow">
        <div className="flex gap-1" role="group" aria-label="Vue">
          {(
            [
              ['monthly', 'Mensuel'],
              ['annual', 'Annuel'],
              ['calendar', 'Calendrier'],
            ] as const
          ).map(([value, label]) => (
            <button
              key={value}
              onClick={() => setMode(value)}
              className={
                mode === value
                  ? 'rounded bg-accent px-3 py-1.5 text-sm font-medium text-white'
                  : 'rounded border border-border px-3 py-1.5 text-sm text-body hover:bg-overlay'
              }
            >
              {label}
            </button>
          ))}
        </div>

        {mode === 'calendar' && (
          <div className="flex gap-1" role="group" aria-label="Granularité du calendrier">
            {(
              [
                ['month', 'Mois'],
                ['year', 'Année'],
              ] as const
            ).map(([value, label]) => (
              <button
                key={value}
                onClick={() => setCalendarView(value)}
                className={
                  calendarView === value
                    ? 'rounded bg-accent px-3 py-1.5 text-sm font-medium text-white'
                    : 'rounded border border-border px-3 py-1.5 text-sm text-body hover:bg-overlay'
                }
              >
                {label}
              </button>
            ))}
          </div>
        )}

        {usesMonthControl ? (
          <div>
            <label htmlFor="forecast-month" className="block text-xs font-medium text-body">
              Mois
            </label>
            <input
              id="forecast-month"
              type="month"
              value={monthInput}
              onChange={(e) => setMonthInput(e.target.value)}
              className="mt-1 rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
            />
          </div>
        ) : (
          <div>
            <label htmlFor="forecast-year" className="block text-xs font-medium text-body">
              Année
            </label>
            <input
              id="forecast-year"
              type="number"
              value={year}
              onChange={(e) => setYear(Number(e.target.value) || year)}
              className="mt-1 w-24 rounded border border-border px-2 py-1.5 text-sm bg-field text-heading"
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

      {mode === 'calendar' && calendarView === 'month' && (
        <CalendarMonthView
          query={calendarMonthlyQuery}
          monthInput={monthInput}
          onNavigate={(delta) => setMonthInput(shiftMonthValue(monthInput, delta))}
        />
      )}

      {mode === 'calendar' && calendarView === 'year' && (
        <CalendarYearView query={calendarAnnualQuery} year={year} />
      )}

      <RecurringExpensesSection categories={categories} />
      <RecurringIncomesSection categories={categories} />
    </AppLayout>
  )
}

function MonthlyForecastView({ query }: { query: UseQueryResult<MonthlyForecast, unknown> }) {
  return (
    <section className="rounded-lg bg-surface shadow">
      <h2 className="font-display px-4 pt-4 font-medium text-heading">
        {query.data ? `Prévisionnel de ${formatMonthFr(query.data.month)}` : 'Prévisionnel'}
      </h2>

      {query.isPending && <p className="p-4 text-body">Chargement…</p>}

      {query.isError && (
        <p role="alert" className="m-4 rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {errorMessage(query.error)}
        </p>
      )}

      {query.data && (
        <div className="overflow-x-auto p-4">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-border text-xs uppercase text-muted">
              <tr>
                <th className="py-2">Catégorie</th>
                <th className="py-2">Source</th>
                <th className="py-2 text-right">Montant</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {query.data.categoryLines.map((line, index) => (
                <tr key={`${line.categoryId ?? 'none'}-${index}`}>
                  <td className="py-2 text-heading">{line.categoryLabel ?? 'Sans catégorie'}</td>
                  <td className="py-2">
                    <SourceBadge source={line.source} />
                  </td>
                  <td className="py-2 text-right text-body">{formatCurrency(line.amount)}</td>
                </tr>
              ))}
              {query.data.loanPayments > 0 && (
                <tr>
                  <td className="py-2 text-heading">Remboursements de crédits</td>
                  <td className="py-2" />
                  <td className="py-2 text-right text-body">{formatCurrency(query.data.loanPayments)}</td>
                </tr>
              )}
              {query.data.categoryLines.length === 0 && query.data.loanPayments === 0 && (
                <tr>
                  <td colSpan={3} className="py-6 text-center text-muted">
                    Aucune dépense prévue ce mois-ci.
                  </td>
                </tr>
              )}
            </tbody>
            <tfoot>
              <tr className="border-t border-border font-semibold text-heading">
                <td className="py-2" colSpan={2}>
                  Total des dépenses prévues
                </td>
                <td className="py-2 text-right">{formatCurrency(query.data.total)}</td>
              </tr>
              {query.data.totalRecurringIncome > 0 && (
                <tr className="text-heading">
                  <td className="py-2" colSpan={2}>
                    Revenus récurrents prévus
                  </td>
                  <td className="py-2 text-right text-positive">
                    {formatCurrency(query.data.totalRecurringIncome)}
                  </td>
                </tr>
              )}
              {query.data.totalRecurringIncome > 0 && (
                <tr className="border-t border-border font-semibold text-heading">
                  <td className="py-2" colSpan={2}>
                    Solde net prévisionnel
                  </td>
                  <td
                    className={`py-2 text-right ${query.data.netBalance < 0 ? 'text-negative' : 'text-positive'}`}
                  >
                    {formatCurrency(query.data.netBalance)}
                  </td>
                </tr>
              )}
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
    <section className="rounded-lg bg-surface shadow">
      <h2 className="font-display px-4 pt-4 font-medium text-heading">Prévisionnel annuel</h2>

      {query.isPending && <p className="p-4 text-body">Chargement…</p>}

      {query.isError && (
        <p role="alert" className="m-4 rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {errorMessage(query.error)}
        </p>
      )}

      {query.data && (
        <div className="overflow-x-auto p-4">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-border text-xs uppercase text-muted">
              <tr>
                <th className="py-2">Mois</th>
                <th className="py-2 text-right">Total prévu</th>
                <th className="py-2" />
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {query.data.map((forecast) => (
                <tr key={forecast.month}>
                  <td className="py-2 text-heading">{formatMonthFr(forecast.month)}</td>
                  <td className="py-2 text-right text-body">{formatCurrency(forecast.total)}</td>
                  <td className="py-2 text-right">
                    <button
                      onClick={() => onSelectMonth(forecast.month)}
                      className="rounded border border-border px-2 py-1 text-xs hover:bg-overlay"
                    >
                      Détail
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t border-border font-semibold text-heading">
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

function CalendarMonthView({
  query,
  monthInput,
  onNavigate,
}: {
  query: UseQueryResult<CalendarEntry[], unknown>
  monthInput: string
  onNavigate: (delta: number) => void
}) {
  const entriesByDate = new Map<string, CalendarEntry[]>()
  for (const entry of query.data ?? []) {
    const list = entriesByDate.get(entry.date) ?? []
    list.push(entry)
    entriesByDate.set(entry.date, list)
  }

  const cells = buildMonthCells(monthInput)

  return (
    <section className="rounded-lg bg-surface shadow">
      <div className="flex items-center justify-between px-4 pt-4">
        <h2 className="font-display font-medium text-heading">
          Calendrier de {formatMonthFr(`${monthInput}-01`)}
        </h2>
        <div className="flex gap-1">
          <button
            onClick={() => onNavigate(-1)}
            aria-label="Mois précédent"
            className="rounded border border-border px-2 py-1 text-sm text-body hover:bg-overlay"
          >
            ‹
          </button>
          <button
            onClick={() => onNavigate(1)}
            aria-label="Mois suivant"
            className="rounded border border-border px-2 py-1 text-sm text-body hover:bg-overlay"
          >
            ›
          </button>
        </div>
      </div>

      {query.isPending && <p className="p-4 text-body">Chargement…</p>}

      {query.isError && (
        <p role="alert" className="m-4 rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {errorMessage(query.error)}
        </p>
      )}

      {query.data && (
        <div className="p-4">
          <div className="grid grid-cols-7 gap-px overflow-hidden rounded border border-border bg-border text-xs">
            {WEEKDAY_LABELS.map((label) => (
              <div key={label} className="bg-surface px-2 py-1 text-center font-medium uppercase text-muted">
                {label}
              </div>
            ))}
            {cells.map((cell, index) => (
              <div key={index} className="min-h-24 bg-field/40 p-1">
                {cell.date && (
                  <>
                    <div className="text-right text-muted">{cell.day}</div>
                    <div className="mt-1 flex flex-col gap-1">
                      {(entriesByDate.get(cell.date) ?? []).map((entry, i) => (
                        <div
                          key={i}
                          title={`${CALENDAR_TYPE_LABELS[entry.type]} — ${entry.label} — ${formatCurrency(entry.amount)}`}
                          className={`truncate rounded px-1 py-0.5 ${calendarEntryClasses(entry.type)}`}
                        >
                          {entry.label}
                          {entry.isLastOccurrence && <span className="ml-1 text-negative">(dernière)</span>}
                        </div>
                      ))}
                    </div>
                  </>
                )}
              </div>
            ))}
          </div>

          {query.data.length === 0 && (
            <p className="mt-3 text-center text-muted">Aucune échéance ce mois-ci.</p>
          )}
        </div>
      )}
    </section>
  )
}

function CalendarYearView({
  query,
  year,
}: {
  query: UseQueryResult<CalendarEntry[], unknown>
  year: number
}) {
  const entriesByMonth = new Map<string, CalendarEntry[]>()
  for (const entry of query.data ?? []) {
    const monthKey = entry.date.slice(0, 7)
    const list = entriesByMonth.get(monthKey) ?? []
    list.push(entry)
    entriesByMonth.set(monthKey, list)
  }

  const months = Array.from({ length: 12 }, (_, i) => `${year}-${pad(i + 1)}`)

  return (
    <section className="rounded-lg bg-surface shadow">
      <h2 className="font-display px-4 pt-4 font-medium text-heading">Calendrier annuel {year}</h2>

      {query.isPending && <p className="p-4 text-body">Chargement…</p>}

      {query.isError && (
        <p role="alert" className="m-4 rounded bg-negative/10 px-3 py-2 text-sm text-negative">
          {errorMessage(query.error)}
        </p>
      )}

      {query.data && (
        <div className="grid gap-4 p-4 sm:grid-cols-2 lg:grid-cols-3">
          {months.map((monthKey) => {
            const entries = entriesByMonth.get(monthKey) ?? []
            return (
              <div key={monthKey} className="rounded border border-border p-3">
                <h3 className="font-display mb-2 text-sm font-medium text-heading">
                  {formatMonthFr(`${monthKey}-01`)}
                </h3>
                {entries.length === 0 ? (
                  <p className="text-xs text-muted">Aucune échéance.</p>
                ) : (
                  <ul className="flex flex-col gap-1 text-xs">
                    {entries.map((entry, i) => (
                      <li key={i} className="flex items-center justify-between gap-2">
                        <span className="truncate text-body">
                          {formatDateFr(entry.date)} — {entry.label}
                          {entry.isLastOccurrence && <span className="ml-1 text-negative">(dernière)</span>}
                        </span>
                        <span className="shrink-0 text-muted">{formatCurrency(entry.amount)}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            )
          })}
        </div>
      )}
    </section>
  )
}
