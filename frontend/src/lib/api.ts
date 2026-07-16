import type { ApiErrorBody } from '@/types/api'

const TOKEN_STORAGE_KEY = 'strikeshield.token'

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
    this.name = 'ApiError'
  }
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_STORAGE_KEY)
}

export function setToken(token: string | null) {
  if (token) {
    localStorage.setItem(TOKEN_STORAGE_KEY, token)
  } else {
    localStorage.removeItem(TOKEN_STORAGE_KEY)
  }
}

// Fires whenever the API rejects a request as unauthenticated, so the auth
// context can clear state and bounce to /login without every call site
// having to check for a 401 itself.
type UnauthorizedListener = () => void
let unauthorizedListener: UnauthorizedListener | null = null
export function onUnauthorized(listener: UnauthorizedListener) {
  unauthorizedListener = listener
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}
  const token = getToken()
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  const response = await fetch(path, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (response.status === 401) {
    unauthorizedListener?.()
    throw new ApiError(401, 'Your session has expired. Please log in again.')
  }

  if (response.status === 204) {
    return undefined as T
  }

  const contentType = response.headers.get('content-type') ?? ''

  if (!response.ok) {
    let message = `Request failed (${response.status})`
    if (contentType.includes('application/json')) {
      const errorBody = (await response.json().catch(() => null)) as ApiErrorBody | null
      message = errorBody?.detail ?? errorBody?.title ?? errorBody?.error ?? message
    }
    throw new ApiError(response.status, message)
  }

  if (contentType.includes('application/json')) {
    return (await response.json()) as T
  }

  return undefined as T
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body ?? {}),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body ?? {}),
  patch: <T>(path: string, body?: unknown) => request<T>('PATCH', path, body ?? {}),
  delete: <T>(path: string) => request<T>('DELETE', path),
}

/** Downloads a binary (e.g. PDF report) response and triggers a browser save. */
export async function downloadFile(path: string, suggestedFileName: string): Promise<void> {
  const token = getToken()
  const headers: Record<string, string> = {}
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }

  const response = await fetch(path, { headers })

  if (response.status === 401) {
    unauthorizedListener?.()
    throw new ApiError(401, 'Your session has expired. Please log in again.')
  }

  if (!response.ok) {
    const errorBody = (await response.json().catch(() => null)) as ApiErrorBody | null
    throw new ApiError(response.status, errorBody?.detail ?? errorBody?.error ?? `Request failed (${response.status})`)
  }

  const disposition = response.headers.get('content-disposition')
  const fileNameMatch = disposition?.match(/filename="?([^";]+)"?/)
  const fileName = fileNameMatch?.[1] ?? suggestedFileName

  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
