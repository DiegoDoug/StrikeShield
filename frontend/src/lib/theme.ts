const THEME_STORAGE_KEY = 'strikeshield.theme'

export type Theme = 'dark' | 'light'

// StrikeShield defaults dark (long monitoring sessions) — tokens.css only
// defines an inverted palette under [data-theme="light"]. Applied once on
// load and again only on an explicit user toggle, never mid-session from a
// system event (see tokens.css's dark-mode-protocol comment).
export function getStoredTheme(): Theme {
  const stored = localStorage.getItem(THEME_STORAGE_KEY)
  return stored === 'light' ? 'light' : 'dark'
}

export function applyTheme(theme: Theme) {
  if (theme === 'light') {
    document.documentElement.setAttribute('data-theme', 'light')
  } else {
    document.documentElement.removeAttribute('data-theme')
  }
  localStorage.setItem(THEME_STORAGE_KEY, theme)
}
