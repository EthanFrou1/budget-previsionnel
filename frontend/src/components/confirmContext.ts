import { createContext, useContext } from 'react'

export interface ConfirmOptions {
  confirmLabel?: string
  cancelLabel?: string
  /** Red confirm button, for destructive actions (delete). */
  danger?: boolean
}

export type ConfirmFn = (message: string, options?: ConfirmOptions) => Promise<boolean>

// Split from ConfirmDialog.tsx (which only exports components): a file that exports both
// a component and other values (a context, a hook) opts it out of React Fast Refresh, so
// this stays a plain module - no JSX here. Same split as auth/authContext.ts.
export const ConfirmContext = createContext<ConfirmFn | null>(null)

/**
 * Promise-based replacement for the native `confirm()` - same call shape at the call
 * site (`if (!(await confirm(message))) return`), but renders a themed modal instead of
 * the browser's own unstyled dialog (which also can't be exercised with Testing Library
 * the way this can).
 */
export function useConfirm(): ConfirmFn {
  const confirm = useContext(ConfirmContext)
  if (!confirm) {
    throw new Error('useConfirm must be used within a ConfirmProvider')
  }
  return confirm
}
