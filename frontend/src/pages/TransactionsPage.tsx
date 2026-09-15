import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { ApiError } from '../api/client'
import type { BankAccount, Category, Transaction, TransactionPage as TransactionPageResponse } from '../api/types'
import { AppHeader } from '../components/AppHeader'
import { useApiClient } from '../auth/useApiClient'
import { formatCurrency, formatDateFr } from '../utils/format'
import { CategoryRulesSection } from './CategoryRulesSection'

const PAGE_SIZE = 50

interface Filters {
  bankAccountId: string
  categoryId: string
  fromDate: string
  toDate: string
  excludeInternalTransfers: boolean
}

const EMPTY_FILTERS: Filters = {
  bankAccountId: '',
  categoryId: '',
  fromDate: '',
  toDate: '',
  excludeInternalTransfers: false,
}

function buildQueryString(filters: Filters, search: string, page: number): string {
  const params = new URLSearchParams()
  if (filters.bankAccountId) params.set('bankAccountId', filters.bankAccountId)
  if (filters.categoryId) params.set('categoryId', filters.categoryId)
  if (filters.fromDate) params.set('fromDate', filters.fromDate)
  if (filters.toDate) params.set('toDate', filters.toDate)
  if (search.trim()) params.set('search', search.trim())
  if (filters.excludeInternalTransfers) params.set('excludeInternalTransfers', 'true')
  params.set('page', String(page))
  params.set('pageSize', String(PAGE_SIZE))
  return params.toString()
}

function errorMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : 'Une erreur est survenue.'
}

export function TransactionsPage() {
  const apiClient = useApiClient()
  const queryClient = useQueryClient()

  const [filters, setFilters] = useState<Filters>(EMPTY_FILTERS)
  const [searchInput, setSearchInput] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [page, setPage] = useState(1)

  // Debounces the search box so every keystroke doesn't fire a request - the rest of the
  // filters below reset the page directly from their own onChange instead, since they
  // change rarely enough not to need debouncing.
  useEffect(() => {
    const timeout = setTimeout(() => {
      setDebouncedSearch(searchInput)
      setPage(1)
    }, 400)
    return () => clearTimeout(timeout)
  }, [searchInput])

  function updateFilters(partial: Partial<Filters>) {
    setFilters((f) => ({ ...f, ...partial }))
    setPage(1)
  }

  const accountsQuery = useQuery({
    queryKey: ['bank-accounts'],
    queryFn: () => apiClient<BankAccount[]>('/api/bank-accounts'),
  })

  const categoriesQuery = useQuery({
    queryKey: ['categories'],
    queryFn: () => apiClient<Category[]>('/api/categories'),
  })

  const transactionsQuery = useQuery({
    queryKey: ['transactions', filters, debouncedSearch, page],
    queryFn: () =>
      apiClient<TransactionPageResponse>(`/api/transactions?${buildQueryString(filters, debouncedSearch, page)}`),
    placeholderData: keepPreviousData,
  })

  const updateCategoryMutation = useMutation({
    mutationFn: ({ transactionId, categoryId }: { transactionId: number; categoryId: number | null }) =>
      apiClient(`/api/transactions/${transactionId}/category`, { method: 'PUT', body: { categoryId } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['transactions'] }),
  })

  const categories = [...(categoriesQuery.data ?? [])].sort((a, b) => a.name.localeCompare(b.name))
  const totalPages = transactionsQuery.data ? Math.max(1, Math.ceil(transactionsQuery.data.totalCount / PAGE_SIZE)) : 1

  return (
    <div className="min-h-svh bg-gray-50 dark:bg-gray-900">
      <AppHeader />

      <main className="mx-auto max-w-5xl space-y-6 p-4">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-white">Transactions</h1>

        <section className="grid gap-3 rounded-lg bg-white p-4 shadow dark:bg-gray-800 sm:grid-cols-3 lg:grid-cols-6">
          <div>
            <label htmlFor="filter-account" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
              Compte
            </label>
            <select
              id="filter-account"
              value={filters.bankAccountId}
              onChange={(e) => updateFilters({ bankAccountId: e.target.value })}
              className="mt-1 w-full rounded border border-gray-300 px-2 py-1.5 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            >
              <option value="">Tous les comptes</option>
              {accountsQuery.data?.map((account) => (
                <option key={account.id} value={account.id}>
                  {account.label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="filter-category" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
              Catégorie
            </label>
            <select
              id="filter-category"
              value={filters.categoryId}
              onChange={(e) => updateFilters({ categoryId: e.target.value })}
              className="mt-1 w-full rounded border border-gray-300 px-2 py-1.5 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            >
              <option value="">Toutes catégories</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="filter-from" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
              Du
            </label>
            <input
              id="filter-from"
              type="date"
              value={filters.fromDate}
              onChange={(e) => updateFilters({ fromDate: e.target.value })}
              className="mt-1 w-full rounded border border-gray-300 px-2 py-1.5 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            />
          </div>

          <div>
            <label htmlFor="filter-to" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
              Au
            </label>
            <input
              id="filter-to"
              type="date"
              value={filters.toDate}
              onChange={(e) => updateFilters({ toDate: e.target.value })}
              className="mt-1 w-full rounded border border-gray-300 px-2 py-1.5 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            />
          </div>

          <div className="sm:col-span-2">
            <label htmlFor="filter-search" className="block text-xs font-medium text-gray-700 dark:text-gray-300">
              Recherche
            </label>
            <input
              id="filter-search"
              type="search"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Libellé…"
              className="mt-1 w-full rounded border border-gray-300 px-2 py-1.5 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            />
          </div>

          <label className="flex items-end gap-2 pb-1.5 text-sm text-gray-700 dark:text-gray-300 lg:col-span-6">
            <input
              type="checkbox"
              checked={filters.excludeInternalTransfers}
              onChange={(e) => updateFilters({ excludeInternalTransfers: e.target.checked })}
              className="h-4 w-4 rounded border-gray-300"
            />
            Exclure les virements internes
          </label>
        </section>

        {transactionsQuery.isPending && <p className="text-gray-600 dark:text-gray-300">Chargement…</p>}

        {transactionsQuery.isError && (
          <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
            {errorMessage(transactionsQuery.error)}
          </p>
        )}

        {transactionsQuery.data && (
          <div className="overflow-x-auto rounded-lg bg-white shadow dark:bg-gray-800">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-gray-200 text-xs uppercase text-gray-500 dark:border-gray-700 dark:text-gray-400">
                <tr>
                  <th className="px-3 py-2">Date</th>
                  <th className="px-3 py-2">Compte</th>
                  <th className="px-3 py-2">Libellé</th>
                  <th className="px-3 py-2 text-right">Montant</th>
                  <th className="px-3 py-2">Catégorie</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
                {transactionsQuery.data.items.map((transaction: Transaction) => (
                  <tr key={transaction.id}>
                    <td className="whitespace-nowrap px-3 py-2 text-gray-600 dark:text-gray-300">
                      {formatDateFr(transaction.date)}
                    </td>
                    <td className="px-3 py-2 text-gray-600 dark:text-gray-300">{transaction.bankAccountLabel}</td>
                    <td className="px-3 py-2 text-gray-900 dark:text-white">
                      {transaction.cleanedLabel ?? transaction.rawLabel}
                      {transaction.isInternalTransfer && (
                        <span className="ml-2 rounded bg-sky-50 px-1.5 py-0.5 text-xs text-sky-700 dark:bg-sky-950 dark:text-sky-300">
                          Virement interne
                        </span>
                      )}
                    </td>
                    <td
                      className={`whitespace-nowrap px-3 py-2 text-right font-medium ${
                        transaction.amount < 0
                          ? 'text-red-700 dark:text-red-400'
                          : 'text-green-700 dark:text-green-400'
                      }`}
                    >
                      {formatCurrency(transaction.amount)}
                    </td>
                    <td className="px-3 py-2">
                      <select
                        aria-label={`Catégorie de ${transaction.cleanedLabel ?? transaction.rawLabel}`}
                        value={transaction.categoryId?.toString() ?? ''}
                        onChange={(e) =>
                          updateCategoryMutation.mutate({
                            transactionId: transaction.id,
                            categoryId: e.target.value === '' ? null : Number(e.target.value),
                          })
                        }
                        className="w-full rounded border border-gray-300 px-2 py-1 text-sm dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                      >
                        <option value="">Non catégorisé</option>
                        {categories.map((category) => (
                          <option key={category.id} value={category.id}>
                            {category.name}
                          </option>
                        ))}
                      </select>
                    </td>
                  </tr>
                ))}
                {transactionsQuery.data.items.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-3 py-6 text-center text-gray-500 dark:text-gray-400">
                      Aucune transaction ne correspond à ces filtres.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>

            <div className="flex items-center justify-between border-t border-gray-200 px-3 py-2 text-sm text-gray-600 dark:border-gray-700 dark:text-gray-300">
              <button
                onClick={() => setPage((p) => p - 1)}
                disabled={page <= 1}
                className="rounded border border-gray-300 px-3 py-1 hover:bg-gray-100 disabled:opacity-50 dark:border-gray-600 dark:hover:bg-gray-700"
              >
                Précédent
              </button>
              <span>
                Page {page} / {totalPages} ({transactionsQuery.data.totalCount} transaction(s))
              </span>
              <button
                onClick={() => setPage((p) => p + 1)}
                disabled={page >= totalPages}
                className="rounded border border-gray-300 px-3 py-1 hover:bg-gray-100 disabled:opacity-50 dark:border-gray-600 dark:hover:bg-gray-700"
              >
                Suivant
              </button>
            </div>
          </div>
        )}

        <CategoryRulesSection categories={categories} />
      </main>
    </div>
  )
}
