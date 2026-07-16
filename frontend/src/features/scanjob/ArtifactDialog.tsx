import type { ArtifactResponse, StepRunResponse } from '@/types/api'
import { Dialog } from '@/components/ui/Dialog'

export function ArtifactDialog({
  step,
  onClose,
}: {
  step: StepRunResponse | null
  onClose: () => void
}) {
  return (
    <Dialog open={step !== null} onClose={onClose} title={step ? `${step.stepKey} — artifacts` : ''} widthClassName="max-w-2xl">
      {step && (
        <div className="flex flex-col gap-4">
          {step.errorMessage && (
            <div className="rounded-md border border-sev-critical/30 bg-sev-critical-bg px-3 py-2.5 text-sm text-sev-critical">
              {step.errorMessage}
            </div>
          )}
          {step.artifacts.length === 0 && <p className="text-sm text-secondary">No artifacts recorded for this step yet.</p>}
          {step.artifacts.map((artifact) => (
            <ArtifactBlock key={artifact.id} artifact={artifact} />
          ))}
        </div>
      )}
    </Dialog>
  )
}

function ArtifactBlock({ artifact }: { artifact: ArtifactResponse }) {
  return (
    <div>
      <div className="mb-1.5 flex items-center justify-between">
        <p className="font-mono text-xs text-secondary">{artifact.fileName}</p>
        <span className="text-[0.65rem] text-tertiary">{artifact.contentType}</span>
      </div>
      <pre className="max-h-64 overflow-auto rounded-md border border-hairline bg-sunken p-3 font-mono text-xs text-secondary whitespace-pre-wrap break-words">
        {artifact.content || '(empty)'}
      </pre>
    </div>
  )
}
