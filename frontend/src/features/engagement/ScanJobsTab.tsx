import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useApi } from '@/lib/useApi'
import { api, ApiError } from '@/lib/api'
import type { EngagementResponse, PlaybookResponse, ScanJobResponse, TargetResponse } from '@/types/api'
import { Panel, PanelHeader } from '@/components/ui/Panel'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { FormField, Select } from '@/components/ui/Field'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, LoadingBlock, ErrorBlock } from '@/components/ui/misc'
import { ScanJobStatusBadge } from '@/components/ui/Badge'

export function ScanJobsTab({ engagement }: { engagement: EngagementResponse }) {
  const navigate = useNavigate()
  const {
    data: scanJobs,
    loading,
    error,
    refetch,
  } = useApi<ScanJobResponse[]>(`/api/scan-jobs?engagementId=${engagement.id}`)
  const { data: targets } = useApi<TargetResponse[]>(`/api/targets?projectId=${engagement.projectId}`)
  const [wizardOpen, setWizardOpen] = useState(false)

  const targetById = new Map((targets ?? []).map((t) => [t.id, t]))

  return (
    <div>
      <Panel>
        <PanelHeader
          title="Scan jobs"
          description="Every launch runs through the same scope/approval gate — see Authorization & scope above."
          actions={
            <Button variant="primary" onClick={() => setWizardOpen(true)}>
              <Icon name="play" /> Launch scan
            </Button>
          }
        />
        {loading && <LoadingBlock />}
        {error && (
          <div className="px-5 py-4">
            <ErrorBlock message={error} />
          </div>
        )}
        {scanJobs && scanJobs.length === 0 && (
          <EmptyState
            title="No scans launched yet"
            description="Launch a playbook against one of this project's targets."
            action={
              <Button variant="primary" onClick={() => setWizardOpen(true)}>
                <Icon name="play" /> Launch scan
              </Button>
            }
          />
        )}
        {scanJobs && scanJobs.length > 0 && (
          <ul className="divide-y divide-hairline">
            {scanJobs.map((job) => (
              <li key={job.id}>
                <button
                  type="button"
                  onClick={() => navigate(`/scan-jobs/${job.id}`)}
                  className="flex w-full items-center justify-between gap-4 px-5 py-4 text-left transition-colors duration-[var(--ss-duration-fast)] hover:bg-elevated"
                >
                  <div>
                    <p className="text-md font-medium text-primary">{job.playbookName}</p>
                    <p className="text-xs text-tertiary">
                      {targetById.get(job.targetId)?.value ?? job.targetId} ·{' '}
                      {new Date(job.createdAt).toLocaleString()}
                    </p>
                  </div>
                  <ScanJobStatusBadge status={job.status} />
                </button>
              </li>
            ))}
          </ul>
        )}
      </Panel>

      <LaunchScanDialog
        open={wizardOpen}
        onClose={() => setWizardOpen(false)}
        engagement={engagement}
        targets={targets ?? []}
        onLaunched={(scanJobId) => {
          setWizardOpen(false)
          refetch()
          navigate(`/scan-jobs/${scanJobId}`)
        }}
      />
    </div>
  )
}

function isWithinScopeWindow(engagement: EngagementResponse): boolean {
  const now = Date.now()
  return now >= new Date(engagement.scopeStart).getTime() && now <= new Date(engagement.scopeEnd).getTime()
}

function LaunchScanDialog({
  open,
  onClose,
  engagement,
  targets,
  onLaunched,
}: {
  open: boolean
  onClose: () => void
  engagement: EngagementResponse
  targets: TargetResponse[]
  onLaunched: (scanJobId: string) => void
}) {
  const { data: playbooks } = useApi<PlaybookResponse[]>(open ? '/api/playbooks' : null)
  const [targetId, setTargetId] = useState('')
  const [playbookSlug, setPlaybookSlug] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const withinWindow = isWithinScopeWindow(engagement)
  const canLaunch = engagement.isApproved && withinWindow

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!targetId || !playbookSlug) return
    setSubmitting(true)
    setError(null)
    try {
      const created = await api.post<ScanJobResponse>('/api/scan-jobs', {
        engagementId: engagement.id,
        targetId,
        playbookName: playbookSlug,
      })
      onLaunched(created.id)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to launch scan.')
    } finally {
      setSubmitting(false)
    }
  }

  const selectedPlaybook = playbooks?.find((p) => p.slug === playbookSlug)

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Launch scan"
      widthClassName="max-w-lg"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} type="button">
            Cancel
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="launch-scan-form"
            loading={submitting}
            disabled={!canLaunch || !targetId || !playbookSlug}
          >
            <Icon name="play" /> Launch
          </Button>
        </>
      }
    >
      {!canLaunch && (
        <div className="mb-4 rounded-md border border-sev-medium/30 bg-sev-medium-bg px-3 py-2.5 text-sm text-sev-medium">
          {!engagement.isApproved
            ? 'This engagement is not approved yet — approve it before launching a scan.'
            : "This engagement is outside its scope window — scans can't be launched right now."}
        </div>
      )}
      <form id="launch-scan-form" onSubmit={onSubmit} className="flex flex-col gap-4">
        <FormField label="Target" htmlFor="target">
          <Select id="target" required value={targetId} onChange={(e) => setTargetId(e.target.value)}>
            <option value="" disabled>
              Select a target…
            </option>
            {targets.map((t) => (
              <option key={t.id} value={t.id}>
                {t.value} ({t.type})
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label="Playbook" htmlFor="playbook">
          <Select id="playbook" required value={playbookSlug} onChange={(e) => setPlaybookSlug(e.target.value)}>
            <option value="" disabled>
              Select a playbook…
            </option>
            {playbooks?.map((p) => (
              <option key={p.id} value={p.slug}>
                {p.name}
              </option>
            ))}
          </Select>
        </FormField>
        {selectedPlaybook && (
          <div className="rounded-md border border-hairline bg-sunken px-3 py-2.5 text-xs text-secondary">
            <p className="mb-1 font-medium text-primary">{selectedPlaybook.steps.length} step(s)</p>
            <p>{selectedPlaybook.steps.map((s) => s.toolName).join(' → ')}</p>
            {selectedPlaybook.description && <p className="mt-1.5">{selectedPlaybook.description}</p>}
          </div>
        )}
        {targets.length === 0 && (
          <p className="text-sm text-sev-medium">This project has no targets yet — add one before launching a scan.</p>
        )}
        {error && <p className="text-sm text-sev-critical">{error}</p>}
      </form>
    </Dialog>
  )
}
