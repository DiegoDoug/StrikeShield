import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useApi } from '@/lib/useApi'
import { api } from '@/lib/api'
import { subscribeToScanJob } from '@/lib/signalr'
import type { EngagementResponse, ScanJobResponse, StepRunResponse } from '@/types/api'
import { PageHeader, LoadingBlock, ErrorBlock, Breadcrumb } from '@/components/ui/misc'
import { Panel, PanelBody, PanelHeader } from '@/components/ui/Panel'
import { ScanJobStatusBadge } from '@/components/ui/Badge'
import { StepsGraph } from '@/features/scanjob/StepsGraph'
import { ArtifactDialog } from '@/features/scanjob/ArtifactDialog'
import { AmendmentsPanel } from '@/features/scanjob/AmendmentsPanel'
import { AssetsPanel } from '@/features/scanjob/AssetsPanel'

const ACTIVE_STATUSES: ScanJobResponse['status'][] = ['Queued', 'Running', 'AwaitingApproval']
const POLL_INTERVAL_MS = 4000

export function ScanJobDetailPage() {
  const { scanJobId } = useParams<{ scanJobId: string }>()
  const { data: initialJob, loading, error } = useApi<ScanJobResponse>(scanJobId ? `/api/scan-jobs/${scanJobId}` : null)
  const { data: initialSteps } = useApi<StepRunResponse[]>(scanJobId ? `/api/scan-jobs/${scanJobId}/steps` : null)
  const { data: engagement } = useApi<EngagementResponse>(
    initialJob ? `/api/engagements/${initialJob.engagementId}` : null,
    [initialJob?.engagementId],
  )

  const [job, setJob] = useState<ScanJobResponse | undefined>(undefined)
  const [steps, setSteps] = useState<StepRunResponse[] | undefined>(undefined)
  const [selectedStep, setSelectedStep] = useState<StepRunResponse | null>(null)
  const [amendmentsVersion, setAmendmentsVersion] = useState(0)

  useEffect(() => setJob(initialJob), [initialJob])
  useEffect(() => setSteps(initialSteps), [initialSteps])

  // Live progress via SignalR — see docs/PHASED_PLAN.md Phase 9. Falls back
  // to REST polling below if the WebSocket connection never comes up.
  useEffect(() => {
    if (!scanJobId) return
    return subscribeToScanJob(scanJobId, (payload) => {
      setJob(payload.scanJob)
      setSteps(payload.steps)
    })
  }, [scanJobId])

  useEffect(() => {
    if (!scanJobId || !job || !ACTIVE_STATUSES.includes(job.status)) return
    const timer = setInterval(() => {
      Promise.all([
        api.get<ScanJobResponse>(`/api/scan-jobs/${scanJobId}`),
        api.get<StepRunResponse[]>(`/api/scan-jobs/${scanJobId}/steps`),
      ])
        .then(([j, s]) => {
          setJob(j)
          setSteps(s)
        })
        .catch(() => {
          // Best-effort — the next tick (or a SignalR push) will catch up.
        })
    }, POLL_INTERVAL_MS)
    return () => clearInterval(timer)
  }, [scanJobId, job?.status])

  if (loading) return <LoadingBlock />
  if (error) return <ErrorBlock message={error} />
  if (!job) return null

  return (
    <div>
      <PageHeader
        breadcrumb={
          <Breadcrumb
            items={[
              { label: 'Clients', href: '/clients' },
              { label: engagement?.name ?? 'Engagement', href: `/engagements/${job.engagementId}` },
              { label: job.playbookName },
            ]}
          />
        }
        title={job.playbookName}
        description={`Launched ${new Date(job.createdAt).toLocaleString()}`}
        actions={<ScanJobStatusBadge status={job.status} />}
      />

      <div className="flex flex-col gap-5">
        <AmendmentsPanel key={amendmentsVersion} scanJobId={job.id} onDecided={() => setAmendmentsVersion((v) => v + 1)} />

        <Panel>
          <PanelHeader title="Step progress" description="Click a step to view its raw tool output." />
          <PanelBody>
            {steps && steps.length > 0 ? (
              <StepsGraph steps={steps} onSelectStep={setSelectedStep} />
            ) : (
              <p className="text-sm text-secondary">Waiting for the Orchestrator to pick this job up…</p>
            )}
          </PanelBody>
        </Panel>

        <AssetsPanel scanJobId={job.id} />
      </div>

      <ArtifactDialog step={selectedStep} onClose={() => setSelectedStep(null)} />
    </div>
  )
}
