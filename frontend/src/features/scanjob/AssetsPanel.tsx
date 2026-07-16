import { useApi } from '@/lib/useApi'
import type { AssetResponse } from '@/types/api'
import { Panel, PanelBody, PanelHeader } from '@/components/ui/Panel'
import { LoadingBlock } from '@/components/ui/misc'

export function AssetsPanel({ scanJobId }: { scanJobId: string }) {
  const { data: assets, loading } = useApi<AssetResponse[]>(`/api/scan-jobs/${scanJobId}/assets`)

  if (loading) return <LoadingBlock />
  if (!assets || assets.length === 0) return null

  const grouped = assets.reduce<Record<string, AssetResponse[]>>((acc, asset) => {
    ;(acc[asset.type] ??= []).push(asset)
    return acc
  }, {})

  return (
    <Panel>
      <PanelHeader title="Discovered assets" description="Everything a recon step found — hosts, ports, URLs, subdomains, fingerprints." />
      <PanelBody className="flex flex-col gap-4">
        {Object.entries(grouped).map(([type, items]) => (
          <div key={type}>
            <p className="mb-1.5 text-xs font-medium uppercase tracking-[var(--ss-tracking-wide)] text-tertiary">
              {type} ({items.length})
            </p>
            <div className="flex flex-wrap gap-1.5">
              {items.map((asset) => (
                <span
                  key={asset.id}
                  className="rounded-full bg-sunken px-2 py-0.5 font-mono text-xs text-secondary"
                  title={asset.metadata ?? undefined}
                >
                  {asset.value}
                </span>
              ))}
            </div>
          </div>
        ))}
      </PanelBody>
    </Panel>
  )
}
