import { useState } from 'react'
import { useApi } from '@/lib/useApi'
import { api, ApiError } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import type { PlaybookAmendmentResponse } from '@/types/api'
import { Panel, PanelBody, PanelHeader } from '@/components/ui/Panel'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { LoadingBlock } from '@/components/ui/misc'

export function AmendmentsPanel({ scanJobId, onDecided }: { scanJobId: string; onDecided: () => void }) {
  const { session } = useAuth()
  const { data: amendments, loading, refetch } = useApi<PlaybookAmendmentResponse[]>(
    `/api/scan-jobs/${scanJobId}/amendments`,
  )
  const [pendingId, setPendingId] = useState<string | null>(null)

  const decide = async (amendment: PlaybookAmendmentResponse, action: 'approve' | 'reject') => {
    setPendingId(amendment.id)
    try {
      await api.post(`/api/scan-jobs/${scanJobId}/amendments/${amendment.id}/${action}`, {
        decidedBy: session?.email ?? 'operator',
      })
      refetch()
      onDecided()
    } catch (err) {
      alert(err instanceof ApiError ? err.message : `Failed to ${action} amendment.`)
    } finally {
      setPendingId(null)
    }
  }

  if (loading) return <LoadingBlock />
  if (!amendments || amendments.length === 0) return null

  const pending = amendments.filter((a) => a.status === 'Pending')
  const decided = amendments.filter((a) => a.status !== 'Pending')

  return (
    <Panel>
      <PanelHeader
        title="Adaptive Planner amendments"
        description="Proposed argument changes for a not-yet-run step, based on assets discovered so far — never applied automatically."
      />
      <PanelBody className="flex flex-col gap-3">
        {pending.map((amendment) => (
          <div key={amendment.id} className="rounded-md border border-flare/30 bg-flare-muted p-4">
            <p className="text-md font-medium text-primary">Targets step: {amendment.targetStepKey}</p>
            <p className="mt-1 text-sm text-secondary">{amendment.rationale}</p>
            <pre className="mt-2 overflow-auto rounded-sm border border-hairline bg-sunken p-2 font-mono text-xs text-secondary">
              {amendment.proposedArgsTemplate}
            </pre>
            <div className="mt-3 flex gap-2">
              <Button
                variant="primary"
                size="sm"
                loading={pendingId === amendment.id}
                onClick={() => decide(amendment, 'approve')}
              >
                <Icon name="check" className="h-3.5 w-3.5" /> Approve
              </Button>
              <Button
                variant="danger"
                size="sm"
                loading={pendingId === amendment.id}
                onClick={() => decide(amendment, 'reject')}
              >
                <Icon name="x" className="h-3.5 w-3.5" /> Reject
              </Button>
            </div>
          </div>
        ))}
        {decided.map((amendment) => (
          <div key={amendment.id} className="rounded-md border border-hairline bg-sunken/40 p-3 text-sm text-secondary">
            <span className="font-medium text-primary">{amendment.targetStepKey}</span> — {amendment.status} by{' '}
            {amendment.decidedBy} on {amendment.decidedAt && new Date(amendment.decidedAt).toLocaleString()}
          </div>
        ))}
      </PanelBody>
    </Panel>
  )
}
