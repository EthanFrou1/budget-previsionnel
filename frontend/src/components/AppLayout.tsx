import type { ReactNode } from 'react'
import { AppSidebar } from './AppSidebar'
import { ConfirmProvider } from './ConfirmDialog'

/**
 * Shared page shell (sidebar + content column) - extracted once every
 * protected page had grown its own copy of the same div/AppHeader/main
 * boilerplate (6 pages by the time AppHeader became AppSidebar). One fixed
 * content width for every page (no more per-page maxWidth) - pages used to
 * pick their own (max-w-3xl/4xl/5xl), which read as an unintentional
 * inconsistency rather than a deliberate one from page to page.
 *
 * ConfirmProvider lives here rather than at the app root: every protected
 * page renders through this shell, so every delete/destructive action gets
 * useConfirm() for free without wrapping App.tsx itself (LoginPage/RegisterPage
 * never need it).
 */
export function AppLayout({ children }: { children: ReactNode }) {
  return (
    <ConfirmProvider>
      <div className="flex min-h-svh flex-col bg-bg lg:flex-row">
        <AppSidebar />
        <main className="min-w-0 flex-1">
          <div className="mx-auto max-w-5xl space-y-6 p-4">{children}</div>
        </main>
      </div>
    </ConfirmProvider>
  )
}
