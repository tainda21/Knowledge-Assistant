/* eslint-disable react-refresh/only-export-components */
import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5090'
const TOKEN_KEY = 'ka-token'

export interface UserDto {
  id: string
  username: string
  role: 'Admin' | 'User'
  createdAt: string
}

interface AuthContextValue {
  user: UserDto | null
  token: string | null
  isLoading: boolean
  login: (token: string, user: UserDto) => void
  logout: () => void
  authFetch: (input: string, init?: RequestInit) => Promise<Response>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem(TOKEN_KEY))
  const [user, setUser] = useState<UserDto | null>(null)
  const [isLoading, setIsLoading] = useState(() => Boolean(localStorage.getItem(TOKEN_KEY)))

  const login = useCallback((newToken: string, newUser: UserDto) => {
    localStorage.setItem(TOKEN_KEY, newToken)
    setToken(newToken)
    setUser(newUser)
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem(TOKEN_KEY)
    setToken(null)
    setUser(null)
  }, [])

  const authFetch = useCallback(
    (input: string, init: RequestInit = {}) => {
      const headers = new Headers(init.headers)
      if (token) headers.set('Authorization', `Bearer ${token}`)
      return fetch(input, { ...init, headers })
    },
    [token]
  )

  // Re-validate token on mount
  useEffect(() => {
    if (!token) return
    const controller = new AbortController()
    fetch(`${apiBaseUrl}/api/auth/me`, {
      headers: { Authorization: `Bearer ${token}` },
      signal: controller.signal,
    })
      .then(async res => {
        if (!res.ok) throw new Error('invalid')
        return res.json() as Promise<UserDto>
      })
      .then(u => setUser(u))
      .catch(() => { localStorage.removeItem(TOKEN_KEY); setToken(null) })
      .finally(() => setIsLoading(false))
    return () => controller.abort()
  }, [token])

  return (
    <AuthContext.Provider value={{ user, token, isLoading, login, logout, authFetch }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}

export { apiBaseUrl }
