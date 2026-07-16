import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

export function Spinner({ className = '' }: { className?: string }) {
  return (
    <span
      className={`inline-block h-4 w-4 animate-spin rounded-full border-2 border-hairline border-t-flare ${className}`}
    />
  )
}

export function LoadingBlock({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-12 text-sm text-secondary">
      <Spinner />
      {label}
    </div>
  )
}

export function ErrorBlock({ message }: { message: string }) {
  return (
    <div className="rounded-md border border-sev-critical/30 bg-sev-critical-bg px-4 py-3 text-sm text-sev-critical">
      {message}
    </div>
  )
}

export function EmptyState({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-3 px-4 py-12 text-center">
      <p className="text-md font-medium text-primary">{title}</p>
      {description && <p className="max-w-sm text-sm text-secondary">{description}</p>}
      {action}
    </div>
  )
}

export function PageHeader({
  title,
  description,
  actions,
  breadcrumb,
}: {
  title: ReactNode
  description?: ReactNode
  actions?: ReactNode
  breadcrumb?: ReactNode
}) {
  return (
    <div className="flex flex-col gap-3 pb-6 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex flex-col gap-1.5">
        {breadcrumb}
        <h1 className="text-xl font-medium text-primary tracking-[var(--ss-tracking-tight)]">{title}</h1>
        {description && <p className="text-sm text-secondary">{description}</p>}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  )
}

export function Breadcrumb({ items }: { items: { label: string; href?: string }[] }) {
  return (
    <nav className="flex items-center gap-1.5 text-xs text-tertiary">
      {items.map((item, i) => (
        <span key={i} className="flex items-center gap-1.5">
          {i > 0 && <span aria-hidden>/</span>}
          {item.href ? (
            <Link to={item.href} className="hover:text-secondary">
              {item.label}
            </Link>
          ) : (
            <span>{item.label}</span>
          )}
        </span>
      ))}
    </nav>
  )
}
