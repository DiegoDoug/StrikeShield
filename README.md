# StrikeShield

StrikeShield is an audit/orchestration platform that manages pentesting
projects, clients, scheduling, and AI-generated reporting on top of two
kinds of workers:

- **Strix** ([usestrix/strix](https://github.com/usestrix/strix)) — the
  autonomous AI pentesting engine, run as an isolated Docker step.
- A set of classical security tools (Nmap, Nuclei, OWASP ZAP, ffuf, Nikto,
  Trivy, Semgrep/CodeQL, Amass/Subfinder, Katana) — each run in its own
  isolated Docker container, normalized into one findings schema.

Read **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** for how Strix works
internally, the tech stack rationale, the AI orchestration design, and the
data/tool-normalization model.

Read **[docs/PHASED_PLAN.md](docs/PHASED_PLAN.md)** for the build order —
every phase ships something you can `git pull` and verify with
`docker compose up`.

## Current status

**Phase 1: core domain, JWT auth, and the scope/authorization gate.**
Clients → Projects → Targets → Engagements, with a hard-enforced rule: no
`ScanJob` can be created against an Engagement that isn't approved and
currently inside its scope window (`docs/ARCHITECTURE.md` §4/§8). Scan
execution is still stubbed — a ScanJob just records `Queued` once the gate
passes; the Docker orchestrator that actually runs tools lands in Phase 2.

Also included: JWT bearer auth (a single seeded admin user), EF Core +
Postgres migrations, and CRUD for every entity above.

## Quick start

Requirements: Docker + Docker Compose. (.NET 8 SDK only if you want to run
`dotnet build`/`dotnet test` outside of Docker.)

```bash
git clone <this-repo>
cd StrikeShield
docker compose up --build
```

Then:

```bash
curl -s http://localhost:8080/health | jq
# {
#   "status": "Healthy",
#   "checks": [{ "name": "postgres", "status": "Healthy", "description": "Postgres connection succeeded." }],
#   "totalDurationMs": ...
# }
```

Swagger UI: http://localhost:8080/swagger

> **Port already in use?** If `docker compose up` fails with
> `Bind for 0.0.0.0:8080 failed: port is already allocated` (or the same for
> 5432/6379), something else on your machine already has that port —
> another project, a local Postgres/Redis install, or (on Windows) IIS
> Express or a leftover container from a previous run. Either stop
> whatever's holding it, or remap it: copy `.env.example` to `.env` and set
> `STRIKESHIELD_API_PORT` / `STRIKESHIELD_POSTGRES_PORT` /
> `STRIKESHIELD_REDIS_PORT` to a free port, then re-run
> `docker compose up --build` (adjust the `localhost:8080` URLs above to
> match). `docker ps -a` will show if a stray container from an earlier
> attempt is still holding the port.

To run tests locally (requires a reachable Postgres — either
`docker compose up -d postgres` first, or point `ConnectionStrings__Postgres`
at one):

```bash
dotnet test StrikeShield.sln
```

### Phase 1 walkthrough (the scope-gate acceptance test, via curl)

On first boot the API seeds one organization and one admin user
(`admin@strikeshield.local` / `ChangeMe123!` by default — override via
`.env`, see `.env.example`, before running this anywhere but local dev).

```bash
# 1. Log in and grab a token
TOKEN=$(curl -s -X POST localhost:8080/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@strikeshield.local","password":"ChangeMe123!"}' | jq -r .token)
AUTH="Authorization: Bearer $TOKEN"

# 2. Get the seeded organization id
ORG_ID=$(curl -s localhost:8080/api/organizations -H "$AUTH" | jq -r '.[0].id')

# 3. Client -> Project -> Target -> Engagement (unapproved)
CLIENT_ID=$(curl -s -X POST localhost:8080/api/clients -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"organizationId\":\"$ORG_ID\",\"name\":\"Acme Corp\"}" | jq -r .id)
PROJECT_ID=$(curl -s -X POST localhost:8080/api/projects -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"clientId\":\"$CLIENT_ID\",\"name\":\"Q3 External Pentest\"}" | jq -r .id)
TARGET_ID=$(curl -s -X POST localhost:8080/api/targets -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"projectId\":\"$PROJECT_ID\",\"type\":\"Url\",\"value\":\"https://juice-shop.example.test\"}" | jq -r .id)
ENGAGEMENT_ID=$(curl -s -X POST localhost:8080/api/engagements -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"projectId\":\"$PROJECT_ID\",\"name\":\"July Engagement\",\"scopeStart\":\"2026-01-01T00:00:00Z\",\"scopeEnd\":\"2027-01-01T00:00:00Z\"}" | jq -r .id)

# 4. Scan-job request against the unapproved engagement -> expect 403
curl -s -o /dev/null -w '%{http_code}\n' -X POST localhost:8080/api/scan-jobs -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"engagementId\":\"$ENGAGEMENT_ID\",\"targetId\":\"$TARGET_ID\",\"playbookName\":\"nuclei-quick\"}"
# -> 403

# 5. Approve the engagement
curl -s -X POST "localhost:8080/api/engagements/$ENGAGEMENT_ID/approve" -H "$AUTH" -H 'Content-Type: application/json' \
  -d '{"approvedBy":"qa-lead@strikeshield.local"}'

# 6. Same scan-job request again -> expect 202
curl -s -o /dev/null -w '%{http_code}\n' -X POST localhost:8080/api/scan-jobs -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"engagementId\":\"$ENGAGEMENT_ID\",\"targetId\":\"$TARGET_ID\",\"playbookName\":\"nuclei-quick\"}"
# -> 202
```

The same flow is asserted end-to-end in
`tests/StrikeShield.Api.Tests/ScopeGateTests.cs`.

## Solution layout

```
src/
  StrikeShield.Domain/          entities, enums, and the scope-gate business rule
  StrikeShield.Application/     use cases, DTOs, auth (JWT + password hashing)
  StrikeShield.Infrastructure/  EF Core + Postgres, migrations, seeding, health checks
  StrikeShield.Api/             ASP.NET Core Web API host, JWT wiring, endpoints
tests/
  StrikeShield.Api.Tests/       integration tests (WebApplicationFactory)
docs/
  ARCHITECTURE.md               design & rationale
  PHASED_PLAN.md                phase-by-phase build + acceptance criteria
```

## Legal / scope

StrikeShield launches active security tools (including an autonomous AI
exploitation agent) against configured targets. Only ever point it at
systems you own or are explicitly authorized to test. The scope/authorization
gate added in Phase 1 (`docs/ARCHITECTURE.md` §4/§8) enforces this at the
data-model level — no `ScanJob` can be created against an unapproved or
out-of-window Engagement — but scan *execution* itself is still stubbed
until Phase 2's Docker orchestrator lands, so this repo does not yet launch
any real tool against any target.
