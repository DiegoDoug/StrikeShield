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

**Phase 0 (this commit): repo bootstrap.** Clean Architecture solution
skeleton (`Domain` / `Application` / `Infrastructure` / `Api`), Postgres +
Redis via Docker Compose, a `/health` endpoint that actually verifies DB
connectivity, and CI (build + test on every push). No business logic yet —
that starts in Phase 1.

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

To run tests locally (requires a reachable Postgres — either
`docker compose up -d postgres` first, or point `ConnectionStrings__Postgres`
at one):

```bash
dotnet test StrikeShield.sln
```

## Solution layout

```
src/
  StrikeShield.Domain/          entities & business rules (empty until Phase 1)
  StrikeShield.Application/     use cases / orchestration services
  StrikeShield.Infrastructure/  EF Core + Postgres, health checks, (later) Docker orchestration
  StrikeShield.Api/             ASP.NET Core Web API host
tests/
  StrikeShield.Api.Tests/       integration tests (WebApplicationFactory)
docs/
  ARCHITECTURE.md               design & rationale
  PHASED_PLAN.md                phase-by-phase build + acceptance criteria
```

## Legal / scope

StrikeShield launches active security tools (including an autonomous AI
exploitation agent) against configured targets. Only ever point it at
systems you own or are explicitly authorized to test. Phase 1 adds a
hard-enforced scope/authorization gate — see `docs/ARCHITECTURE.md` §4 and
§8 — but until that lands, this repo is scaffolding only and does not run
any scans.
