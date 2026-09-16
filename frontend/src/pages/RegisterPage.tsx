import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/authContext'

export function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await register(email, password)
      navigate('/', { replace: true })
    } catch (err) {
      if (err instanceof ApiError) {
        // Surface FluentValidation's field-level messages (e.g. "Password must be at
        // least 8 characters") over the generic title when present.
        const fieldErrors = err.errors ? Object.values(err.errors).flat() : []
        setError(fieldErrors[0] ?? err.message)
      } else {
        setError('Une erreur est survenue.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-svh items-center justify-center bg-bg px-4">
      <form onSubmit={handleSubmit} className="w-full max-w-sm space-y-4 rounded-lg bg-surface p-6 shadow">
        <h1 className="font-display text-xl font-semibold text-heading">Créer un compte</h1>

        {error && (
          <p role="alert" className="rounded bg-negative/10 px-3 py-2 text-sm text-negative">
            {error}
          </p>
        )}

        <div>
          <label htmlFor="email" className="block text-sm font-medium text-body">
            Email
          </label>
          <input
            id="email"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="mt-1 w-full rounded border border-border px-3 py-2 focus:border-accent focus:outline-none bg-field text-heading"
          />
        </div>

        <div>
          <label htmlFor="password" className="block text-sm font-medium text-body">
            Mot de passe
          </label>
          <input
            id="password"
            type="password"
            required
            minLength={8}
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="mt-1 w-full rounded border border-border px-3 py-2 focus:border-accent focus:outline-none bg-field text-heading"
          />
          <p className="mt-1 text-xs text-muted">8 caractères minimum.</p>
        </div>

        <button
          type="submit"
          disabled={isSubmitting}
          className="w-full rounded bg-accent px-4 py-2 font-medium text-white hover:bg-accent-hover disabled:opacity-50"
        >
          {isSubmitting ? 'Création…' : 'Créer le compte'}
        </button>

        <p className="text-center text-sm text-body">
          Déjà un compte ?{' '}
          <Link to="/login" className="text-accent hover:underline">
            Se connecter
          </Link>
        </p>
      </form>
    </div>
  )
}
