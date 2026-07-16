import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useApi } from '@/lib/useApi'
import { api, ApiError } from '@/lib/api'
import type { ClientResponse, OrganizationResponse } from '@/types/api'
import { PageHeader, EmptyState, LoadingBlock, ErrorBlock } from '@/components/ui/misc'
import { Panel } from '@/components/ui/Panel'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { FormField, Input } from '@/components/ui/Field'
import { Icon } from '@/components/ui/Icon'

export function ClientsPage() {
  const navigate = useNavigate()
  const { data: clients, loading, error, refetch } = useApi<ClientResponse[]>('/api/clients')
  const { data: organizations } = useApi<OrganizationResponse[]>('/api/organizations')
  const [dialogOpen, setDialogOpen] = useState(false)

  return (
    <div>
      <PageHeader
        title="Clients"
        description="Every engagement starts with a client — the organization you're testing on behalf of."
        actions={
          <Button variant="primary" onClick={() => setDialogOpen(true)}>
            <Icon name="plus" /> New client
          </Button>
        }
      />

      {loading && <LoadingBlock />}
      {error && <ErrorBlock message={error} />}

      {clients && clients.length === 0 && (
        <Panel>
          <EmptyState
            title="No clients yet"
            description="Create your first client to start scoping projects, targets, and engagements."
            action={
              <Button variant="primary" onClick={() => setDialogOpen(true)}>
                <Icon name="plus" /> New client
              </Button>
            }
          />
        </Panel>
      )}

      {clients && clients.length > 0 && (
        <Panel>
          <ul className="divide-y divide-hairline">
            {clients.map((client) => (
              <li key={client.id}>
                <button
                  type="button"
                  onClick={() => navigate(`/clients/${client.id}`)}
                  className="flex w-full items-center justify-between gap-4 px-5 py-4 text-left transition-colors duration-[var(--ss-duration-fast)] hover:bg-elevated"
                >
                  <div>
                    <p className="text-md font-medium text-primary">{client.name}</p>
                    <p className="text-xs text-tertiary">
                      Created {new Date(client.createdAt).toLocaleDateString()}
                    </p>
                  </div>
                  <Icon name="chevron-right" className="h-4 w-4 text-tertiary" />
                </button>
              </li>
            ))}
          </ul>
        </Panel>
      )}

      <CreateClientDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        organizationId={organizations?.[0]?.id}
        onCreated={() => {
          setDialogOpen(false)
          refetch()
        }}
      />
    </div>
  )
}

function CreateClientDialog({
  open,
  onClose,
  organizationId,
  onCreated,
}: {
  open: boolean
  onClose: () => void
  organizationId: string | undefined
  onCreated: () => void
}) {
  const [name, setName] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!organizationId) {
      setError('No organization available yet.')
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      await api.post('/api/clients', { organizationId, name })
      setName('')
      onCreated()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to create client.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="New client"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} type="button">
            Cancel
          </Button>
          <Button variant="primary" type="submit" form="create-client-form" loading={submitting}>
            Create
          </Button>
        </>
      }
    >
      <form id="create-client-form" onSubmit={onSubmit} className="flex flex-col gap-4">
        <FormField label="Name" htmlFor="client-name">
          <Input id="client-name" required autoFocus value={name} onChange={(e) => setName(e.target.value)} />
        </FormField>
        {error && <p className="text-sm text-sev-critical">{error}</p>}
      </form>
    </Dialog>
  )
}
