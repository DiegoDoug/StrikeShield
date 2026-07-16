import type { FindingSeverity, FindingStatus, ScanJobStatus, StepRunStatus } from '@/types/api'

// Severity is always icon + label, never color alone (tokens.css comment on
// the severity scale) — each level gets a distinct glyph shape, not just a
// distinct color, so it still reads correctly for colorblind users.
const severityConfig: Record<FindingSeverity, { label: string; text: string; bg: string; glyph: string }> = {
  Critical: { label: 'Critical', text: 'text-sev-critical', bg: 'bg-sev-critical-bg', glyph: '▲' },
  High: { label: 'High', text: 'text-sev-high', bg: 'bg-sev-high-bg', glyph: '◆' },
  Medium: { label: 'Medium', text: 'text-sev-medium', bg: 'bg-sev-medium-bg', glyph: '■' },
  Low: { label: 'Low', text: 'text-sev-low', bg: 'bg-sev-low-bg', glyph: '●' },
  Info: { label: 'Info', text: 'text-sev-info', bg: 'bg-sev-info-bg', glyph: 'ⓘ' },
}

export function SeverityBadge({ severity }: { severity: FindingSeverity }) {
  const cfg = severityConfig[severity]
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium ${cfg.text} ${cfg.bg}`}
    >
      <span aria-hidden className="text-[0.65rem] leading-none">
        {cfg.glyph}
      </span>
      {cfg.label}
    </span>
  )
}

const scanJobStatusConfig: Record<ScanJobStatus, { label: string; dot: string; live?: boolean }> = {
  Queued: { label: 'Queued', dot: 'bg-status-queued' },
  Running: { label: 'Running', dot: 'bg-status-running', live: true },
  Completed: { label: 'Completed', dot: 'bg-status-completed' },
  Failed: { label: 'Failed', dot: 'bg-status-failed' },
  TimedOut: { label: 'Timed out', dot: 'bg-status-timedout' },
  AwaitingApproval: { label: 'Awaiting approval', dot: 'bg-flare', live: true },
}

export function ScanJobStatusBadge({ status }: { status: ScanJobStatus }) {
  const cfg = scanJobStatusConfig[status]
  return (
    <span className="inline-flex items-center gap-1.5 text-sm font-medium text-secondary">
      <span className={`h-1.5 w-1.5 rounded-full ${cfg.dot} ${cfg.live ? 'ss-live-dot' : ''}`} />
      {cfg.label}
    </span>
  )
}

const stepStatusConfig: Record<StepRunStatus, { label: string; dot: string; live?: boolean }> = {
  Pending: { label: 'Pending', dot: 'bg-status-queued' },
  Running: { label: 'Running', dot: 'bg-status-running', live: true },
  Completed: { label: 'Completed', dot: 'bg-status-completed' },
  Failed: { label: 'Failed', dot: 'bg-status-failed' },
  TimedOut: { label: 'Timed out', dot: 'bg-status-timedout' },
  Skipped: { label: 'Skipped', dot: 'bg-border-strong' },
}

export function StepStatusBadge({ status }: { status: StepRunStatus }) {
  const cfg = stepStatusConfig[status]
  return (
    <span className="inline-flex items-center gap-1.5 text-xs font-medium text-secondary">
      <span className={`h-1.5 w-1.5 rounded-full ${cfg.dot} ${cfg.live ? 'ss-live-dot' : ''}`} />
      {cfg.label}
    </span>
  )
}

const findingStatusConfig: Record<FindingStatus, { label: string }> = {
  New: { label: 'New' },
  Confirmed: { label: 'Confirmed' },
  FalsePositive: { label: 'False positive' },
  Fixed: { label: 'Fixed' },
  AcceptedRisk: { label: 'Accepted risk' },
  Regressed: { label: 'Regressed' },
}

export function FindingStatusLabel({ status }: { status: FindingStatus }) {
  return findingStatusConfig[status].label
}
