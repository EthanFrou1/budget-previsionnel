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
    <div className="flex min-h-svh items-center justify-center bg-gray-50 px-4 dark:bg-gray-900">
      <form
        onSubmit={handleSubmit}
        className="w-full max-w-sm space-y-4 rounded-lg bg-white p-6 shadow dark:bg-gray-800"
      >
        <h1 className="text-xl font-semibold text-gray-900 dark:text-white">Créer un compte</h1>

        {error && (
          <p
            role="alert"
            className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300"
          >
            {error}
          </p>
        )}

        <div>
          <label htmlFor="email" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Email
          </label>
          <input
            id="email"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          />
        </div>

        <div>
          <label htmlFor="password" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
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
            className="mt-1 w-full rounded border border-gray-300 px-3 py-2 focus:border-sky-500 focus:outline-none dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          />
          <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">8 caractères minimum.</p>
        </div>

        <button
          type="submit"
          disabled={isSubmitting}
          className="w-full rounded bg-sky-600 px-4 py-2 font-medium text-white hover:bg-sky-700 disabled:opacity-50"
        >
          {isSubmitting ? 'Création…' : 'Créer le compte'}
        </button>

        <p className="text-center text-sm text-gray-600 dark:text-gray-400">
          Déjà un compte ?{' '}
          <Link to="/login" className="text-sky-600 hover:underline dark:text-sky-400">
            Se connecter
          </Link>
        </p>
      </form>
    </div>
  )
}
