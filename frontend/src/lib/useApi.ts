import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, api } from '@/lib/api'

interface UseApiResult<T> {
  data: T | undefined
  loading: boolean
  error: string | null
  refetch: () => void
}

/** GETs `path` on mount and whenever `deps` changes. Pass `path` as null to skip fetching (e.g. while a param isn't ready yet). */
export function useApi<T>(path: string | null, deps: unknown[] = []): UseApiResult<T> {
  const [data, setData] = useState<T | undefined>(undefined)
  const [loading, setLoading] = useState(path !== null)
  const [error, setError] = useState<string | null>(null)
  const requestId = useRef(0)

  const load = useCallback(() => {
    if (path === null) {
      setLoading(false)
      return
    }
    const id = ++requestId.current
    setLoading(true)
    setError(null)
    api
      .get<T>(path)
      .then((result) => {
        if (id === requestId.current) {
          setData(result)
          setLoading(false)
        }
      })
      .catch((err) => {
        if (id === requestId.current) {
          setError(err instanceof ApiError ? err.message : 'Something went wrong.')
          setLoading(false)
        }
      })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [path, ...deps])

  useEffect(() => {
    load()
  }, [load])

  return { data, loading, error, refetch: load }
}

/** Polls `path` on an interval, in addition to fetching once on mount. Useful as a SignalR fallback. */
export function usePolledApi<T>(path: string | null, intervalMs: number, deps: unknown[] = []): UseApiResult<T> {
  const result = useApi<T>(path, deps)
  useEffect(() => {
    if (path === null) return
    const timer = setInterval(result.refetch, intervalMs)
    return () => clearInterval(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [path, intervalMs, ...deps])
  return result
}
