import type { CategoryBreakdownEntry } from '../../api/types'
import { formatCurrency } from '../../utils/format'

const MAX_ROWS = 7

// Single sequential hue (the app's accent): this is a magnitude ranking ("who spent the
// most"), not an identity comparison - the category name label already carries identity,
// so one hue for every bar is the correct job per the dataviz skill's choosing-a-form.md,
// not 7 categorical colors for a story that's really "biggest to smallest."
const BAR_COLOR = 'bg-accent'

export function CategoryBreakdownChart({ entries }: { entries: CategoryBreakdownEntry[] }) {
  if (entries.length === 0) {
    return <p className="p-4 text-sm text-muted">Aucune dépense sur cette période.</p>
  }

  const sorted = [...entries].sort((a, b) => b.amount - a.amount)
  const displayed = sorted.slice(0, MAX_ROWS)
  const rest = sorted.slice(MAX_ROWS)
  const restTotal = rest.reduce((sum, e) => sum + e.amount, 0)
  const rows =
    restTotal > 0
      ? [...displayed, { categoryId: null, categoryName: `Autres (${rest.length})`, amount: restTotal }]
      : displayed

  const maxAmount = Math.max(...rows.map((r) => r.amount), 1)

  return (
    // A real <table> rather than a <ul>: the bar is a visual layered on top of the same
    // name/amount cells a screen reader already gets - no separate "table view" toggle
    // needed since the values are direct-labeled here, never hidden behind a hover.
    <table className="w-full text-sm">
      <caption className="sr-only">Répartition des dépenses par catégorie</caption>
      <tbody>
        {rows.map((row) => (
          <tr key={row.categoryId ?? row.categoryName}>
            <td className="w-1/3 py-1.5 pr-3 text-body">{row.categoryName ?? 'Non catégorisé'}</td>
            <td className="py-1.5">
              <div className="flex items-center gap-2">
                <div className="h-4 flex-1 rounded bg-border">
                  <div
                    className={`h-4 rounded ${BAR_COLOR}`}
                    style={{ width: `${Math.max((row.amount / maxAmount) * 100, row.amount > 0 ? 2 : 0)}%` }}
                  />
                </div>
                <span className="w-24 shrink-0 text-right text-xs font-medium text-heading">
                  {formatCurrency(row.amount)}
                </span>
              </div>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
