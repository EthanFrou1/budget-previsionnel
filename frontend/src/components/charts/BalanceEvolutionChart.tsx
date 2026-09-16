import { useId, useRef, useState } from 'react'
import type { BalancePoint } from '../../api/types'
import { formatCurrency, formatDateFr } from '../../utils/format'

const WIDTH = 800
const HEIGHT = 260
const PADDING = { top: 16, right: 16, bottom: 28, left: 56 }
const PLOT_WIDTH = WIDTH - PADDING.left - PADDING.right
const PLOT_HEIGHT = HEIGHT - PADDING.top - PADDING.bottom

// Single hue (sequential job: "trend over time" - dataviz skill choosing-a-form.md) -
// the app's accent, since a cumulative balance isn't inherently "positive" or
// "negative" the way a single income/expense figure is (unlike the diverging
// pair in MonthlyComparisonChart, which does map to those semantic colors).
const LINE_COLOR = 'stroke-accent'
const FILL_COLOR = 'fill-accent/10'
const DOT_COLOR = 'fill-accent'

function dayIndex(isoDate: string): number {
  const [y, m, d] = isoDate.split('-').map(Number)
  return Date.UTC(y, m - 1, d) / 86_400_000
}

function niceTicks(min: number, max: number, count: number): number[] {
  if (min === max) {
    return [min]
  }
  const step = (max - min) / (count - 1)
  return Array.from({ length: count }, (_, i) => min + step * i)
}

export function BalanceEvolutionChart({ points }: { points: BalancePoint[] }) {
  const svgRef = useRef<SVGSVGElement>(null)
  const [hoverIndex, setHoverIndex] = useState<number | null>(null)
  const [showTable, setShowTable] = useState(false)
  const gradientId = useId()

  if (points.length === 0) {
    return <p className="p-4 text-sm text-muted">Aucune transaction sur cette période.</p>
  }

  const days = points.map((p) => dayIndex(p.date))
  const minDay = days[0]
  const maxDay = days[days.length - 1]
  const dayRange = Math.max(maxDay - minDay, 1)

  const balances = points.map((p) => p.cumulativeBalance)
  let minBalance = Math.min(...balances, 0)
  let maxBalance = Math.max(...balances, 0)
  if (minBalance === maxBalance) {
    minBalance -= 1
    maxBalance += 1
  }
  const balanceRange = maxBalance - minBalance

  function x(i: number): number {
    return PADDING.left + ((days[i] - minDay) / dayRange) * PLOT_WIDTH
  }
  function y(balance: number): number {
    return PADDING.top + (1 - (balance - minBalance) / balanceRange) * PLOT_HEIGHT
  }

  const linePath = points.map((p, i) => `${i === 0 ? 'M' : 'L'}${x(i)},${y(p.cumulativeBalance)}`).join(' ')
  const areaPath = `${linePath} L${x(points.length - 1)},${PADDING.top + PLOT_HEIGHT} L${x(0)},${PADDING.top + PLOT_HEIGHT} Z`

  const yTicks = niceTicks(minBalance, maxBalance, 5)
  const xTickIndices = [0, Math.floor((points.length - 1) / 2), points.length - 1].filter(
    (v, i, arr) => arr.indexOf(v) === i,
  )
  const zeroY = minBalance <= 0 && maxBalance >= 0 ? y(0) : null

  function handlePointerMove(event: React.PointerEvent<SVGRectElement>) {
    const svg = svgRef.current
    if (!svg) return
    const rect = svg.getBoundingClientRect()
    const fraction = (event.clientX - rect.left) / rect.width
    const targetDay = minDay + fraction * dayRange
    let nearest = 0
    let nearestDistance = Infinity
    days.forEach((d, i) => {
      const distance = Math.abs(d - targetDay)
      if (distance < nearestDistance) {
        nearest = i
        nearestDistance = distance
      }
    })
    setHoverIndex(nearest)
  }

  const hovered = hoverIndex !== null ? points[hoverIndex] : null
  const last = points[points.length - 1]

  return (
    <div>
      <div className="flex justify-end px-2 pt-2">
        <button
          onClick={() => setShowTable((v) => !v)}
          className="text-xs font-medium text-accent hover:underline"
        >
          {showTable ? 'Voir le graphique' : 'Voir le tableau'}
        </button>
      </div>

      {showTable ? (
        <div className="max-h-72 overflow-y-auto p-2">
          <table className="w-full text-left text-sm">
            <thead className="text-xs uppercase text-muted">
              <tr>
                <th className="px-2 py-1">Date</th>
                <th className="px-2 py-1 text-right">Variation</th>
                <th className="px-2 py-1 text-right">Solde cumulé</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {points.map((p) => (
                <tr key={p.date}>
                  <td className="px-2 py-1">{formatDateFr(p.date)}</td>
                  <td className="px-2 py-1 text-right">{formatCurrency(p.netChange)}</td>
                  <td className="px-2 py-1 text-right font-medium">{formatCurrency(p.cumulativeBalance)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="relative">
          <svg
            ref={svgRef}
            viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
            preserveAspectRatio="none"
            width="100%"
            height={HEIGHT}
            role="img"
            aria-label="Évolution du solde cumulé"
          >
            {yTicks.map((tick) => (
              <g key={tick}>
                <line
                  x1={PADDING.left}
                  x2={WIDTH - PADDING.right}
                  y1={y(tick)}
                  y2={y(tick)}
                  className="stroke-border"
                  strokeWidth={1}
                />
                <text
                  x={PADDING.left - 8}
                  y={y(tick)}
                  textAnchor="end"
                  dominantBaseline="middle"
                  className="fill-muted text-[10px]"
                >
                  {formatCurrency(tick)}
                </text>
              </g>
            ))}

            {zeroY !== null && (
              <line
                x1={PADDING.left}
                x2={WIDTH - PADDING.right}
                y1={zeroY}
                y2={zeroY}
                className="stroke-muted"
                strokeWidth={1}
              />
            )}

            {xTickIndices.map((i) => (
              <text
                key={i}
                x={x(i)}
                y={HEIGHT - 6}
                textAnchor={i === 0 ? 'start' : i === points.length - 1 ? 'end' : 'middle'}
                className="fill-muted text-[10px]"
              >
                {formatDateFr(points[i].date)}
              </text>
            ))}

            <defs>
              <clipPath id={gradientId}>
                <rect x={PADDING.left} y={PADDING.top} width={PLOT_WIDTH} height={PLOT_HEIGHT} />
              </clipPath>
            </defs>

            <g clipPath={`url(#${gradientId})`}>
              <path d={areaPath} className={FILL_COLOR} stroke="none" />
              <path
                d={linePath}
                className={LINE_COLOR}
                fill="none"
                strokeWidth={2}
                strokeLinejoin="round"
                strokeLinecap="round"
              />
            </g>

            {/* End marker with a surface ring, per marks-and-anatomy - direct-labeled since
                lines label their endpoint rather than every point. */}
            <circle
              cx={x(points.length - 1)}
              cy={y(last.cumulativeBalance)}
              r={4}
              className={`${DOT_COLOR} stroke-surface`}
              strokeWidth={2}
            />
            <text
              x={x(points.length - 1) - 6}
              y={y(last.cumulativeBalance) - 10}
              textAnchor="end"
              className="fill-heading text-xs font-medium"
            >
              {formatCurrency(last.cumulativeBalance)}
            </text>

            {hoverIndex !== null && (
              <line
                x1={x(hoverIndex)}
                x2={x(hoverIndex)}
                y1={PADDING.top}
                y2={PADDING.top + PLOT_HEIGHT}
                className="stroke-muted"
                strokeWidth={1}
              />
            )}
            {hovered && (
              <circle
                cx={x(hoverIndex!)}
                cy={y(hovered.cumulativeBalance)}
                r={4}
                className={`${DOT_COLOR} stroke-surface`}
                strokeWidth={2}
              />
            )}

            <rect
              x={PADDING.left}
              y={PADDING.top}
              width={PLOT_WIDTH}
              height={PLOT_HEIGHT}
              fill="transparent"
              onPointerMove={handlePointerMove}
              onPointerLeave={() => setHoverIndex(null)}
            />
          </svg>

          {hovered && (
            <div className="pointer-events-none absolute top-2 left-2 rounded border border-border bg-surface px-2 py-1 text-xs shadow">
              <p className="text-muted">{formatDateFr(hovered.date)}</p>
              <p className="font-medium text-heading">{formatCurrency(hovered.cumulativeBalance)}</p>
              <p className="text-muted">Variation du jour : {formatCurrency(hovered.netChange)}</p>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
