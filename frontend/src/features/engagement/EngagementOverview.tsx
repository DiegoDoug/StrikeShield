import { useState } from 'react'
import { useAuth } from '@/lib/auth'
import { api, ApiError } from '@/lib/api'
import type { EngagementResponse } from '@/types/api'
import { Panel, PanelBody, PanelHeader } from '@/components/ui/Panel'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'

function isWithinScopeWindow(engagement: EngagementResponse): boolean {
  const now = Date.now()
  return now >= new Date(engagement.scopeStart).getTime() && now <= new Date(engagement.scopeEnd).getTime()
}

export function EngagementOverview({
  engagement,
  onApproved,
}: {
  engagement: EngagementResponse
  onApproved: () => void
}) {
  const { session } = useAuth()
  const [approving, setApproving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const withinWindow = isWithinScopeWindow(engagement)
  const canLaunchScans = engagement.isApproved && withinWindow

  const onApprove = async () => {
    setApproving(true)
    setError(null)
    try {
      await api.post(`/api/engagements/${engagement.id}/approve`, { approvedBy: session?.email ?? 'operator' })
      onApproved()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to approve engagement.')
    } finally {
      setApproving(false)
    }
  }

  return (
    <Panel>
      <PanelHeader
        title="Authorization & scope"
        description="Every scan launch is gated on this — an engagement that isn't approved or outside its scope window rejects any scan request with 403."
        actions={
          !engagement.isApproved ? (
            <Button variant="primary" onClick={onApprove} loading={approving}>
              <Icon name="check" /> Approve engagement
            </Button>
          ) : undefined
        }
      />
      <PanelBody className="flex flex-col gap-4">
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          <StatusStat
            label="Approval"
            value={engagement.isApproved ? 'Approved' : 'Pending'}
            tone={engagement.isApproved ? 'good' : 'warn'}
          />
          <StatusStat
            label="Scope window"
            value={withinWindow ? 'Active' : 'Outside window'}
            tone={withinWindow ? 'good' : 'warn'}
          />
          <StatusStat
            label="Scope start"
            value={new Date(engagement.scopeStart).toLocaleDateString()}
          />
          <StatusStat label="Scope end" value={new Date(engagement.scopeEnd).toLocaleDateString()} />
        </div>

        {engagement.isApproved && (
          <p className="text-xs text-tertiary">
            Approved by {engagement.approvedBy} on{' '}
            {engagement.approvedAt && new Date(engagement.approvedAt).toLocaleString()}
          </p>
        )}

        {!canLaunchScans && (
          <div className="rounded-md border border-sev-medium/30 bg-sev-medium-bg px-3 py-2.5 text-sm text-sev-medium">
            {!engagement.isApproved
              ? 'Scans cannot be launched until this engagement is approved.'
              : "Scans cannot be launched — the current date is outside this engagement's scope window."}
          </div>
        )}

        {engagement.allowedScopeRules.length > 0 && (
          <div>
            <p className="mb-1.5 text-md font-medium text-secondary">Allowed scope rules</p>
            <div className="flex flex-wrap gap-1.5">
              {engagement.allowedScopeRules.map((rule) => (
                <span key={rule} className="rounded-full bg-sunken px-2 py-0.5 font-mono text-xs text-secondary">
                  {rule}
                </span>
              ))}
            </div>
          </div>
        )}

        {engagement.rulesOfEngagement && (
          <div>
            <p className="mb-1 text-md font-medium text-secondary">Rules of engagement</p>
            <p className="whitespace-pre-wrap text-sm text-secondary">{engagement.rulesOfEngagement}</p>
          </div>
        )}

        {engagement.authorizationEvidenceUri && (
          <a
            href={engagement.authorizationEvidenceUri}
            target="_blank"
            rel="noreferrer"
            className="inline-flex w-fit items-center gap-1.5 text-sm text-flare hover:text-flare-hover"
          >
            <Icon name="external-link" className="h-3.5 w-3.5" /> Authorization evidence
          </a>
        )}

        {error && <p className="text-sm text-sev-critical">{error}</p>}
      </PanelBody>
    </Panel>
  )
}

function StatusStat({ label, value, tone }: { label: string; value: string; tone?: 'good' | 'warn' }) {
  const toneClass = tone === 'good' ? 'text-status-completed' : tone === 'warn' ? 'text-sev-medium' : 'text-primary'
  return (
    <div>
      <p className="text-xs uppercase tracking-[var(--ss-tracking-wide)] text-tertiary">{label}</p>
      <p className={`text-md font-medium ${toneClass}`}>{value}</p>
    </div>
  )
}
