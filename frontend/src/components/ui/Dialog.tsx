import { type ReactNode, useEffect } from 'react'
import { createPortal } from 'react-dom'

export function Dialog({
  open,
  onClose,
  title,
  children,
  footer,
  widthClassName = 'max-w-md',
}: {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
  footer?: ReactNode
  widthClassName?: string
}) {
  useEffect(() => {
    if (!open) return
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [open, onClose])

  if (!open) return null

  return createPortal(
    <div className="fixed inset-0 z-[var(--ss-z-modal)] flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/60" onClick={onClose} aria-hidden />
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className={`relative w-full ${widthClassName} rounded-md border border-hairline bg-elevated shadow-ss-md`}
      >
        <div className="flex items-center justify-between border-b border-hairline px-5 py-4">
          <h3 className="text-lg font-medium text-primary">{title}</h3>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="rounded-sm p-1 text-tertiary hover:bg-sunken hover:text-primary"
          >
            ✕
          </button>
        </div>
        <div className="px-5 py-4">{children}</div>
        {footer && <div className="flex justify-end gap-2 border-t border-hairline px-5 py-3">{footer}</div>}
      </div>
    </div>,
    document.body,
  )
}
