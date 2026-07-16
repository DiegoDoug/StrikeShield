export function Tabs<T extends string>({
  tabs,
  active,
  onChange,
}: {
  tabs: { value: T; label: string; count?: number }[]
  active: T
  onChange: (value: T) => void
}) {
  return (
    <div className="flex items-center gap-1 border-b border-hairline">
      {tabs.map((tab) => {
        const isActive = tab.value === active
        return (
          <button
            key={tab.value}
            type="button"
            onClick={() => onChange(tab.value)}
            className={`relative flex items-center gap-1.5 px-3 py-2.5 text-sm font-medium transition-colors duration-[var(--ss-duration-fast)] ${
              isActive ? 'text-primary' : 'text-tertiary hover:text-secondary'
            }`}
          >
            {tab.label}
            {tab.count !== undefined && (
              <span className="rounded-full bg-sunken px-1.5 py-0.5 text-xs text-tertiary">{tab.count}</span>
            )}
            {isActive && <span className="absolute inset-x-0 -bottom-px h-0.5 rounded-full bg-flare" />}
          </button>
        )
      })}
    </div>
  )
}
