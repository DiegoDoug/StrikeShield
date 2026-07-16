import type { SVGProps } from 'react'

export type IconName =
  | 'clients'
  | 'playbooks'
  | 'settings'
  | 'logout'
  | 'sun'
  | 'moon'
  | 'chevron-right'
  | 'chevron-down'
  | 'plus'
  | 'trash'
  | 'check'
  | 'x'
  | 'play'
  | 'download'
  | 'external-link'
  | 'shield-check'
  | 'refresh'
  | 'calendar'
  | 'bolt'

const paths: Record<IconName, string> = {
  clients: 'M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6M9 10a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM16 14c2.2.4 4 2.4 4 4.6M14.5 4.3a3 3 0 0 1 0 5.6',
  playbooks: 'M4 6h5v5H4V6ZM15 13h5v5h-5v-5ZM9 8.5h4a2 2 0 0 1 2 2V13M6.5 11v3a2 2 0 0 0 2 2H9',
  settings:
    'M12 15.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7ZM19.4 13.5a1.7 1.7 0 0 0 .3 1.9l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.5V20a2 2 0 1 1-4 0v-.2a1.7 1.7 0 0 0-1-1.5 1.7 1.7 0 0 0-1.9.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.9 1.7 1.7 0 0 0-1.5-1H4a2 2 0 1 1 0-4h.2a1.7 1.7 0 0 0 1.5-1 1.7 1.7 0 0 0-.3-1.9l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.9.3H10a1.7 1.7 0 0 0 1-1.5V4a2 2 0 1 1 4 0v.2a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.9-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.9V10a1.7 1.7 0 0 0 1.5 1H20a2 2 0 1 1 0 4h-.2a1.7 1.7 0 0 0-1.5 1Z',
  logout: 'M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9',
  sun: 'M12 3v2M12 19v2M4.2 4.2l1.4 1.4M18.4 18.4l1.4 1.4M3 12h2M19 12h2M4.2 19.8l1.4-1.4M18.4 5.6l1.4-1.4M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Z',
  moon: 'M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8Z',
  'chevron-right': 'M9 6l6 6-6 6',
  'chevron-down': 'M6 9l6 6 6-6',
  plus: 'M12 5v14M5 12h14',
  trash: 'M4 7h16M9 7V4h6v3M6 7l1 13a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-13M10 11v6M14 11v6',
  check: 'M20 6 9 17l-5-5',
  x: 'M18 6 6 18M6 6l12 12',
  play: 'M6 4.5v15l14-7.5-14-7.5Z',
  download: 'M12 3v13M6.5 11 12 16.5 17.5 11M4 20h16',
  'external-link': 'M14 5h5v5M19 5l-9 9M9 5H6a2 2 0 0 0-2 2v11a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-3',
  'shield-check': 'M12 3l8 3v6c0 4.5-3.2 7.7-8 9-4.8-1.3-8-4.5-8-9V6l8-3ZM9 12l2 2 4-4',
  refresh: 'M20 12a8 8 0 1 1-2.6-5.9M20 4v5h-5',
  calendar: 'M4 8h16M7 3v4M17 3v4M5 6h14a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1Z',
  bolt: 'M13 2 4 14h6l-1 8 9-12h-6l1-8Z',
}

export function Icon({ name, className = 'h-4 w-4', ...props }: { name: IconName } & SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
      {...props}
    >
      <path d={paths[name]} />
    </svg>
  )
}
