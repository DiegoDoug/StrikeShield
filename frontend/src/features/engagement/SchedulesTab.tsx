import { useState, type FormEvent } from 'react'
import { useApi } from '@/lib/useApi'
import { api, ApiError } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import type { EngagementResponse, PlaybookResponse, ScanScheduleResponse, TargetResponse } from '@/types/api'
import { Panel, PanelHeader } from '@/components/ui/Panel'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { FormField, Input, Select } from '@/components/ui/Field'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, LoadingBlock, ErrorBlock } from '@/components/ui/misc'

export function SchedulesTab({ engagement }: { engagement: EngagementResponse }) {
  const { data: schedules, loading, error, refetch } = useApi<ScanScheduleResponse[]>(
    `/api/scan-schedules?engagementId=${engagement.id}`,
  )
  const { data: targets } = useApi<TargetResponse[]>(`/api/targets?projectId=${engagement.projectId}`)
  const [dialogOpen, setDialogOpen] = useState(false)
  const targetById = new Map((targets ?? []).map((t) => [t.id, t]))

  const onToggle = async (schedule: ScanScheduleResponse) => {
    try {
      await api.patch(`/api/scan-schedules/${schedule.id}`, { enabled: !schedule.enabled })
      refetch()
    } catch (err) {
      alert(err instanceof ApiError ? err.message : 'Failed to update schedule.')
    }
  }

  const onDelete = async (schedule: ScanScheduleResponse) => {
    if (!confirm(`Delete this recurring schedule (${schedule.cronExpression})?`)) return
    try {
      await api.delete(`/api/scan-schedules/${schedule.id}`)
      refetch()
    } catch (err) {
      alert(err instanceof ApiError ? err.message : 'Failed to delete schedule.')
    }
  }

  return (
    <div>
      <Panel>
        <PanelHeader
          title="Recurring scans"
          description="Cron-based, evaluated in UTC. The scope/approval gate is re-checked on every fire, not just at creation."
          actions={
            <Button variant="primary" onClick={() => setDialogOpen(true)}>
              <Icon name="plus" /> New schedule
            </Button>
          }
        />
        {loading && <LoadingBlock />}
        {error && (
          <div className="px-5 py-4">
            <ErrorBlock message={error} />
          </div>
        )}
        {schedules && schedules.length === 0 && (
          <EmptyState
            title="No recurring schedules"
            description="Create one to keep scanning this engagement's targets on a cadence."
            action={
              <Button variant="primary" onClick={() => setDialogOpen(true)}>
                <Icon name="plus" /> New schedule
              </Button>
            }
          />
        )}
        {schedules && schedules.length > 0 && (
          <ul className="divide-y divide-hairline">
            {schedules.map((schedule) => (
              <li key={schedule.id} className="flex items-center justify-between gap-4 px-5 py-4">
                <div>
                  <p className="flex items-center gap-2 text-md font-medium text-primary">
                    <span className="font-mono text-sm">{schedule.cronExpression}</span>
                    <span className="text-xs text-tertiary">· {schedule.playbookName}</span>
                  </p>
                  <p className="text-xs text-tertiary">
                    {targetById.get(schedule.targetId)?.value ?? schedule.targetId}
                    {schedule.lastFiredAt && (
                      <>
                        {' '}
                        · last fired {new Date(schedule.lastFiredAt).toLocaleString()} (
                        {schedule.lastFireOutcome})
                        {schedule.lastSkipReason && ` — ${schedule.lastSkipReason}`}
                      </>
                    )}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => onToggle(schedule)}
                    className={`rounded-full px-2.5 py-1 text-xs font-medium ${
                      schedule.enabled
                        ? 'bg-status-completed/15 text-status-completed'
                        : 'bg-sunken text-tertiary'
                    }`}
                  >
                    {schedule.enabled ? 'Enabled' : 'Disabled'}
                  </button>
                  <button
                    type="button"
                    onClick={() => onDelete(schedule)}
                    aria-label="Delete schedule"
                    className="rounded-sm p-1.5 text-tertiary hover:bg-sev-critical-bg hover:text-sev-critical"
                  >
                    <Icon name="trash" className="h-4 w-4" />
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Panel>

      <CreateScheduleDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        engagement={engagement}
        targets={targets ?? []}
        onCreated={() => {
          setDialogOpen(false)
          refetch()
        }}
      />
    </div>
  )
}

function CreateScheduleDialog({
  open,
  onClose,
  engagement,
  targets,
  onCreated,
}: {
  open: boolean
  onClose: () => void
  engagement: EngagementResponse
  targets: TargetResponse[]
  onCreated: () => void
}) {
  const { session } = useAuth()
  const { data: playbooks } = useApi<PlaybookResponse[]>(open ? '/api/playbooks' : null)
  const [targetId, setTargetId] = useState('')
  const [playbookSlug, setPlaybookSlug] = useState('')
  const [cronExpression, setCronExpression] = useState('0 3 * * *')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await api.post('/api/scan-schedules', {
        engagementId: engagement.id,
        targetId,
        playbookName: playbookSlug,
        cronExpression,
        createdBy: session?.email ?? 'operator',
      })
      onCreated()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to create schedule.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="New recurring schedule"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} type="button">
            Cancel
          </Button>
          <Button variant="primary" type="submit" form="create-schedule-form" loading={submitting}>
            Create
          </Button>
        </>
      }
    >
      <form id="create-schedule-form" onSubmit={onSubmit} className="flex flex-col gap-4">
        <FormField label="Target" htmlFor="schedule-target">
          <Select id="schedule-target" required value={targetId} onChange={(e) => setTargetId(e.target.value)}>
            <option value="" disabled>
              Select a target…
            </option>
            {targets.map((t) => (
              <option key={t.id} value={t.id}>
                {t.value}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label="Playbook" htmlFor="schedule-playbook">
          <Select
            id="schedule-playbook"
            required
            value={playbookSlug}
            onChange={(e) => setPlaybookSlug(e.target.value)}
          >
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
        <FormField label="Cron expression" htmlFor="cron" hint="Standard 5-field cron, evaluated in UTC.">
          <Input
            id="cron"
            required
            className="font-mono"
            value={cronExpression}
            onChange={(e) => setCronExpression(e.target.value)}
          />
        </FormField>
        {error && <p className="text-sm text-sev-critical">{error}</p>}
      </form>
    </Dialog>
  )
}
