import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ConfirmProvider } from './ConfirmDialog'
import { useConfirm } from './confirmContext'

function TestHarness() {
  const confirm = useConfirm()
  return (
    <button
      onClick={async () => {
        const result = await confirm('Supprimer cet élément ?', { confirmLabel: 'Supprimer', danger: true })
        document.title = result ? 'confirmed' : 'cancelled'
      }}
    >
      Supprimer
    </button>
  )
}

function renderHarness() {
  return render(
    <ConfirmProvider>
      <TestHarness />
    </ConfirmProvider>,
  )
}

describe('ConfirmDialog', () => {
  it('resolves true when the confirm button is clicked', async () => {
    renderHarness()
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Supprimer' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(dialog).toHaveTextContent('Supprimer cet élément ?')

    await user.click(within(dialog).getByRole('button', { name: 'Supprimer' }))

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(document.title).toBe('confirmed')
  })

  it('resolves false when cancelled', async () => {
    renderHarness()
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Supprimer' }))
    await screen.findByRole('alertdialog')
    await user.click(screen.getByRole('button', { name: 'Annuler' }))

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(document.title).toBe('cancelled')
  })

  it('resolves false on Escape', async () => {
    renderHarness()
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Supprimer' }))
    await screen.findByRole('alertdialog')
    await user.keyboard('{Escape}')

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(document.title).toBe('cancelled')
  })

  it('resolves false on a backdrop click', async () => {
    renderHarness()
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Supprimer' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(dialog.parentElement!)

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(document.title).toBe('cancelled')
  })

  it('throws when useConfirm is used outside a ConfirmProvider', () => {
    // Swallow the expected React error-boundary console.error noise for this one case.
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => render(<TestHarness />)).toThrow('useConfirm must be used within a ConfirmProvider')
    spy.mockRestore()
  })
})
