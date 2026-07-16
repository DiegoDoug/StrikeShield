import { useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useApi } from '@/lib/useApi'
import { api, ApiError } from '@/lib/api'
import type { ClientResponse, ProjectResponse } from '@/types/api'
import { PageHeader, EmptyState, LoadingBlock, ErrorBlock, Breadcrumb } from '@/components/ui/misc'
import { Panel } from '@/components/ui/Panel'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { FormField, Input } from '@/components/ui/Field'
import { Icon } from '@/components/ui/Icon'

export function ClientDetailPage() {
  const { clientId } = useParams<{ clientId: string }>()
  const navigate = useNavigate()
  const { data: client, error: clientError } = useApi<ClientResponse>(clientId ? `/api/clients/${clientId}` : null)
  const {
    data: projects,
    loading,
    error,
    refetch,
  } = useApi<ProjectResponse[]>(clientId ? `/api/projects?clientId=${clientId}` : null)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const onDeleteClient = async () => {
    if (!clientId || !confirm(`Delete client "${client?.name}"? This cannot be undone.`)) return
    setDeleting(true)
    try {
      await api.delete(`/api/clients/${clientId}`)
      navigate('/clients')
    } catch (err) {
      alert(err instanceof ApiError ? err.message : 'Failed to delete client.')
    } finally {
      setDeleting(false)
    }
  }

  return (
    <div>
      <PageHeader
        breadcrumb={<Breadcrumb items={[{ label: 'Clients', href: '/clients' }, { label: client?.name ?? '…' }]} />}
        title={client?.name ?? 'Client'}
        description="Projects group the targets and engagements you run on behalf of this client."
        actions={
          <>
            <Button variant="danger" onClick={onDeleteClient} loading={deleting}>
              <Icon name="trash" /> Delete client
            </Button>
            <Button variant="primary" onClick={() => setDialogOpen(true)}>
              <Icon name="plus" /> New project
            </Button>
          </>
        }
      />

      {clientError && <ErrorBlock message={clientError} />}
      {loading && <LoadingBlock />}
      {error && <ErrorBlock message={error} />}

      {projects && projects.length === 0 && (
        <Panel>
          <EmptyState
            title="No projects yet"
            description="Create a project to add targets and open engagements for this client."
            action={
              <Button variant="primary" onClick={() => setDialogOpen(true)}>
                <Icon name="plus" /> New project
              </Button>
            }
          />
        </Panel>
      )}

      {projects && projects.length > 0 && (
        <Panel>
          <ul className="divide-y divide-hairline">
            {projects.map((project) => (
              <li key={project.id}>
                <button
                  type="button"
                  onClick={() => navigate(`/projects/${project.id}`)}
                  className="flex w-full items-center justify-between gap-4 px-5 py-4 text-left transition-colors duration-[var(--ss-duration-fast)] hover:bg-elevated"
                >
                  <div>
                    <p className="text-md font-medium text-primary">{project.name}</p>
                    <p className="text-xs text-tertiary">
                      Created {new Date(project.createdAt).toLocaleDateString()}
                    </p>
                  </div>
                  <Icon name="chevron-right" className="h-4 w-4 text-tertiary" />
                </button>
              </li>
            ))}
          </ul>
        </Panel>
      )}

      {clientId && (
        <CreateProjectDialog
          open={dialogOpen}
          onClose={() => setDialogOpen(false)}
          clientId={clientId}
          onCreated={() => {
            setDialogOpen(false)
            refetch()
          }}
        />
      )}
    </div>
  )
}

function CreateProjectDialog({
  open,
  onClose,
  clientId,
  onCreated,
}: {
  open: boolean
  onClose: () => void
  clientId: string
  onCreated: () => void
}) {
  const [name, setName] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await api.post('/api/projects', { clientId, name })
      setName('')
      onCreated()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to create project.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="New project"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} type="button">
            Cancel
          </Button>
          <Button variant="primary" type="submit" form="create-project-form" loading={submitting}>
            Create
          </Button>
        </>
      }
    >
      <form id="create-project-form" onSubmit={onSubmit} className="flex flex-col gap-4">
        <FormField label="Name" htmlFor="project-name">
          <Input id="project-name" required autoFocus value={name} onChange={(e) => setName(e.target.value)} />
        </FormField>
        {error && <p className="text-sm text-sev-critical">{error}</p>}
      </form>
    </Dialog>
  )
}
