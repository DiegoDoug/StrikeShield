import { useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useApi } from '@/lib/useApi'
import { api, ApiError } from '@/lib/api'
import type { ClientResponse, EngagementResponse, ProjectResponse, TargetResponse, TargetType } from '@/types/api'
import { PageHeader, EmptyState, LoadingBlock, ErrorBlock, Breadcrumb } from '@/components/ui/misc'
import { Panel } from '@/components/ui/Panel'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { FormField, Input, Select, Textarea } from '@/components/ui/Field'
import { Icon } from '@/components/ui/Icon'
import { Tabs } from '@/components/ui/Tabs'

const targetTypes: TargetType[] = ['Url', 'Domain', 'IpAddress', 'Repository', 'LocalPath']

export function ProjectDetailPage() {
  const { projectId } = useParams<{ projectId: string }>()
  const navigate = useNavigate()
  const [tab, setTab] = useState<'targets' | 'engagements'>('targets')

  const { data: project, error: projectError } = useApi<ProjectResponse>(
    projectId ? `/api/projects/${projectId}` : null,
  )
  const { data: client } = useApi<ClientResponse>(project ? `/api/clients/${project.clientId}` : null, [project?.clientId])
  const {
    data: targets,
    loading: targetsLoading,
    error: targetsError,
    refetch: refetchTargets,
  } = useApi<TargetResponse[]>(projectId ? `/api/targets?projectId=${projectId}` : null)
  const {
    data: engagements,
    loading: engagementsLoading,
    error: engagementsError,
    refetch: refetchEngagements,
  } = useApi<EngagementResponse[]>(projectId ? `/api/engagements?projectId=${projectId}` : null)

  const [targetDialogOpen, setTargetDialogOpen] = useState(false)
  const [engagementDialogOpen, setEngagementDialogOpen] = useState(false)

  const onDeleteTarget = async (target: TargetResponse) => {
    if (!confirm(`Delete target "${target.value}"?`)) return
    try {
      await api.delete(`/api/targets/${target.id}`)
      refetchTargets()
    } catch (err) {
      alert(err instanceof ApiError ? err.message : 'Failed to delete target.')
    }
  }

  return (
    <div>
      <PageHeader
        breadcrumb={
          <Breadcrumb
            items={[
              { label: 'Clients', href: '/clients' },
              ...(client ? [{ label: client.name, href: `/clients/${client.id}` }] : []),
              { label: project?.name ?? '…' },
            ]}
          />
        }
        title={project?.name ?? 'Project'}
        description="Targets are what you scan; engagements are the authorized, time-boxed window you're allowed to scan them in."
        actions={
          tab === 'targets' ? (
            <Button variant="primary" onClick={() => setTargetDialogOpen(true)}>
              <Icon name="plus" /> New target
            </Button>
          ) : (
            <Button variant="primary" onClick={() => setEngagementDialogOpen(true)}>
              <Icon name="plus" /> New engagement
            </Button>
          )
        }
      />

      {projectError && <ErrorBlock message={projectError} />}

      <Tabs
        active={tab}
        onChange={setTab}
        tabs={[
          { value: 'targets', label: 'Targets', count: targets?.length },
          { value: 'engagements', label: 'Engagements', count: engagements?.length },
        ]}
      />

      <div className="pt-5">
        {tab === 'targets' && (
          <>
            {targetsLoading && <LoadingBlock />}
            {targetsError && <ErrorBlock message={targetsError} />}
            {targets && targets.length === 0 && (
              <Panel>
                <EmptyState
                  title="No targets yet"
                  description="Add a URL, domain, IP, repository, or local path to scan."
                  action={
                    <Button variant="primary" onClick={() => setTargetDialogOpen(true)}>
                      <Icon name="plus" /> New target
                    </Button>
                  }
                />
              </Panel>
            )}
            {targets && targets.length > 0 && (
              <Panel>
                <ul className="divide-y divide-hairline">
                  {targets.map((target) => (
                    <li key={target.id} className="flex items-center justify-between gap-4 px-5 py-4">
                      <div>
                        <p className="text-md font-medium text-primary">{target.value}</p>
                        <p className="text-xs text-tertiary">{target.type}</p>
                      </div>
                      <button
                        type="button"
                        onClick={() => onDeleteTarget(target)}
                        aria-label="Delete target"
                        className="rounded-sm p-1.5 text-tertiary hover:bg-sev-critical-bg hover:text-sev-critical"
                      >
                        <Icon name="trash" className="h-4 w-4" />
                      </button>
                    </li>
                  ))}
                </ul>
              </Panel>
            )}
          </>
        )}

        {tab === 'engagements' && (
          <>
            {engagementsLoading && <LoadingBlock />}
            {engagementsError && <ErrorBlock message={engagementsError} />}
            {engagements && engagements.length === 0 && (
              <Panel>
                <EmptyState
                  title="No engagements yet"
                  description="An engagement is the authorized, scoped window that gates every scan — nothing can run without one."
                  action={
                    <Button variant="primary" onClick={() => setEngagementDialogOpen(true)}>
                      <Icon name="plus" /> New engagement
                    </Button>
                  }
                />
              </Panel>
            )}
            {engagements && engagements.length > 0 && (
              <Panel>
                <ul className="divide-y divide-hairline">
                  {engagements.map((engagement) => (
                    <li key={engagement.id}>
                      <button
                        type="button"
                        onClick={() => navigate(`/engagements/${engagement.id}`)}
                        className="flex w-full items-center justify-between gap-4 px-5 py-4 text-left transition-colors duration-[var(--ss-duration-fast)] hover:bg-elevated"
                      >
                        <div>
                          <p className="text-md font-medium text-primary">{engagement.name}</p>
                          <p className="text-xs text-tertiary">
                            {new Date(engagement.scopeStart).toLocaleDateString()} –{' '}
                            {new Date(engagement.scopeEnd).toLocaleDateString()}
                          </p>
                        </div>
                        <span
                          className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                            engagement.isApproved
                              ? 'bg-status-completed/15 text-status-completed'
                              : 'bg-sunken text-tertiary'
                          }`}
                        >
                          {engagement.isApproved ? 'Approved' : 'Pending approval'}
                        </span>
                      </button>
                    </li>
                  ))}
                </ul>
              </Panel>
            )}
          </>
        )}
      </div>

      {projectId && (
        <>
          <CreateTargetDialog
            open={targetDialogOpen}
            onClose={() => setTargetDialogOpen(false)}
            projectId={projectId}
            onCreated={() => {
              setTargetDialogOpen(false)
              refetchTargets()
            }}
          />
          <CreateEngagementDialog
            open={engagementDialogOpen}
            onClose={() => setEngagementDialogOpen(false)}
            projectId={projectId}
            onCreated={() => {
              setEngagementDialogOpen(false)
              refetchEngagements()
            }}
          />
        </>
      )}
    </div>
  )
}

function CreateTargetDialog({
  open,
  onClose,
  projectId,
  onCreated,
}: {
  open: boolean
  onClose: () => void
  projectId: string
  onCreated: () => void
}) {
  const [type, setType] = useState<TargetType>('Url')
  const [value, setValue] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await api.post('/api/targets', { projectId, type, value })
      setValue('')
      onCreated()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to create target.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="New target"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} type="button">
            Cancel
          </Button>
          <Button variant="primary" type="submit" form="create-target-form" loading={submitting}>
            Create
          </Button>
        </>
      }
    >
      <form id="create-target-form" onSubmit={onSubmit} className="flex flex-col gap-4">
        <FormField label="Type" htmlFor="target-type">
          <Select id="target-type" value={type} onChange={(e) => setType(e.target.value as TargetType)}>
            {targetTypes.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label="Value" htmlFor="target-value" hint="e.g. https://juice-shop:3000, example.com, 10.0.0.5">
          <Input id="target-value" required autoFocus value={value} onChange={(e) => setValue(e.target.value)} />
        </FormField>
        {error && <p className="text-sm text-sev-critical">{error}</p>}
      </form>
    </Dialog>
  )
}

function CreateEngagementDialog({
  open,
  onClose,
  projectId,
  onCreated,
}: {
  open: boolean
  onClose: () => void
  projectId: string
  onCreated: () => void
}) {
  const [name, setName] = useState('')
  const [rulesOfEngagement, setRulesOfEngagement] = useState('')
  const [authorizationEvidenceUri, setAuthorizationEvidenceUri] = useState('')
  const today = new Date().toISOString().slice(0, 10)
  const inThirtyDays = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10)
  const [scopeStart, setScopeStart] = useState(today)
  const [scopeEnd, setScopeEnd] = useState(inThirtyDays)
  const [allowedScopeRules, setAllowedScopeRules] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await api.post('/api/engagements', {
        projectId,
        name,
        rulesOfEngagement: rulesOfEngagement || null,
        authorizationEvidenceUri: authorizationEvidenceUri || null,
        scopeStart: new Date(scopeStart).toISOString(),
        scopeEnd: new Date(scopeEnd).toISOString(),
        allowedScopeRules: allowedScopeRules
          .split(',')
          .map((r) => r.trim())
          .filter(Boolean),
      })
      setName('')
      onCreated()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to create engagement.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="New engagement"
      widthClassName="max-w-lg"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} type="button">
            Cancel
          </Button>
          <Button variant="primary" type="submit" form="create-engagement-form" loading={submitting}>
            Create
          </Button>
        </>
      }
    >
      <form id="create-engagement-form" onSubmit={onSubmit} className="flex flex-col gap-4">
        <FormField label="Name" htmlFor="engagement-name">
          <Input id="engagement-name" required autoFocus value={name} onChange={(e) => setName(e.target.value)} />
        </FormField>
        <div className="grid grid-cols-2 gap-4">
          <FormField label="Scope start" htmlFor="scope-start">
            <Input
              id="scope-start"
              type="date"
              required
              value={scopeStart}
              onChange={(e) => setScopeStart(e.target.value)}
            />
          </FormField>
          <FormField label="Scope end" htmlFor="scope-end">
            <Input id="scope-end" type="date" required value={scopeEnd} onChange={(e) => setScopeEnd(e.target.value)} />
          </FormField>
        </div>
        <FormField label="Rules of engagement" htmlFor="roe" hint="Optional — link or free text.">
          <Textarea id="roe" value={rulesOfEngagement} onChange={(e) => setRulesOfEngagement(e.target.value)} />
        </FormField>
        <FormField label="Authorization evidence URI" htmlFor="auth-uri" hint="Optional — link to signed authorization.">
          <Input id="auth-uri" value={authorizationEvidenceUri} onChange={(e) => setAuthorizationEvidenceUri(e.target.value)} />
        </FormField>
        <FormField label="Allowed scope rules" htmlFor="scope-rules" hint="Comma-separated, e.g. *.example.com, 10.0.0.0/24">
          <Input id="scope-rules" value={allowedScopeRules} onChange={(e) => setAllowedScopeRules(e.target.value)} />
        </FormField>
        {error && <p className="text-sm text-sev-critical">{error}</p>}
      </form>
    </Dialog>
  )
}
