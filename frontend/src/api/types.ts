// Mirrors BudgetPrevisionnel.Api.Contracts.Auth (Api/Contracts/Auth/AuthContracts.cs).
// Kept hand-written rather than generated for now - revisit with an OpenAPI codegen
// step if/when the number of endpoints makes hand-syncing these error-prone.

export interface UserResponse {
  id: number
  email: string
}

export interface AuthResponse {
  token: string
  expiresAtUtc: string
  user: UserResponse
}

/** RFC 7807 shape returned by AppExceptionHandler for every AppException. */
export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
}

/** Shape returned by ValidationActionFilter for FluentValidation failures. */
export interface ValidationProblemDetails extends ProblemDetails {
  errors?: Record<string, string[]>
}

// Mirrors BudgetPrevisionnel.Api.Contracts.BankAccounts (Api/Contracts/BankAccounts/BankAccountContracts.cs).

export interface BankAccount {
  id: number
  bankName: string
  label: string
  iban: string | null
}

export interface CreateBankAccountRequest {
  bankName: string
  label: string
  iban: string | null
}

export type UpdateBankAccountRequest = CreateBankAccountRequest

export interface ImportSummary {
  totalRowsParsed: number
  newTransactionsImported: number
  duplicatesSkipped: number
  internalTransfersDetected: number
}

// Mirrors BudgetPrevisionnel.Api.Contracts.Categories (Api/Contracts/Categories/CategoryContracts.cs).

export interface Category {
  id: number
  name: string
  icon: string | null
  color: string | null
  parentCategoryId: number | null
  isSystemDefault: boolean
  isOwnedByCurrentUser: boolean
}

export interface CategoryRule {
  id: number
  matchPattern: string
  categoryId: number
  priority: number
}

export interface CreateCategoryRuleRequest {
  matchPattern: string
  categoryId: number
  priority: number
}

// Mirrors BudgetPrevisionnel.Api.Contracts.Transactions (Api/Contracts/Transactions/TransactionContracts.cs).

export interface Transaction {
  id: number
  bankAccountId: number
  bankAccountLabel: string
  date: string
  rawLabel: string
  cleanedLabel: string | null
  amount: number
  categoryId: number | null
  categoryName: string | null
  isInternalTransfer: boolean
}

export interface TransactionPage {
  items: Transaction[]
  totalCount: number
  page: number
  pageSize: number
}

// Mirrors BudgetPrevisionnel.Api.Contracts.Dashboard (Api/Contracts/Dashboard/DashboardContracts.cs).
// CumulativeBalance is a running net-flow total, NOT the bank's real balance - the app
// doesn't persist that (see docs/roadmap.md Lot 8).

export interface BalancePoint {
  date: string
  netChange: number
  cumulativeBalance: number
}

export interface CategoryBreakdownEntry {
  categoryId: number | null
  categoryName: string | null
  amount: number
}

export interface MonthlyComparisonEntry {
  month: string
  income: number
  expense: number
  net: number
}

// Mirrors BudgetPrevisionnel.Api.Contracts.SavingsGoals (Api/Contracts/SavingsGoals/SavingsGoalContracts.cs).

export interface SavingsGoal {
  id: number
  label: string
  targetAmount: number
  currentAmount: number
  targetDate: string | null
  linkedAccountId: number | null
}

export interface CreateSavingsGoalRequest {
  label: string
  targetAmount: number
  currentAmount: number
  targetDate: string | null
  linkedAccountId: number | null
}

export type UpdateSavingsGoalRequest = CreateSavingsGoalRequest

// Mirrors BudgetPrevisionnel.Api.Contracts.Loans (Api/Contracts/Loans/LoanContracts.cs).

export interface Loan {
  id: number
  label: string
  principalAmount: number
  remainingAmount: number
  interestRate: number
  monthlyPayment: number
  endDate: string
}

export interface CreateLoanRequest {
  label: string
  principalAmount: number
  remainingAmount: number
  interestRate: number
  monthlyPayment: number
  endDate: string
}

export type UpdateLoanRequest = CreateLoanRequest

// Mirrors BudgetPrevisionnel.Domain.Enums.RecurrenceFrequency, serialized as a string by
// the API's JsonStringEnumConverter (see Program.cs).
export type RecurrenceFrequency = 'Weekly' | 'Monthly' | 'Yearly'

// Mirrors BudgetPrevisionnel.Api.Contracts.RecurringExpenses (Api/Contracts/RecurringExpenses/RecurringExpenseContracts.cs).

export interface RecurringExpense {
  id: number
  label: string
  amount: number
  categoryId: number | null
  categoryName: string | null
  frequency: RecurrenceFrequency
  startDate: string
  endDate: string | null
}

export interface CreateRecurringExpenseRequest {
  label: string
  amount: number
  categoryId: number | null
  frequency: RecurrenceFrequency
  startDate: string
  endDate: string | null
}

export type UpdateRecurringExpenseRequest = CreateRecurringExpenseRequest

// Mirrors BudgetPrevisionnel.Api.Contracts.Budgets (Api/Contracts/Budgets/BudgetContracts.cs).
// ActualAmount is computed server-side from transactions in that category/month - never sent back up.

export interface BudgetLine {
  id: number
  month: string
  categoryId: number
  categoryName: string
  plannedAmount: number
  actualAmount: number
}

export interface CreateBudgetRequest {
  month: string
  categoryId: number
  plannedAmount: number
}

export interface UpdateBudgetRequest {
  plannedAmount: number
}

// Mirrors BudgetPrevisionnel.Api.Contracts.Forecasting (Api/Contracts/Forecasting/ForecastContracts.cs).
// Source tells the UI why a line exists: an explicit Budget entry takes precedence over a
// RecurringExpense guess for the same category (see ForecastService's precedence rule).

export type ForecastSource = 'Budget' | 'RecurringExpense'

export interface ForecastCategoryLine {
  categoryId: number | null
  categoryLabel: string | null
  amount: number
  source: ForecastSource
}

export interface MonthlyForecast {
  month: string
  categoryLines: ForecastCategoryLine[]
  loanPayments: number
  total: number
}
