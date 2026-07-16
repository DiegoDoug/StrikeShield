import type { HTMLAttributes, ReactNode } from 'react'

export function Panel({ className = '', children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={`rounded-md border border-hairline bg-surface shadow-ss-sm ${className}`}
      {...props}
    >
      {children}
    </div>
  )
}

export function PanelHeader({
  title,
  description,
  actions,
}: {
  title: ReactNode
  description?: ReactNode
  actions?: ReactNode
}) {
  return (
    <div className="flex items-start justify-between gap-4 border-b border-hairline px-5 py-4">
      <div className="flex flex-col gap-1">
        <h2 className="text-lg font-medium text-primary tracking-[var(--ss-tracking-tight)]">{title}</h2>
        {description && <p className="text-sm text-secondary">{description}</p>}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  )
}

export function PanelBody({ className = '', children }: { className?: string; children: ReactNode }) {
  return <div className={`px-5 py-4 ${className}`}>{children}</div>
}
