import { useState } from 'react'
import type { MonthlyComparisonEntry } from '../../api/types'
import { formatCurrency } from '../../utils/format'

const WIDTH = 800
const HEIGHT = 280
const PADDING = { top: 16, right: 16, bottom: 24, left: 56 }
const PLOT_WIDTH = WIDTH - PADDING.left - PADDING.right
const PLOT_HEIGHT = HEIGHT - PADDING.top - PADDING.bottom
const ZERO_Y = PADDING.top + PLOT_HEIGHT / 2
const HALF_HEIGHT = PLOT_HEIGHT / 2
const MAX_BAR_WIDTH = 24
const BAR_GAP = 2

const MONTH_LABELS = ['Jan', 'Fév', 'Mar', 'Avr', 'Mai', 'Jun', 'Jul', 'Aoû', 'Sep', 'Oct', 'Nov', 'Déc']

// Diverging pair (blue<->red) from the validated palette: income above the zero baseline,
// expense below it - "above/below a baseline" is exactly the diverging job per the dataviz
// skill, and blue/red read as opposite poles (unlike e.g. blue/aqua, which are both cool).
const INCOME_COLOR = 'fill-[#2a78d6] dark:fill-[#3987e5]'
const EXPENSE_COLOR = 'fill-[#e34948] dark:fill-[#e66767]'

function monthIndex(monthIso: string): number {
  return Number(monthIso.split('-')[1]) - 1
}

export function MonthlyComparisonChart({ entries }: { entries: MonthlyComparisonEntry[] }) {
  const [hoverIndex, setHoverIndex] = useState<number | null>(null)

  const maxValue = Math.max(...entries.map((e) => e.income), ...entries.map((e) => e.expense), 1)
  const bandWidth = PLOT_WIDTH / entries.length

  function barHeight(amount: number): number {
    return (amount / maxValue) * HALF_HEIGHT
  }

  const hovered = hoverIndex !== null ? entries[hoverIndex] : null

  return (
    <div>
      <div className="flex items-center gap-4 px-2 pb-2 text-xs text-gray-600 dark:text-gray-300">
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-2.5 w-2.5 rounded-sm bg-[#2a78d6] dark:bg-[#3987e5]" />
          Revenus
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-2.5 w-2.5 rounded-sm bg-[#e34948] dark:bg-[#e66767]" />
          Dépenses
        </span>
      </div>

      <div className="relative">
        <svg
          viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
          preserveAspectRatio="none"
          width="100%"
          height={HEIGHT}
          role="img"
          aria-label="Comparatif mensuel revenus / dépenses"
        >
          <line
            x1={PADDING.left}
            x2={WIDTH - PADDING.right}
            y1={ZERO_Y}
            y2={ZERO_Y}
            className="stroke-gray-400 dark:stroke-gray-500"
            strokeWidth={1}
          />
          <text x={PADDING.left - 8} y={ZERO_Y} textAnchor="end" dominantBaseline="middle" className="fill-gray-500 text-[10px] dark:fill-gray-400">
            0 €
          </text>
          <text x={PADDING.left - 8} y={ZERO_Y - HALF_HEIGHT} textAnchor="end" dominantBaseline="middle" className="fill-gray-500 text-[10px] dark:fill-gray-400">
            {formatCurrency(maxValue)}
          </text>
          <text x={PADDING.left - 8} y={ZERO_Y + HALF_HEIGHT} textAnchor="end" dominantBaseline="middle" className="fill-gray-500 text-[10px] dark:fill-gray-400">
            -{formatCurrency(maxValue)}
          </text>

          {entries.map((entry, i) => {
            const bandCenter = PADDING.left + bandWidth * (i + 0.5)
            const barWidth = Math.min(MAX_BAR_WIDTH, bandWidth * 0.35)
            const incomeHeight = barHeight(entry.income)
            const expenseHeight = barHeight(entry.expense)
            const isHovered = hoverIndex === i

            return (
              <g key={entry.month} opacity={hoverIndex === null || isHovered ? 1 : 0.55}>
                <rect
                  x={bandCenter - barWidth - BAR_GAP / 2}
                  y={ZERO_Y - incomeHeight}
                  width={barWidth}
                  height={incomeHeight}
                  rx={3}
                  className={INCOME_COLOR}
                />
                <rect
                  x={bandCenter + BAR_GAP / 2}
                  y={ZERO_Y}
                  width={barWidth}
                  height={expenseHeight}
                  rx={3}
                  className={EXPENSE_COLOR}
                />
                <text x={bandCenter} y={HEIGHT - 6} textAnchor="middle" className="fill-gray-500 text-[10px] dark:fill-gray-400">
                  {MONTH_LABELS[monthIndex(entry.month)]}
                </text>
                {/* Hit target spans the whole band, wider than the two bars, per interaction.md. */}
                <rect
                  x={PADDING.left + bandWidth * i}
                  y={PADDING.top}
                  width={bandWidth}
                  height={PLOT_HEIGHT}
                  fill="transparent"
                  onPointerEnter={() => setHoverIndex(i)}
                  onPointerLeave={() => setHoverIndex(null)}
                />
              </g>
            )
          })}
        </svg>

        {hovered && hoverIndex !== null && (
          <div
            className="pointer-events-none absolute top-2 rounded border border-gray-200 bg-white px-2 py-1 text-xs shadow dark:border-gray-600 dark:bg-gray-800"
            style={{ left: `${((hoverIndex + 0.5) / entries.length) * 100}%`, transform: 'translateX(-50%)' }}
          >
            <p className="font-medium text-gray-900 dark:text-white">{MONTH_LABELS[monthIndex(hovered.month)]}</p>
            <p className="text-gray-600 dark:text-gray-300">Revenus : {formatCurrency(hovered.income)}</p>
            <p className="text-gray-600 dark:text-gray-300">Dépenses : {formatCurrency(hovered.expense)}</p>
            <p className="text-gray-600 dark:text-gray-300">Net : {formatCurrency(hovered.net)}</p>
          </div>
        )}
      </div>
    </div>
  )
}
