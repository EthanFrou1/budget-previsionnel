import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { ConfirmContext, type ConfirmOptions } from './confirmContext'

interface PendingConfirm extends ConfirmOptions {
  message: string
  resolve: (value: boolean) => void
}

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [pending, setPending] = useState<PendingConfirm | null>(null)

  const confirm = useCallback(
    (message: string, options?: ConfirmOptions) =>
      new Promise<boolean>((resolve) => {
        setPending({ ...options, message, resolve })
      }),
    [],
  )

  function settle(result: boolean) {
    pending?.resolve(result)
    setPending(null)
  }

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      {pending && (
        <ConfirmDialog
          message={pending.message}
          confirmLabel={pending.confirmLabel ?? 'Confirmer'}
          cancelLabel={pending.cancelLabel ?? 'Annuler'}
          danger={pending.danger ?? false}
          onConfirm={() => settle(true)}
          onCancel={() => settle(false)}
        />
      )}
    </ConfirmContext.Provider>
  )
}

function ConfirmDialog({
  message,
  confirmLabel,
  cancelLabel,
  danger,
  onConfirm,
  onCancel,
}: {
  message: string
  confirmLabel: string
  cancelLabel: string
  danger: boolean
  onConfirm: () => void
  onCancel: () => void
}) {
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onCancel()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onCancel])

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4"
      onClick={(e) => {
        if (e.target === e.currentTarget) onCancel()
      }}
    >
      <div
        role="alertdialog"
        aria-modal="true"
        aria-describedby="confirm-dialog-message"
        className="w-full max-w-sm rounded-lg border border-border bg-surface p-4 shadow-xl"
      >
        <p id="confirm-dialog-message" className="text-sm text-heading">
          {message}
        </p>
        <div className="mt-4 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="rounded border border-border px-3 py-1.5 text-sm hover:bg-overlay"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            autoFocus
            className={
              danger
                ? 'rounded bg-negative px-3 py-1.5 text-sm font-medium text-white hover:opacity-90'
                : 'rounded bg-accent px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-hover'
            }
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  )
}
