import { useNavigate } from 'react-router-dom'
import { useApi } from '@/lib/useApi'
import type { PlaybookResponse } from '@/types/api'
import { PageHeader, LoadingBlock, ErrorBlock } from '@/components/ui/misc'
import { Panel } from '@/components/ui/Panel'
import { Icon } from '@/components/ui/Icon'

export function PlaybooksPage() {
  const navigate = useNavigate()
  const { data: playbooks, loading, error } = useApi<PlaybookResponse[]>('/api/playbooks')

  return (
    <div>
      <PageHeader
        title="Playbooks"
        description="Seeded playbook templates — each step runs in its own isolated container, wired as a dependency DAG."
      />
      {loading && <LoadingBlock />}
      {error && <ErrorBlock message={error} />}
      {playbooks && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {playbooks.map((playbook) => (
            <button
              key={playbook.id}
              type="button"
              onClick={() => navigate(`/playbooks/${playbook.id}`)}
              className="text-left"
            >
              <Panel className="flex h-full flex-col gap-3 p-5 transition-colors duration-[var(--ss-duration-fast)] hover:border-border-strong">
                <div className="flex items-center justify-between">
                  <span className="rounded-full bg-sunken px-2 py-0.5 text-xs font-mono text-secondary">
                    {playbook.slug}
                  </span>
                  <Icon name="chevron-right" className="h-4 w-4 text-tertiary" />
                </div>
                <p className="text-md font-medium text-primary">{playbook.name}</p>
                {playbook.description && <p className="text-sm text-secondary">{playbook.description}</p>}
                <p className="mt-auto text-xs text-tertiary">
                  {playbook.steps.length} step{playbook.steps.length === 1 ? '' : 's'} ·{' '}
                  {playbook.steps.map((s) => s.toolName).join(', ')}
                </p>
              </Panel>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
