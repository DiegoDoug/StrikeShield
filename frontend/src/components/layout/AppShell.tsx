import { useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '@/lib/auth'
import { applyTheme, getStoredTheme, type Theme } from '@/lib/theme'
import { Icon, type IconName } from '@/components/ui/Icon'

const navItems: { to: string; label: string; icon: IconName }[] = [
  { to: '/clients', label: 'Clients', icon: 'clients' },
  { to: '/playbooks', label: 'Playbooks', icon: 'playbooks' },
  { to: '/settings/integrations', label: 'Integrations', icon: 'settings' },
]

export function AppShell() {
  const { session, logout } = useAuth()
  const [theme, setTheme] = useState<Theme>(() => getStoredTheme())

  const toggleTheme = () => {
    const next: Theme = theme === 'dark' ? 'light' : 'dark'
    applyTheme(next)
    setTheme(next)
  }

  return (
    <div className="flex h-full min-h-screen bg-base">
      <aside className="flex w-nav shrink-0 flex-col border-r border-hairline bg-surface">
        <div className="flex h-header shrink-0 items-center gap-2 border-b border-hairline px-5">
          <span className="flex h-6 w-6 items-center justify-center rounded-sm bg-flare text-on-accent">
            <Icon name="shield-check" className="h-4 w-4" />
          </span>
          <span className="text-md font-semibold tracking-[var(--ss-tracking-tight)] text-primary">
            StrikeShield
          </span>
        </div>
        <nav className="flex flex-1 flex-col gap-0.5 px-3 py-4">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `flex items-center gap-2.5 rounded-md px-2.5 py-2 text-md font-medium transition-colors duration-[var(--ss-duration-fast)] ${
                  isActive
                    ? 'bg-flare-muted text-flare'
                    : 'text-secondary hover:bg-elevated hover:text-primary'
                }`
              }
            >
              <Icon name={item.icon} className="h-4 w-4 shrink-0" />
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="border-t border-hairline p-3">
          <div className="flex items-center gap-2 rounded-md px-2 py-2">
            <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-elevated text-xs font-medium text-secondary">
              {session?.email.slice(0, 1).toUpperCase()}
            </div>
            <div className="flex min-w-0 flex-1 flex-col">
              <span className="truncate text-sm font-medium text-primary">{session?.email}</span>
              <span className="text-xs text-tertiary">{session?.role}</span>
            </div>
            <button
              type="button"
              onClick={toggleTheme}
              aria-label="Toggle theme"
              className="rounded-sm p-1.5 text-tertiary hover:bg-elevated hover:text-primary"
            >
              <Icon name={theme === 'dark' ? 'sun' : 'moon'} className="h-4 w-4" />
            </button>
            <button
              type="button"
              onClick={logout}
              aria-label="Log out"
              className="rounded-sm p-1.5 text-tertiary hover:bg-elevated hover:text-primary"
            >
              <Icon name="logout" className="h-4 w-4" />
            </button>
          </div>
        </div>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <main className="mx-auto w-full max-w-(--ss-content-max-width) flex-1 px-8 py-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
