import type { StepRunResponse } from '@/types/api'
import { DagView } from '@/components/ui/DagView'
import { StepStatusBadge } from '@/components/ui/Badge'

const cardTone: Record<StepRunResponse['status'], string> = {
  Pending: 'border-hairline bg-sunken/50',
  Running: 'border-flare/40 bg-flare-muted',
  Completed: 'border-status-completed/30 bg-status-completed/10',
  Failed: 'border-status-failed/40 bg-status-failed/10',
  TimedOut: 'border-status-timedout/40 bg-status-timedout/10',
  Skipped: 'border-hairline bg-sunken/30',
}

export function StepsGraph({ steps, onSelectStep }: { steps: StepRunResponse[]; onSelectStep: (step: StepRunResponse) => void }) {
  return (
    <DagView
      nodes={steps.map((s) => ({ ...s, key: s.stepKey }))}
      renderNode={(step) => (
        <button
          type="button"
          onClick={() => onSelectStep(step)}
          className={`w-56 rounded-md border p-3 text-left transition-colors duration-[var(--ss-duration-fast)] hover:border-border-strong ${cardTone[step.status]}`}
        >
          <div className="flex items-center justify-between gap-2">
            <span className="font-mono text-sm font-medium text-primary">{step.stepKey}</span>
            <StepStatusBadge status={step.status} />
          </div>
          <p className="mt-1 text-xs text-secondary">{step.toolName}</p>
          {step.exitCode !== null && (
            <p className="mt-1 text-[0.65rem] text-tertiary">exit {step.exitCode}</p>
          )}
          {step.artifacts.length > 0 && (
            <p className="mt-1 text-[0.65rem] text-tertiary">
              {step.artifacts.length} artifact{step.artifacts.length === 1 ? '' : 's'}
            </p>
          )}
        </button>
      )}
    />
  )
}
