import { forwardRef, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react'

const controlBase =
  'w-full rounded-sm border border-hairline bg-sunken px-2.5 text-primary placeholder:text-tertiary transition-colors duration-[var(--ss-duration-fast)] focus:border-border-strong'

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(function Input(
  { className = '', ...props },
  ref,
) {
  return <input ref={ref} className={`${controlBase} h-9 text-sm ${className}`} {...props} />
})

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(
  function Textarea({ className = '', ...props }, ref) {
    return <textarea ref={ref} className={`${controlBase} min-h-20 py-2 text-sm ${className}`} {...props} />
  },
)

export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(function Select(
  { className = '', children, ...props },
  ref,
) {
  return (
    <select ref={ref} className={`${controlBase} h-9 text-sm ${className}`} {...props}>
      {children}
    </select>
  )
})

export function FormField({
  label,
  htmlFor,
  hint,
  error,
  children,
}: {
  label: string
  htmlFor?: string
  hint?: string
  error?: string | null
  children: ReactNode
}) {
  return (
    <label htmlFor={htmlFor} className="flex flex-col gap-1.5">
      <span className="text-md font-medium text-secondary tracking-[var(--ss-tracking-tight)]">{label}</span>
      {children}
      {hint && !error && <span className="text-xs text-tertiary">{hint}</span>}
      {error && <span className="text-xs text-sev-critical">{error}</span>}
    </label>
  )
}
