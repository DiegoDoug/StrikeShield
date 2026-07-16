import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, getToken, onUnauthorized, setToken } from '@/lib/api'
import type { AuthResponse, UserRole } from '@/types/api'

interface Session {
  userId: string
  email: string
  role: UserRole
}

interface AuthContextValue {
  session: Session | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

const SESSION_STORAGE_KEY = 'strikeshield.session'

const AuthContext = createContext<AuthContextValue | null>(null)

function readStoredSession(): Session | null {
  const raw = localStorage.getItem(SESSION_STORAGE_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as Session
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => (getToken() ? readStoredSession() : null))

  useEffect(() => {
    onUnauthorized(() => {
      setToken(null)
      localStorage.removeItem(SESSION_STORAGE_KEY)
      setSession(null)
    })
  }, [])

  const login = async (email: string, password: string) => {
    const response = await api.post<AuthResponse>('/api/auth/login', { email, password })
    setToken(response.token)
    const nextSession: Session = { userId: response.userId, email: response.email, role: response.role }
    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(nextSession))
    setSession(nextSession)
  }

  const logout = () => {
    setToken(null)
    localStorage.removeItem(SESSION_STORAGE_KEY)
    setSession(null)
  }

  const value = useMemo<AuthContextValue>(
    () => ({ session, isAuthenticated: session !== null, login, logout }),
    [session],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return ctx
}
