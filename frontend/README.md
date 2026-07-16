# StrikeShield frontend

React + TypeScript + Vite SPA covering the full operator lifecycle (docs/PHASED_PLAN.md Phase 9): auth, client/project/target management, engagement approval, playbook DAG viewer, scan launch wizard, live scan progress (SignalR with REST-polling fallback), findings triage, report generation/download, recurring scan scheduling, and integration settings.

Styled with the StrikeShield design tokens — `src/styles/tokens.css` and `src/styles/theme.css` are the canonical source; Tailwind v4 picks them up via `@theme` in `src/index.css`. Don't hand-roll colors/spacing/radii outside those files — add a token instead.

## Development

```bash
npm install
npm run dev   # http://localhost:5173, proxies /api and /hubs to http://localhost:8085
```

Requires the Api running separately (`docker compose up -d postgres redis api` from the repo root, or `dotnet run` against a local Postgres).

## Build

```bash
npm run build   # tsc -b && vite build -> dist/
```

## Docker

Built and served by the `frontend` service in the repo root's `docker-compose.yml` — see `Dockerfile` (multi-stage: Node build, then nginx) and `nginx.conf` (reverse-proxies `/api` and `/hubs` to the `api` container so the SPA is always same-origin).

## Structure

- `src/lib/` — API client (`api.ts`), auth context (`auth.tsx`), SignalR client (`signalr.ts`), theme toggle (`theme.ts`), data-fetching hook (`useApi.ts`)
- `src/types/api.ts` — TypeScript types mirrored by hand from `StrikeShield.Application`'s `*Models.cs` records and `StrikeShield.Domain`'s `Enums/*.cs` — keep in sync when the API changes
- `src/components/ui/` — design-system primitives (Button, Field, Panel, Dialog, Tabs, Badge, DagView, Icon)
- `src/components/layout/` — app shell (sidebar nav + header)
- `src/pages/` — top-level routed pages
- `src/features/` — page-scoped feature components (engagement tabs, scan job panels) too specific to live in `components/ui/`
