import { useState } from 'react'
import { ApiError, downloadFile } from '@/lib/api'
import type { EngagementResponse, ReportTypeSlug } from '@/types/api'
import { Panel, PanelBody, PanelHeader } from '@/components/ui/Panel'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'

const reportTypes: { slug: ReportTypeSlug; label: string; description: string }[] = [
  { slug: 'executive', label: 'Executive', description: 'Grouped by severity, no jargon, no CVSS/PoC.' },
  { slug: 'technical', label: 'Technical', description: 'Full detail — CVSS, CWE/CVE, repro steps, PoC.' },
  {
    slug: 'dev-remediation',
    label: 'Developer remediation',
    description: 'Grouped by affected asset/component, fix-focused.',
  },
  { slug: 'compliance', label: 'Compliance', description: 'OWASP/CWE matrix + MITRE ATT&CK coverage table.' },
]

export function ReportsTab({ engagement }: { engagement: EngagementResponse }) {
  const [pending, setPending] = useState<ReportTypeSlug | null>(null)
  const [error, setError] = useState<string | null>(null)

  const onGenerate = async (slug: ReportTypeSlug) => {
    setPending(slug)
    setError(null)
    try {
      await downloadFile(`/api/engagements/${engagement.id}/reports/${slug}`, `${slug}-report.pdf`)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : `Failed to generate the ${slug} report.`)
    } finally {
      setPending(null)
    }
  }

  return (
    <Panel>
      <PanelHeader
        title="Reports"
        description="Regenerated fresh on every download — the two narrative fields (business impact, risk rating) come from one LLM call per report; every other field is sourced directly from normalized findings."
      />
      <PanelBody>
        {error && (
          <div className="mb-4 rounded-md border border-sev-critical/30 bg-sev-critical-bg px-3 py-2.5 text-sm text-sev-critical">
            {error}
          </div>
        )}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {reportTypes.map((rt) => (
            <div
              key={rt.slug}
              className="flex flex-col justify-between gap-3 rounded-md border border-hairline bg-sunken/40 p-4"
            >
              <div>
                <p className="text-md font-medium text-primary">{rt.label}</p>
                <p className="mt-1 text-xs text-secondary">{rt.description}</p>
              </div>
              <Button
                variant="secondary"
                onClick={() => onGenerate(rt.slug)}
                loading={pending === rt.slug}
                disabled={pending !== null}
                className="self-start"
              >
                <Icon name="download" /> Generate & download
              </Button>
            </div>
          ))}
        </div>
      </PanelBody>
    </Panel>
  )
}
