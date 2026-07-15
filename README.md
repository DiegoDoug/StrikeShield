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

**Phase 7: AI-powered, audience-specific reporting.** `ReportGenerationService`
(the Reporting Agent, `StrikeShield.Application.Reporting`) reuses Phase 6's
`ILlmClient` — one structured-output call per report. Every
quantitative/structural finding field (CVSS, CWE/CVE, repro steps, evidence,
PoC, fix, verification steps) comes straight from the already-normalized
`Finding`/`FindingEvidence` entities, never invented by the LLM; the call
only supplies the two narrative fields those don't already carry
(`businessImpact`/`riskRating`), framed per audience via a different system
prompt for each of the 4 report types. The response is schema-validated —
every finding must get both fields, or generation fails rather than
shipping a partial report (`ReportGenerationServiceTests.cs`, via a fake
`ILlmClient`). `ReportMarkdownBuilder` deterministically renders each
type's Markdown (no LLM involved in rendering itself —
`ReportMarkdownBuilderTests.cs`); `PlaywrightReportRenderer` converts that
to HTML (Markdig) then PDF (headless Chromium via Playwright
print-to-PDF) — the Api image now builds
`FROM mcr.microsoft.com/playwright/dotnet:v1.47.0-jammy` for exactly that.
`GET /api/engagements/{id}/reports/{type}` returns the rendered PDF.

**Phase 6** (still active): the AI orchestration layer — Correlator LLM-escalation +
Adaptive Planner.** `ILlmClient` is the one seam both extension points call
through (`StrikeShield.Application.Ai`): `AnthropicLlmClient` (a plain HTTP
call to Anthropic's Messages API) when `STRIKESHIELD_AI_LLM_API_KEY` is
set, `NullLlmClient` otherwise — the same "nothing to run against without
a key" contract Strix has. The Correlator's deterministic exact-fingerprint
pass is unchanged; a new escalation pass sends ambiguous cross-tool
clusters (same target/severity, no shared fingerprint) **one batched LLM
call each** for a merge/no-merge + confidence judgment, and never touches
the LLM at all for deterministic matches or with no key configured
(`CorrelatorLlmEscalationTests.cs` proves both, via a fake `ILlmClient`).
The Adaptive Planner proposes a `PlaybookAmendment` — a full replacement
`ArgsTemplate` for one not-yet-run step later in the same `ScanJob` — as a
pending, human-approved diff after a step discovers new assets; it's never
applied automatically, and never mutates the shared `PlaybookStep`
template. The Orchestrator's DAG executor now pauses a `ScanJob`
(`AwaitingApproval`) the moment a Pending amendment targets its next step,
and resumes safely (already-run steps aren't re-launched) once
`POST /api/scan-jobs/{id}/amendments/{amendmentId}/approve` (or `/reject`)
re-queues it. See `AdaptivePlannerTests.cs` for the proposal logic.

**Phase 5** (still active): multi-tool DAG playbooks + asset hand-off. `PlaybookStep` now
carries a `StepKey`/`DependsOn[]`/`Condition`, and the Orchestrator's new
`PlaybookDagPlanner` topologically sorts a playbook's steps instead of just
running them in `Order` — every Phase 2-4 playbook still runs in the same
order since none of them declare a dependency. New recon adapters
(Subfinder/Amass → subdomain `Asset`, Katana → URL `Asset`) plus `ffuf` and
`nikto` as step types. The actual hand-off: a step's `ArgsTemplate` can use
`"{assetsFile}"`/`"{assetsFile:<AssetType>}"`, expanded by the new
`StepArgsBuilder` into a path to a file of its dependencies' discovered
`Asset` values in the same shared scan-output volume `"{output}"` already
uses — see the seeded `full-external-recon` playbook, where subfinder's
subdomains become nmap's `-iL` host list and katana's URLs become
ffuf's/nuclei's input list. `GET /api/scan-jobs/{id}/assets` surfaces
everything discovered. See `tests/StrikeShield.Api.Tests/PlaybookDagPlannerTests.cs`
and `StepArgsBuilderTests.cs` for the ordering/hand-off proof.

**Phase 4** (still active): Strix, the AI pentesting engine, is wired in. `strikeshield/strix-runner`
(a slim Python base with `pip install strix-agent`, built locally by
`docker compose build`) runs as a new `strix` playbook step type — BYOK via
`STRIKESHIELD_STRIX_LLM_API_KEY` (see `.env.example`). Strix's findings
come back through the same generic SARIF importer from Phase 3, now
reading each SARIF run's own `tool.driver.name` so Strix's findings show
up with `sourceTool: "strix"` (previously hardcoded to `"sarif"`). Because
Strix's own runtime needs to talk to the Docker daemon directly (it spawns
its own sandbox container for dynamic testing), the `strix` step is the
one deliberate, documented exception to "only the Orchestrator touches
docker.sock." A seeded `strix-quick` playbook is ready to run once you
supply your own LLM key.

**Phase 3** (still active): cross-tool finding normalization + correlation. Nuclei (JSONL),
a generic SARIF importer (ready for Strix/Semgrep/CodeQL/Trivy in later
phases), OWASP ZAP (`-J` JSON), and nmap (`-oX` XML) all normalize into one
`Finding` table via per-format adapters — see
`src/StrikeShield.Application/Findings/Adapters/`. nmap's host/port output
also becomes `Asset` rows. A seeded `full-baseline` playbook (Nuclei + ZAP
baseline + nmap vuln scripts) exercises all three; `GET
/api/scan-jobs/{id}/findings` returns them normalized, with a
`Correlator` deterministically grouping findings that share an exact
dedupe fingerprint (same normalized target/CWE-or-CVE/location) into one
`CorrelationGroup` — proven by a fixture-based test
(`tests/StrikeShield.Api.Tests/CorrelatorTests.cs`), not live-scan
nondeterminism.

**Phase 2** (still active): a separate `StrikeShield.Orchestrator` worker
(own container, the only one with Docker socket access) polls for `Queued`
ScanJobs and runs each playbook step as an isolated, resource-bounded
container — no leftover containers whether the step succeeds, fails, or
times out. OWASP Juice Shop is included as the standing safe test target.

**Phase 1** (still active): Clients → Projects → Targets → Engagements,
with a hard-enforced rule — no `ScanJob` can be created against an
Engagement that isn't approved and currently inside its scope window
(`docs/ARCHITECTURE.md` §4/§8) — plus JWT bearer auth (a single seeded
admin user), EF Core + Postgres migrations, and CRUD for every entity.

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
curl -s http://localhost:8085/health | jq
# {
#   "status": "Healthy",
#   "checks": [{ "name": "postgres", "status": "Healthy", "description": "Postgres connection succeeded." }],
#   "totalDurationMs": ...
# }
```

Swagger UI: http://localhost:8085/swagger

> **Port already in use?** If `docker compose up` fails with
> `Bind for 0.0.0.0:8085 failed: port is already allocated` (or the same for
> 5432/6379), something else on your machine already has that port —
> another project, a local Postgres/Redis install, or (on Windows) IIS
> Express or a leftover container from a previous run. Either stop
> whatever's holding it, or remap it: copy `.env.example` to `.env` and set
> `STRIKESHIELD_API_PORT` / `STRIKESHIELD_POSTGRES_PORT` /
> `STRIKESHIELD_REDIS_PORT` to a free port, then re-run
> `docker compose up --build` (adjust the `localhost:8085` URLs above to
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
TOKEN=$(curl -s -X POST localhost:8085/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@strikeshield.local","password":"ChangeMe123!"}' | jq -r .token)
AUTH="Authorization: Bearer $TOKEN"

# 2. Get the seeded organization id
ORG_ID=$(curl -s localhost:8085/api/organizations -H "$AUTH" | jq -r '.[0].id')

# 3. Client -> Project -> Target -> Engagement (unapproved)
CLIENT_ID=$(curl -s -X POST localhost:8085/api/clients -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"organizationId\":\"$ORG_ID\",\"name\":\"Acme Corp\"}" | jq -r .id)
PROJECT_ID=$(curl -s -X POST localhost:8085/api/projects -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"clientId\":\"$CLIENT_ID\",\"name\":\"Q3 External Pentest\"}" | jq -r .id)
TARGET_ID=$(curl -s -X POST localhost:8085/api/targets -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"projectId\":\"$PROJECT_ID\",\"type\":\"Url\",\"value\":\"http://juice-shop:3000\"}" | jq -r .id)
ENGAGEMENT_ID=$(curl -s -X POST localhost:8085/api/engagements -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"projectId\":\"$PROJECT_ID\",\"name\":\"July Engagement\",\"scopeStart\":\"2026-01-01T00:00:00Z\",\"scopeEnd\":\"2027-01-01T00:00:00Z\"}" | jq -r .id)

# 4. Scan-job request against the unapproved engagement -> expect 403
curl -s -o /dev/null -w '%{http_code}\n' -X POST localhost:8085/api/scan-jobs -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"engagementId\":\"$ENGAGEMENT_ID\",\"targetId\":\"$TARGET_ID\",\"playbookName\":\"nuclei-quick\"}"
# -> 403

# 5. Approve the engagement
curl -s -X POST "localhost:8085/api/engagements/$ENGAGEMENT_ID/approve" -H "$AUTH" -H 'Content-Type: application/json' \
  -d '{"approvedBy":"qa-lead@strikeshield.local"}'

# 6. Same scan-job request again -> expect 202
curl -s -o /dev/null -w '%{http_code}\n' -X POST localhost:8085/api/scan-jobs -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"engagementId\":\"$ENGAGEMENT_ID\",\"targetId\":\"$TARGET_ID\",\"playbookName\":\"nuclei-quick\"}"
# -> 202
```

The same flow is asserted end-to-end in
`tests/StrikeShield.Api.Tests/ScopeGateTests.cs`.

### Phase 2 walkthrough (Nuclei actually runs, via curl)

Continuing with the `$AUTH`/`$ENGAGEMENT_ID`/`$TARGET_ID` from above (target
pointed at `http://juice-shop:3000`, engagement approved):

```bash
# 1. Launch the seeded nuclei-quick playbook
SCAN_JOB_ID=$(curl -s -X POST localhost:8085/api/scan-jobs -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"engagementId\":\"$ENGAGEMENT_ID\",\"targetId\":\"$TARGET_ID\",\"playbookName\":\"nuclei-quick\"}" | jq -r .id)

# 2. Poll until Completed — the Orchestrator picks it up within ~5s and
#    Nuclei's first run also fetches its template set, so this can take a
#    couple of minutes the very first time.
watch -n 5 "curl -s localhost:8085/api/scan-jobs/$SCAN_JOB_ID -H \"$AUTH\" | jq .status"

# 3. Raw Nuclei output (JSON-lines), once Completed
curl -s localhost:8085/api/scan-jobs/$SCAN_JOB_ID/steps -H "$AUTH" | jq .

# 4. Confirm no leftover step containers
docker ps -a --filter "name=strikeshield-step"
# -> empty
```

If a step comes back `Failed` instead of `Completed`, check
`errorMessage`/`exitCode` in the steps response — the most likely local
cause is Nuclei's first-run template download taking longer than the
step's timeout (`PlaybookStep.TimeoutSeconds`, 300s by default) on a slow
connection.

### Phase 3 walkthrough (normalized findings across 3 tools, via curl)

Continuing with the same `$AUTH`/`$ENGAGEMENT_ID`/`$TARGET_ID`:

```bash
# 1. Launch the seeded full-baseline playbook (Nuclei + ZAP baseline + nmap)
BASELINE_JOB_ID=$(curl -s -X POST localhost:8085/api/scan-jobs -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"engagementId\":\"$ENGAGEMENT_ID\",\"targetId\":\"$TARGET_ID\",\"playbookName\":\"full-baseline\"}" | jq -r .id)

# 2. Poll until Completed — ZAP's baseline scan is the slow step (up to 900s)
watch -n 5 "curl -s localhost:8085/api/scan-jobs/$BASELINE_JOB_ID -H \"$AUTH\" | jq .status"

# 3. Normalized findings, one shape regardless of source tool
curl -s localhost:8085/api/scan-jobs/$BASELINE_JOB_ID/findings -H "$AUTH" | jq '[.[] | {sourceTool, title, severity, cweIds}]'
```

Cross-tool correlation (grouping the same underlying issue found by two
different tools into one `CorrelationGroup`) is proven deterministically
with a known fixture in `tests/StrikeShield.Api.Tests/CorrelatorTests.cs`
rather than depending on live Nuclei/ZAP output happening to overlap.

### Phase 4 walkthrough (the AI pentesting engine, via curl)

Requires your own LLM API key — copy `.env.example` to `.env` and set
`STRIKESHIELD_STRIX_LLM_API_KEY` first. Every other tool/phase works
without this; Strix specifically has nothing to run against without it.

Continuing with the same `$AUTH`/`$ENGAGEMENT_ID`/`$TARGET_ID`:

```bash
# 1. Launch the seeded strix-quick playbook (budget-capped at $1.00)
STRIX_JOB_ID=$(curl -s -X POST localhost:8085/api/scan-jobs -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"engagementId\":\"$ENGAGEMENT_ID\",\"targetId\":\"$TARGET_ID\",\"playbookName\":\"strix-quick\"}" | jq -r .id)

# 2. Poll until Completed — an LLM-driven agent run, expect several minutes
watch -n 5 "curl -s localhost:8085/api/scan-jobs/$STRIX_JOB_ID -H \"$AUTH\" | jq .status"

# 3. Strix's findings, normalized the same as every other tool's
curl -s localhost:8085/api/scan-jobs/$STRIX_JOB_ID/findings -H "$AUTH" | jq '[.[] | select(.sourceTool=="strix")]'

# 4. run.json (cost/budget metadata) is stored alongside findings.sarif
curl -s localhost:8085/api/scan-jobs/$STRIX_JOB_ID/steps -H "$AUTH" | jq '[.[].artifacts[] | select(.fileName=="run.json")]'
```

If the step fails immediately, check `errorMessage` in the steps response
first — the most likely cause is a missing/invalid `LLM_API_KEY`.

### Phase 5 walkthrough (DAG playbooks + asset hand-off, via curl)

`full-external-recon` (subfinder + katana feeding nmap/ffuf/nuclei) is best
run against a real, authorized, multi-subdomain domain you own — subfinder
and katana have no public DNS/HTTP surface to discover on an internal-only
compose hostname like `juice-shop`. Point `$TARGET_ID` at that domain
instead (still under an approved Engagement) for a real demonstration of
the hand-off; the shape below works either way.

```bash
# 1. Launch the seeded full-external-recon playbook
RECON_JOB_ID=$(curl -s -X POST localhost:8085/api/scan-jobs -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"engagementId\":\"$ENGAGEMENT_ID\",\"targetId\":\"$TARGET_ID\",\"playbookName\":\"full-external-recon\"}" | jq -r .id)

# 2. Poll until Completed
watch -n 5 "curl -s localhost:8085/api/scan-jobs/$RECON_JOB_ID -H \"$AUTH\" | jq .status"

# 3. Step graph, in dependency order — nmap-recon/ffuf/nuclei-targeted
#    only start once their DependsOn step (subfinder/katana) has finished
curl -s localhost:8085/api/scan-jobs/$RECON_JOB_ID/steps -H "$AUTH" | jq '[.[] | {stepKey, dependsOn, status, startedAt, completedAt}]'

# 4. Everything subfinder/katana discovered, feeding the later steps
curl -s localhost:8085/api/scan-jobs/$RECON_JOB_ID/assets -H "$AUTH" | jq .
```

A step whose `DependsOn` step didn't reach `Completed` shows up with
`status: "Skipped"` rather than being launched at all — check
`PlaybookDagPlannerTests.cs`/`StepArgsBuilderTests.cs` for the ordering and
hand-off logic proven directly against the same code the Orchestrator runs.

### Phase 6 walkthrough (Correlator LLM-escalation + Adaptive Planner, via curl)

Requires your own LLM API key — copy `.env.example` to `.env` and set
`STRIKESHIELD_AI_LLM_API_KEY` first. Every other tool/phase works without
this; the Correlator's escalation pass and the Adaptive Planner
specifically have nothing to run against without it.

```bash
# 1. Launch a recon playbook against a real, authorized target (see the
#    Phase 5 walkthrough above) — once a step discovers new assets, the
#    Adaptive Planner may propose an amendment to a later, not-yet-run step
RECON_JOB_ID=$(curl -s -X POST localhost:8085/api/scan-jobs -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"engagementId\":\"$ENGAGEMENT_ID\",\"targetId\":\"$TARGET_ID\",\"playbookName\":\"full-external-recon\"}" | jq -r .id)

# 2. Poll — a Pending amendment pauses the job (status: "AwaitingApproval")
watch -n 5 "curl -s localhost:8085/api/scan-jobs/$RECON_JOB_ID -H \"$AUTH\" | jq .status"

# 3. See the proposal (never auto-applied)
curl -s localhost:8085/api/scan-jobs/$RECON_JOB_ID/amendments -H "$AUTH" | jq .

# 4a. Approve it — the target step now runs with the proposed ArgsTemplate
#     for this ScanJob only (the shared Playbook template is untouched),
#     and the job resumes automatically
curl -s -X POST localhost:8085/api/scan-jobs/$RECON_JOB_ID/amendments/{amendmentId}/approve \
  -H "$AUTH" -H 'Content-Type: application/json' -d '{"decidedBy":"you@example.com"}' | jq .

# 4b. ...or reject it — the job resumes and that step runs unmodified
# curl -s -X POST localhost:8085/api/scan-jobs/$RECON_JOB_ID/amendments/{amendmentId}/reject \
#   -H "$AUTH" -H 'Content-Type: application/json' -d '{"decidedBy":"you@example.com"}' | jq .
```

Cross-tool correlation's LLM-escalation path (merging findings that share a
target/severity but disagree on everything else) is proven deterministically
with a fake `ILlmClient` in `tests/StrikeShield.Api.Tests/CorrelatorLlmEscalationTests.cs`
— including that the LLM is never called for deterministic matches or with
no key configured — rather than depending on a live model's output.

### Phase 7 walkthrough (AI-powered reporting, via curl)

Requires your own LLM API key — the same `STRIKESHIELD_AI_LLM_API_KEY` from
the Phase 6 walkthrough above. Every other tool/phase works without this;
the Reporting Agent specifically has no narrative content to write without
an LLM.

Continuing with the same `$AUTH`/`$ENGAGEMENT_ID` (from an engagement with
at least one completed scan — e.g. the Phase 3 walkthrough's
`full-baseline` run):

```bash
curl -s localhost:8085/api/engagements/$ENGAGEMENT_ID/reports/executive -o exec.pdf
curl -s localhost:8085/api/engagements/$ENGAGEMENT_ID/reports/technical -o tech.pdf
curl -s localhost:8085/api/engagements/$ENGAGEMENT_ID/reports/dev-remediation -o dev.pdf
curl -s localhost:8085/api/engagements/$ENGAGEMENT_ID/reports/compliance -o compliance.pdf
```

Each call regenerates the report fresh (no caching) and returns the
rendered PDF directly. If the engagement has no findings yet, or the LLM
call fails/returns an incomplete response (missing a finding's
`businessImpact`/`riskRating`), the endpoint returns `400` with a message
explaining why rather than a partial PDF — proven with a fake `ILlmClient`
in `tests/StrikeShield.Api.Tests/ReportGenerationServiceTests.cs`; the
deterministic Markdown rendering itself (no LLM/Playwright involved) is
proven in `ReportMarkdownBuilderTests.cs`.

## Solution layout

```
src/
  StrikeShield.Domain/          entities, enums, and the scope-gate business rule
  StrikeShield.Application/     use cases, DTOs, auth (JWT + password hashing), finding adapters + Correlator
  StrikeShield.Infrastructure/  EF Core + Postgres, migrations, seeding, health checks
  StrikeShield.Api/             ASP.NET Core Web API host, JWT wiring, endpoints
  StrikeShield.Orchestrator/    Docker.DotNet worker: runs playbook steps as isolated containers
tests/
  StrikeShield.Api.Tests/       integration tests (WebApplicationFactory)
containers/
  strix-runner/                 Dockerfile for the strix step type (pip install strix-agent)
docs/
  ARCHITECTURE.md               design & rationale
  PHASED_PLAN.md                phase-by-phase build + acceptance criteria
```

## Legal / scope

StrikeShield launches active security tools (including an autonomous AI
exploitation agent, in a later phase) against configured targets. Only
ever point it at systems you own or are explicitly authorized to test. The
scope/authorization gate from Phase 1 (`docs/ARCHITECTURE.md` §4/§8)
enforces this at the data-model level — no `ScanJob` can be created against
an unapproved or out-of-window Engagement — and as of Phase 2 this repo
does actively launch tools (currently Nuclei) in Docker containers, so that
gate is now live, not theoretical. The bundled OWASP Juice Shop service is
there specifically so you have a target you're authorized to scan without
needing one of your own yet.
