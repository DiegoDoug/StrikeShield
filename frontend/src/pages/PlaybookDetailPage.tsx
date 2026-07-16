import { useParams } from 'react-router-dom'
import { useApi } from '@/lib/useApi'
import type { PlaybookResponse } from '@/types/api'
import { PageHeader, LoadingBlock, ErrorBlock, Breadcrumb } from '@/components/ui/misc'
import { Panel, PanelBody, PanelHeader } from '@/components/ui/Panel'
import { DagView } from '@/components/ui/DagView'

export function PlaybookDetailPage() {
  const { playbookId } = useParams<{ playbookId: string }>()
  const { data: playbook, loading, error } = useApi<PlaybookResponse>(
    playbookId ? `/api/playbooks/${playbookId}` : null,
  )

  if (loading) return <LoadingBlock />
  if (error) return <ErrorBlock message={error} />
  if (!playbook) return null

  return (
    <div>
      <PageHeader
        breadcrumb={<Breadcrumb items={[{ label: 'Playbooks', href: '/playbooks' }, { label: playbook.name }]} />}
        title={playbook.name}
        description={playbook.description ?? undefined}
      />

      <Panel>
        <PanelHeader title="Step graph" description="Steps run in dependency order; OnSuccess steps skip if a dependency didn't complete." />
        <PanelBody>
          <DagView
            nodes={playbook.steps.map((s) => ({ ...s, key: s.stepKey }))}
            renderNode={(step) => (
              <div className="w-56 rounded-md border border-hairline bg-sunken/50 p-3">
                <div className="flex items-center justify-between gap-2">
                  <span className="font-mono text-sm font-medium text-primary">{step.stepKey}</span>
                  <span
                    className={`rounded-full px-1.5 py-0.5 text-[0.65rem] font-medium ${
                      step.condition === 'Always' ? 'bg-sev-info-bg text-sev-info' : 'bg-sunken text-tertiary'
                    }`}
                  >
                    {step.condition}
                  </span>
                </div>
                <p className="mt-1 text-xs text-secondary">{step.toolName}</p>
                <p className="mt-1 truncate text-[0.65rem] text-tertiary" title={`${step.imageRepository}:${step.imageTag}`}>
                  {step.imageRepository}:{step.imageTag}
                </p>
                <p className="mt-1 text-[0.65rem] text-tertiary">Timeout {step.timeoutSeconds}s</p>
              </div>
            )}
          />
        </PanelBody>
      </Panel>
    </div>
  )
}
