import * as signalR from '@microsoft/signalr'
import { getToken } from '@/lib/api'
import type { ScanJobResponse, StepRunResponse } from '@/types/api'

export interface ScanJobProgressPayload {
  scanJob: ScanJobResponse
  steps: StepRunResponse[]
}

let connection: signalR.HubConnection | null = null
let connectPromise: Promise<signalR.HubConnection> | null = null

function getConnection(): Promise<signalR.HubConnection> {
  if (connection && connection.state === signalR.HubConnectionState.Connected) {
    return Promise.resolve(connection)
  }

  if (!connectPromise) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/scan-progress', {
        accessTokenFactory: () => getToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connectPromise = connection
      .start()
      .then(() => connection!)
      .catch((err) => {
        connectPromise = null
        throw err
      })
  }

  return connectPromise
}

/**
 * Subscribes to live progress for one ScanJob. Returns an unsubscribe
 * function. Callers should treat this as best-effort — if the SignalR
 * connection never comes up (e.g. blocked WebSocket upgrade), the caller's
 * own polling fallback is what actually keeps the UI current.
 */
export function subscribeToScanJob(
  scanJobId: string,
  onUpdate: (payload: ScanJobProgressPayload) => void,
): () => void {
  let cancelled = false
  let activeConnection: signalR.HubConnection | null = null

  const handler = (payload: ScanJobProgressPayload) => {
    if (!cancelled && payload.scanJob.id === scanJobId) {
      onUpdate(payload)
    }
  }

  getConnection()
    .then((conn) => {
      if (cancelled) return
      activeConnection = conn
      conn.on('scanJobUpdated', handler)
      conn.invoke('JoinScanJob', scanJobId).catch(() => {
        // Best-effort — polling fallback covers this.
      })
    })
    .catch(() => {
      // Best-effort — polling fallback covers this.
    })

  return () => {
    cancelled = true
    if (activeConnection) {
      activeConnection.off('scanJobUpdated', handler)
      activeConnection.invoke('LeaveScanJob', scanJobId).catch(() => {})
    }
  }
}
