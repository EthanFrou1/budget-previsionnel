import { useId, useRef, useState } from 'react'
import type { BalancePoint } from '../../api/types'
import { formatCurrency, formatDateFr } from '../../utils/format'

const WIDTH = 800
const HEIGHT = 260
const PADDING = { top: 16, right: 16, bottom: 28, left: 56 }
const PLOT_WIDTH = WIDTH - PADDING.left - PADDING.right
const PLOT_HEIGHT = HEIGHT - PADDING.top - PADDING.bottom

// Single hue (sequential job: "trend over time" - dataviz skill choosing-a-form.md) -
// the validated palette's blue slot. Matches the diverging pair used in
// MonthlyComparisonChart so "money in" always reads as the same color app-wide.
const LINE_COLOR = 'stroke-[#2a78d6] dark:stroke-[#3987e5]'
const FILL_COLOR = 'fill-[#2a78d6]/10 dark:fill-[#3987e5]/10'
const DOT_COLOR = 'fill-[#2a78d6] dark:fill-[#3987e5]'

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
    return <p className="p-4 text-sm text-gray-500 dark:text-gray-400">Aucune transaction sur cette période.</p>
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
          className="text-xs font-medium text-sky-600 hover:underline dark:text-sky-400"
        >
          {showTable ? 'Voir le graphique' : 'Voir le tableau'}
        </button>
      </div>

      {showTable ? (
        <div className="max-h-72 overflow-y-auto p-2">
          <table className="w-full text-left text-sm">
            <thead className="text-xs uppercase text-gray-500 dark:text-gray-400">
              <tr>
                <th className="px-2 py-1">Date</th>
                <th className="px-2 py-1 text-right">Variation</th>
                <th className="px-2 py-1 text-right">Solde cumulé</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
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
                  className="stroke-gray-200 dark:stroke-gray-700"
                  strokeWidth={1}
                />
                <text x={PADDING.left - 8} y={y(tick)} textAnchor="end" dominantBaseline="middle" className="fill-gray-500 text-[10px] dark:fill-gray-400">
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
                className="stroke-gray-400 dark:stroke-gray-500"
                strokeWidth={1}
              />
            )}

            {xTickIndices.map((i) => (
              <text
                key={i}
                x={x(i)}
                y={HEIGHT - 6}
                textAnchor={i === 0 ? 'start' : i === points.length - 1 ? 'end' : 'middle'}
                className="fill-gray-500 text-[10px] dark:fill-gray-400"
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
              <path d={linePath} className={LINE_COLOR} fill="none" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
            </g>

            {/* End marker with a surface ring, per marks-and-anatomy - direct-labeled since
                lines label their endpoint rather than every point. */}
            <circle cx={x(points.length - 1)} cy={y(last.cumulativeBalance)} r={4} className={`${DOT_COLOR} stroke-white dark:stroke-gray-800`} strokeWidth={2} />
            <text
              x={x(points.length - 1) - 6}
              y={y(last.cumulativeBalance) - 10}
              textAnchor="end"
              className="fill-gray-900 text-xs font-medium dark:fill-white"
            >
              {formatCurrency(last.cumulativeBalance)}
            </text>

            {hoverIndex !== null && (
              <line
                x1={x(hoverIndex)}
                x2={x(hoverIndex)}
                y1={PADDING.top}
                y2={PADDING.top + PLOT_HEIGHT}
                className="stroke-gray-400 dark:stroke-gray-500"
                strokeWidth={1}
              />
            )}
            {hovered && (
              <circle cx={x(hoverIndex!)} cy={y(hovered.cumulativeBalance)} r={4} className={`${DOT_COLOR} stroke-white dark:stroke-gray-800`} strokeWidth={2} />
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
            <div className="pointer-events-none absolute top-2 left-2 rounded border border-gray-200 bg-white px-2 py-1 text-xs shadow dark:border-gray-600 dark:bg-gray-800">
              <p className="text-gray-500 dark:text-gray-400">{formatDateFr(hovered.date)}</p>
              <p className="font-medium text-gray-900 dark:text-white">{formatCurrency(hovered.cumulativeBalance)}</p>
              <p className="text-gray-500 dark:text-gray-400">
                Variation du jour : {formatCurrency(hovered.netChange)}
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
