import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '@/lib/auth'
import { ApiError } from '@/lib/api'
import { Button } from '@/components/ui/Button'
import { FormField, Input } from '@/components/ui/Field'
import { Icon } from '@/components/ui/Icon'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('admin@strikeshield.local')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await login(email, password)
      navigate('/clients', { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Login failed. Check the API is reachable.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-base px-4">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-3">
          <span className="flex h-11 w-11 items-center justify-center rounded-md bg-flare text-on-accent shadow-flare-glow">
            <Icon name="shield-check" className="h-6 w-6" />
          </span>
          <div className="text-center">
            <h1 className="text-xl font-medium text-primary">StrikeShield</h1>
            <p className="text-sm text-secondary">Sign in to your operator console</p>
          </div>
        </div>

        <form onSubmit={onSubmit} className="flex flex-col gap-4 rounded-md border border-hairline bg-surface p-6 shadow-ss-md">
          <FormField label="Email" htmlFor="email">
            <Input
              id="email"
              type="email"
              autoComplete="username"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </FormField>
          <FormField label="Password" htmlFor="password">
            <Input
              id="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </FormField>
          {error && <p className="text-sm text-sev-critical">{error}</p>}
          <Button type="submit" variant="primary" loading={loading} className="mt-1 w-full">
            Sign in
          </Button>
        </form>
      </div>
    </div>
  )
}
