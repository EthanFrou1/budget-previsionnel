// yyyy-MM-dd (the API's DateOnly format, also what <input type="date"> reads/writes) ->
// dd/MM/yyyy. Deliberately not `new Date(iso)`: that parses as UTC midnight and can shift
// a day in either direction depending on the viewer's timezone offset.
export function formatDateFr(isoDate: string): string {
  const [year, month, day] = isoDate.split('-')
  return `${day}/${month}/${year}`
}

const currencyFormatter = new Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'EUR' })

export function formatCurrency(amount: number): string {
  return currencyFormatter.format(amount)
}

const percentFormatter = new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 2 })

export function formatPercent(value: number): string {
  return `${percentFormatter.format(value)}%`
}

const dateTimeFormatter = new Intl.DateTimeFormat('fr-FR', { dateStyle: 'short', timeStyle: 'short' })

// Unlike formatDateFr's ISO date (no timezone info), this takes a real UTC instant
// (e.g. ImportBatch.ImportedAtUtc, serialized with a "Z" suffix) - `new Date(iso)` is the
// correct way to parse that, and Intl renders it in the viewer's own local time.
export function formatDateTimeFr(isoDateTime: string): string {
  return dateTimeFormatter.format(new Date(isoDateTime))
}

const monthFormatter = new Intl.DateTimeFormat('fr-FR', { month: 'long', year: 'numeric' })

// Same "parse the parts, don't hand the ISO string to `new Date`" precaution as
// formatDateFr - a UTC-midnight parse of the 1st can render as the previous month in a
// negative UTC-offset timezone.
export function formatMonthFr(isoDate: string): string {
  const [year, month] = isoDate.split('-').map(Number)
  return monthFormatter.format(new Date(year, month - 1, 1))
}
