import { forwardRef, type ButtonHTMLAttributes } from 'react'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'
type Size = 'sm' | 'md'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  size?: Size
  loading?: boolean
}

const base =
  'inline-flex items-center justify-center gap-2 rounded-md font-medium transition-colors duration-[var(--ss-duration-fast)] ease-[var(--ss-ease-standard)] disabled:opacity-50 disabled:pointer-events-none whitespace-nowrap'

const variants: Record<Variant, string> = {
  primary: 'bg-flare text-on-accent hover:bg-flare-hover active:bg-flare-active shadow-ss-sm',
  secondary:
    'bg-elevated text-primary border border-hairline hover:border-border-strong hover:bg-sunken/40',
  ghost: 'bg-transparent text-secondary hover:bg-elevated hover:text-primary',
  danger: 'bg-transparent text-sev-critical border border-sev-critical/40 hover:bg-sev-critical-bg',
}

const sizes: Record<Size, string> = {
  sm: 'h-7 px-2.5 text-xs',
  md: 'h-9 px-3.5 text-sm',
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = 'secondary', size = 'md', loading, className = '', children, disabled, ...props },
  ref,
) {
  return (
    <button
      ref={ref}
      className={`${base} ${variants[variant]} ${sizes[size]} ${className}`}
      disabled={disabled || loading}
      {...props}
    >
      {loading && (
        <span className="h-3 w-3 animate-spin rounded-full border-2 border-current border-t-transparent" />
      )}
      {children}
    </button>
  )
})
