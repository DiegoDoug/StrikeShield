import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { useApi } from '@/lib/useApi'
import type { EngagementResponse, ProjectResponse } from '@/types/api'
import { PageHeader, LoadingBlock, ErrorBlock, Breadcrumb } from '@/components/ui/misc'
import { Tabs } from '@/components/ui/Tabs'
import { EngagementOverview } from '@/features/engagement/EngagementOverview'
import { ScanJobsTab } from '@/features/engagement/ScanJobsTab'
import { FindingsTab } from '@/features/engagement/FindingsTab'
import { ReportsTab } from '@/features/engagement/ReportsTab'
import { SchedulesTab } from '@/features/engagement/SchedulesTab'

type TabValue = 'overview' | 'scans' | 'findings' | 'reports' | 'schedules'

export function EngagementDetailPage() {
  const { engagementId } = useParams<{ engagementId: string }>()
  const [tab, setTab] = useState<TabValue>('overview')
  const {
    data: engagement,
    loading,
    error,
    refetch,
  } = useApi<EngagementResponse>(engagementId ? `/api/engagements/${engagementId}` : null)
  const { data: project } = useApi<ProjectResponse>(
    engagement ? `/api/projects/${engagement.projectId}` : null,
    [engagement?.projectId],
  )

  if (loading) return <LoadingBlock />
  if (error) return <ErrorBlock message={error} />
  if (!engagement) return null

  return (
    <div>
      <PageHeader
        breadcrumb={
          <Breadcrumb
            items={[
              { label: 'Clients', href: '/clients' },
              ...(project ? [{ label: project.name, href: `/projects/${project.id}` }] : []),
              { label: engagement.name },
            ]}
          />
        }
        title={engagement.name}
      />

      <Tabs
        active={tab}
        onChange={setTab}
        tabs={[
          { value: 'overview', label: 'Overview' },
          { value: 'scans', label: 'Scan jobs' },
          { value: 'findings', label: 'Findings' },
          { value: 'reports', label: 'Reports' },
          { value: 'schedules', label: 'Schedules' },
        ]}
      />

      <div className="pt-5">
        {tab === 'overview' && <EngagementOverview engagement={engagement} onApproved={refetch} />}
        {tab === 'scans' && <ScanJobsTab engagement={engagement} />}
        {tab === 'findings' && <FindingsTab engagement={engagement} />}
        {tab === 'reports' && <ReportsTab engagement={engagement} />}
        {tab === 'schedules' && <SchedulesTab engagement={engagement} />}
      </div>
    </div>
  )
}
