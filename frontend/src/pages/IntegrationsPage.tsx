import { useState, type FormEvent } from 'react'
import { useApi } from '@/lib/useApi'
import { api, ApiError } from '@/lib/api'
import type { IntegrationResponse, IntegrationType, OrganizationResponse } from '@/types/api'
import { PageHeader, EmptyState, LoadingBlock, ErrorBlock } from '@/components/ui/misc'
import { Panel, PanelBody } from '@/components/ui/Panel'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { FormField, Input, Select } from '@/components/ui/Field'
import { Icon } from '@/components/ui/Icon'

const integrationTypes: IntegrationType[] = ['Slack', 'Webhook', 'GitHub']
const typeLabels: Record<IntegrationType, string> = { Slack: 'Slack', Webhook: 'Generic webhook', GitHub: 'GitHub issues' }

export function IntegrationsPage() {
  const { data: organizations } = useApi<OrganizationResponse[]>('/api/organizations')
  const organizationId = organizations?.[0]?.id
  const {
    data: integrations,
    loading,
    error,
    refetch,
  } = useApi<IntegrationResponse[]>(organizationId ? `/api/integrations?organizationId=${organizationId}` : null, [
    organizationId,
  ])
  const [dialogOpen, setDialogOpen] = useState(false)

  const onToggleEnabled = async (integration: IntegrationResponse) => {
    try {
      await api.put(`/api/integrations/${integration.id}`, {
        enabled: !integration.enabled,
        notifyOnScanCompletion: integration.notifyOnScanCompletion,
        notifyOnCriticalFinding: integration.notifyOnCriticalFinding,
        webhookUrl: integration.webhookUrl,
        gitHubRepository: integration.gitHubRepository,
        gitHubAccessToken: null,
      })
      refetch()
    } catch (err) {
      alert(err instanceof ApiError ? err.message : 'Failed to update integration.')
    }
  }

  const onToggleNotify = async (integration: IntegrationResponse, field: 'notifyOnScanCompletion' | 'notifyOnCriticalFinding') => {
    try {
      await api.put(`/api/integrations/${integration.id}`, {
        enabled: integration.enabled,
        notifyOnScanCompletion: integration.notifyOnScanCompletion,
        notifyOnCriticalFinding: integration.notifyOnCriticalFinding,
        webhookUrl: integration.webhookUrl,
        gitHubRepository: integration.gitHubRepository,
        gitHubAccessToken: null,
        [field]: !integration[field],
      })
      refetch()
    } catch (err) {
      alert(err instanceof ApiError ? err.message : 'Failed to update integration.')
    }
  }

  const onDelete = async (integration: IntegrationResponse) => {
    if (!confirm(`Remove the ${typeLabels[integration.type]} integration?`)) return
    try {
      await api.delete(`/api/integrations/${integration.id}`)
      refetch()
    } catch (err) {
      alert(err instanceof ApiError ? err.message : 'Failed to delete integration.')
    }
  }

  return (
    <div>
      <PageHeader
        title="Integrations"
        description="Notify Slack, a generic webhook, or open GitHub issues on scan completion and new critical findings."
        actions={
          <Button variant="primary" onClick={() => setDialogOpen(true)}>
            <Icon name="plus" /> New integration
          </Button>
        }
      />

      {loading && <LoadingBlock />}
      {error && <ErrorBlock message={error} />}

      {integrations && integrations.length === 0 && (
        <Panel>
          <EmptyState
            title="No integrations configured"
            description="Wire up Slack, a webhook receiver, or GitHub issue creation."
            action={
              <Button variant="primary" onClick={() => setDialogOpen(true)}>
                <Icon name="plus" /> New integration
              </Button>
            }
          />
        </Panel>
      )}

      {integrations && integrations.length > 0 && (
        <div className="flex flex-col gap-3">
          {integrations.map((integration) => (
            <Panel key={integration.id}>
              <PanelBody className="flex flex-col gap-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="text-md font-medium text-primary">{typeLabels[integration.type]}</span>
                    <button
                      type="button"
                      onClick={() => onToggleEnabled(integration)}
                      className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${
                        integration.enabled
                          ? 'bg-status-completed/15 text-status-completed'
                          : 'bg-sunken text-tertiary'
                      }`}
                    >
                      {integration.enabled ? 'Enabled' : 'Disabled'}
                    </button>
                  </div>
                  <button
                    type="button"
                    onClick={() => onDelete(integration)}
                    aria-label="Delete integration"
                    className="rounded-sm p-1.5 text-tertiary hover:bg-sev-critical-bg hover:text-sev-critical"
                  >
                    <Icon name="trash" className="h-4 w-4" />
                  </button>
                </div>

                {integration.webhookUrl && <p className="font-mono text-xs text-secondary">{integration.webhookUrl}</p>}
                {integration.gitHubRepository && (
                  <p className="font-mono text-xs text-secondary">
                    {integration.gitHubRepository} {integration.hasGitHubAccessToken ? '· token configured' : '· no token'}
                  </p>
                )}

                <div className="flex flex-wrap gap-4 text-sm text-secondary">
                  <label className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      checked={integration.notifyOnScanCompletion}
                      onChange={() => onToggleNotify(integration, 'notifyOnScanCompletion')}
                      className="accent-flare"
                    />
                    Notify on scan completion
                  </label>
                  <label className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      checked={integration.notifyOnCriticalFinding}
                      onChange={() => onToggleNotify(integration, 'notifyOnCriticalFinding')}
                      className="accent-flare"
                    />
                    Notify on new critical finding
                  </label>
                </div>
              </PanelBody>
            </Panel>
          ))}
        </div>
      )}

      {organizationId && (
        <CreateIntegrationDialog
          open={dialogOpen}
          onClose={() => setDialogOpen(false)}
          organizationId={organizationId}
          onCreated={() => {
            setDialogOpen(false)
            refetch()
          }}
        />
      )}
    </div>
  )
}

function CreateIntegrationDialog({
  open,
  onClose,
  organizationId,
  onCreated,
}: {
  open: boolean
  onClose: () => void
  organizationId: string
  onCreated: () => void
}) {
  const [type, setType] = useState<IntegrationType>('Slack')
  const [webhookUrl, setWebhookUrl] = useState('')
  const [gitHubRepository, setGitHubRepository] = useState('')
  const [gitHubAccessToken, setGitHubAccessToken] = useState('')
  const [notifyOnScanCompletion, setNotifyOnScanCompletion] = useState(true)
  const [notifyOnCriticalFinding, setNotifyOnCriticalFinding] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await api.post('/api/integrations', {
        organizationId,
        type,
        notifyOnScanCompletion,
        notifyOnCriticalFinding,
        webhookUrl: type === 'GitHub' ? null : webhookUrl || null,
        gitHubRepository: type === 'GitHub' ? gitHubRepository || null : null,
        gitHubAccessToken: type === 'GitHub' ? gitHubAccessToken || null : null,
      })
      onCreated()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to create integration.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="New integration"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} type="button">
            Cancel
          </Button>
          <Button variant="primary" type="submit" form="create-integration-form" loading={submitting}>
            Create
          </Button>
        </>
      }
    >
      <form id="create-integration-form" onSubmit={onSubmit} className="flex flex-col gap-4">
        <FormField label="Type" htmlFor="integration-type">
          <Select id="integration-type" value={type} onChange={(e) => setType(e.target.value as IntegrationType)}>
            {integrationTypes.map((t) => (
              <option key={t} value={t}>
                {typeLabels[t]}
              </option>
            ))}
          </Select>
        </FormField>

        {type !== 'GitHub' ? (
          <FormField
            label="Webhook URL"
            htmlFor="webhook-url"
            hint={type === 'Slack' ? 'Slack incoming webhook URL.' : 'Any URL that accepts a JSON POST.'}
          >
            <Input
              id="webhook-url"
              type="url"
              required
              value={webhookUrl}
              onChange={(e) => setWebhookUrl(e.target.value)}
            />
          </FormField>
        ) : (
          <>
            <FormField label="Repository" htmlFor="gh-repo" hint="owner/repo">
              <Input
                id="gh-repo"
                required
                value={gitHubRepository}
                onChange={(e) => setGitHubRepository(e.target.value)}
              />
            </FormField>
            <FormField label="Access token" htmlFor="gh-token" hint="Never echoed back once saved.">
              <Input
                id="gh-token"
                type="password"
                required
                value={gitHubAccessToken}
                onChange={(e) => setGitHubAccessToken(e.target.value)}
              />
            </FormField>
          </>
        )}

        <div className="flex flex-col gap-2 text-sm text-secondary">
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={notifyOnScanCompletion}
              onChange={(e) => setNotifyOnScanCompletion(e.target.checked)}
              className="accent-flare"
            />
            Notify on scan completion
          </label>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={notifyOnCriticalFinding}
              onChange={(e) => setNotifyOnCriticalFinding(e.target.checked)}
              className="accent-flare"
            />
            Notify on new critical finding
          </label>
        </div>

        {error && <p className="text-sm text-sev-critical">{error}</p>}
      </form>
    </Dialog>
  )
}
