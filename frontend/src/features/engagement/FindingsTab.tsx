import { useMemo, useState } from 'react'
import { useApi } from '@/lib/useApi'
import { api, ApiError } from '@/lib/api'
import type { EngagementResponse, FindingResponse, FindingSeverity, FindingStatus } from '@/types/api'
import { Panel, PanelHeader } from '@/components/ui/Panel'
import { EmptyState, LoadingBlock, ErrorBlock } from '@/components/ui/misc'
import { SeverityBadge } from '@/components/ui/Badge'
import { Select } from '@/components/ui/Field'

const severities: FindingSeverity[] = ['Critical', 'High', 'Medium', 'Low', 'Info']
const statuses: FindingStatus[] = ['New', 'Confirmed', 'FalsePositive', 'Fixed', 'AcceptedRisk', 'Regressed']

const severityOrder: Record<FindingSeverity, number> = { Critical: 0, High: 1, Medium: 2, Low: 3, Info: 4 }

export function FindingsTab({ engagement }: { engagement: EngagementResponse }) {
  const { data, loading, error, refetch } = useApi<FindingResponse[]>(`/api/findings?engagementId=${engagement.id}`)
  const [severityFilter, setSeverityFilter] = useState<Set<FindingSeverity>>(new Set())
  const [statusFilter, setStatusFilter] = useState<FindingStatus | 'all'>('all')
  const [pendingIds, setPendingIds] = useState<Set<string>>(new Set())

  const findings = useMemo(() => {
    const list = (data ?? []).filter((f) => {
      if (severityFilter.size > 0 && !severityFilter.has(f.severity)) return false
      if (statusFilter !== 'all' && f.status !== statusFilter) return false
      return true
    })
    return list.sort((a, b) => severityOrder[a.severity] - severityOrder[b.severity])
  }, [data, severityFilter, statusFilter])

  const toggleSeverity = (severity: FindingSeverity) => {
    setSeverityFilter((prev) => {
      const next = new Set(prev)
      if (next.has(severity)) next.delete(severity)
      else next.add(severity)
      return next
    })
  }

  const onStatusChange = async (finding: FindingResponse, status: FindingStatus) => {
    setPendingIds((prev) => new Set(prev).add(finding.id))
    try {
      await api.patch(`/api/findings/${finding.id}`, { status })
      refetch()
    } catch (err) {
      alert(err instanceof ApiError ? err.message : 'Failed to update finding status.')
    } finally {
      setPendingIds((prev) => {
        const next = new Set(prev)
        next.delete(finding.id)
        return next
      })
    }
  }

  return (
    <Panel>
      <PanelHeader
        title="Findings triage"
        description="Every finding across every scan job in this engagement, deduplicated by the Correlator."
      />
      <div className="flex flex-wrap items-center gap-3 border-b border-hairline px-5 py-3">
        <div className="flex flex-wrap gap-1.5">
          {severities.map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => toggleSeverity(s)}
              className={`rounded-full border px-2 py-0.5 text-xs font-medium transition-colors duration-[var(--ss-duration-fast)] ${
                severityFilter.has(s)
                  ? 'border-flare bg-flare-muted text-flare'
                  : 'border-hairline text-tertiary hover:text-secondary'
              }`}
            >
              {s}
            </button>
          ))}
        </div>
        <div className="ml-auto flex items-center gap-2">
          <label htmlFor="status-filter" className="text-xs text-tertiary">
            Status
          </label>
          <Select
            id="status-filter"
            className="!w-auto"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as FindingStatus | 'all')}
          >
            <option value="all">All</option>
            {statuses.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </Select>
        </div>
      </div>

      {loading && <LoadingBlock />}
      {error && (
        <div className="px-5 py-4">
          <ErrorBlock message={error} />
        </div>
      )}
      {findings.length === 0 && !loading && (
        <EmptyState
          title={data && data.length > 0 ? 'No findings match these filters' : 'No findings yet'}
          description={data && data.length > 0 ? undefined : 'Findings appear here once a scan job completes.'}
        />
      )}
      {findings.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-left">
            <thead>
              <tr className="border-b border-hairline text-xs uppercase tracking-[var(--ss-tracking-wide)] text-tertiary">
                <th className="px-5 py-2 font-medium">Severity</th>
                <th className="px-2 py-2 font-medium">Finding</th>
                <th className="px-2 py-2 font-medium">Source</th>
                <th className="px-2 py-2 font-medium">Affected asset</th>
                <th className="px-2 py-2 font-medium">First seen</th>
                <th className="px-5 py-2 font-medium">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-hairline">
              {findings.map((finding) => (
                <tr key={finding.id} className="text-sm">
                  <td className="px-5 py-3">
                    <SeverityBadge severity={finding.severity} />
                  </td>
                  <td className="max-w-xs px-2 py-3">
                    <p className="truncate font-medium text-primary" title={finding.title}>
                      {finding.title}
                    </p>
                    {finding.cweIds.length > 0 && (
                      <p className="truncate text-xs text-tertiary">{finding.cweIds.join(', ')}</p>
                    )}
                  </td>
                  <td className="px-2 py-3 text-xs text-secondary">{finding.sourceTool}</td>
                  <td className="max-w-[16rem] truncate px-2 py-3 font-mono text-xs text-secondary" title={finding.affectedAsset}>
                    {finding.affectedAsset}
                  </td>
                  <td className="whitespace-nowrap px-2 py-3 text-xs text-tertiary">
                    {new Date(finding.firstSeenAt).toLocaleDateString()}
                  </td>
                  <td className="px-5 py-3">
                    <Select
                      className="!h-8 !w-auto text-xs"
                      value={finding.status}
                      disabled={pendingIds.has(finding.id)}
                      onChange={(e) => onStatusChange(finding, e.target.value as FindingStatus)}
                    >
                      {statuses.map((s) => (
                        <option key={s} value={s}>
                          {s}
                        </option>
                      ))}
                    </Select>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Panel>
  )
}
