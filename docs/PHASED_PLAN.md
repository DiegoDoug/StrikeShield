# Phased Development Plan

Ground rules for every phase:
- Work happens on `claude/pentesting-orchestration-platform-0xzgjs` (or its
  successor feature branches), you `git pull`, run the docker command shown,
  and check the acceptance criteria before moving on.
- Every phase ships something that runs via `docker compose up --build`.
  Nothing is "done" until that command works from a clean pull.
- Phases are additive — later phases don't rewrite earlier ones, they extend
  the compose stack and the domain model.
- Only test against targets you own or are explicitly authorized to test
  (e.g. OWASP Juice Shop / DVWA, spun up as throwaway compose services —
  see Phase 2). Never point active-scan phases at third-party infrastructure.

Status: **Phases 0-7 implemented** (see repo root). Phase 8+ are
specified below, ready to build next.

---

## Phase 0 — Repo bootstrap & health-checkable skeleton ✅ (this session)

**Goal:** Prove the toolchain (ASP.NET Core + Postgres + Redis + Docker
Compose + CI) works end to end before any business logic exists.

**Deliverables:**
- Clean Architecture solution skeleton: `StrikeShield.Domain`,
  `StrikeShield.Application`, `StrikeShield.Infrastructure`,
  `StrikeShield.Api`, `StrikeShield.Api.Tests`.
- `Program.cs` with Serilog logging, Swagger, and a `/health` endpoint that
  actually pings Postgres (not a hardcoded 200).
- `docker-compose.yml`: `api`, `postgres`, `redis`.
- GitHub Actions CI: restore/build/test on push.

**Test it:**
```bash
git pull origin claude/pentesting-orchestration-platform-0xzgjs
docker compose up --build
curl -s http://localhost:8085/health | jq
# expect: {"status":"Healthy", "checks":[{"name":"postgres","status":"Healthy"}]}
```
**Acceptance:** container stack comes up clean, `/health` returns `Healthy`,
`dotnet test` passes locally (`dotnet test` in `src/`).

---

## Phase 1 — Core domain: Clients, Projects, Targets, Engagements, scope gate ✅

**Goal:** The non-negotiable authorization/scope model from
`ARCHITECTURE.md` §4, plus basic CRUD, before any scanning exists.

**Deliverables:**
- EF Core entities + migrations: `Organization`, `AppUser`, `Client`,
  `Project`, `Target`, `Engagement` (with `authorizationEvidenceUri`,
  `approvedBy/At`, `scopeStart/End`, `allowedScopeRules`), `ScanJob`.
- REST endpoints: CRUD for clients/projects/targets, create+approve for
  engagements, create+read for scan-jobs, a read-only organizations list.
- Domain rule: creating a `ScanJob` (stubbed in this phase — real execution
  is Phase 2) against an `Engagement` that isn't approved/in-window returns
  `403` with a clear reason (`Engagement.CheckAuthorizedForScan`, enforced in
  `ScanJobService`), unit/integration tested.
- JWT bearer auth (`Microsoft.Extensions.Identity.Core`'s `PasswordHasher`
  + hand-issued JWTs) with a single seeded admin user — full RBAC and the
  EF Identity membership system come later (Phase 10).

**Test it:**
```bash
docker compose up --build
# See README.md "Phase 1 walkthrough" for the full curl sequence:
# 1. login, create client -> project -> target -> engagement (unapproved)
# 2. POST /api/scan-jobs against it -> expect 403
# 3. approve the engagement -> retry -> expect 202
```
**Acceptance:** `tests/StrikeShield.Api.Tests/ScopeGateTests.cs` (an
integration test using `WebApplicationFactory`) covers the happy path and
the scope-gate rejection end to end; the same flow is provable via curl
(README.md).

---

## Phase 2 — Scan orchestrator + first real tool (Nuclei) against a safe target ✅

**Goal:** Prove the Docker-orchestration primitive end to end with the
simplest tool before adding Strix or multi-step DAGs.

**Deliverables:**
- `StrikeShield.Orchestrator`: a separate worker service (own project,
  own container, own Dockerfile) using `Docker.DotNet` — deliberately not
  merged into the Api process, since it's the only component that mounts
  the Docker socket (docs/ARCHITECTURE.md §5/§8).
- `juice-shop` (OWASP Juice Shop) added as a compose service — the standing
  safe test target for every phase from here on.
- `Playbook`/`PlaybookStep`/`ScanJob`/`StepRun`/`Artifact` entities. `ScanJob`
  now references a real `Playbook` (by slug, e.g. `"nuclei-quick"`) instead
  of a free-text name. A single-step "nuclei-quick" playbook is seeded on
  first boot.
- The Orchestrator polls for `Queued` ScanJobs, and for each step launches a
  pinned `projectdiscovery/nuclei` container attached to the shared
  `strikeshield-net` network (so it can resolve `juice-shop`), bounded by
  the step's configured memory/CPU/timeout, with output written to a
  Docker volume (`strikeshield-scan-output`) shared with the Orchestrator's
  own filesystem view — collected into an `Artifact` row and the container
  removed immediately after, whether it succeeded, failed, or timed out.
- `GET /api/scan-jobs/{id}/steps` exposes each step's status and artifacts
  (`Queued → Running → Completed/Failed/TimedOut` at both the ScanJob and
  StepRun level).

> **Schema change note:** this phase changes the ScanJobs table (drops
> `PlaybookName`, adds a required `PlaybookId` FK). If you already ran
> Phase 1 locally, drop your Postgres volume first: `docker compose down -v`.

**Test it:**
```bash
docker compose down -v   # only needed if you ran Phase 1 before this
docker compose up --build

# 1. Log in, create client -> project -> target (juice-shop) -> engagement,
#    approve it (see README.md Phase 1 walkthrough for the exact curl calls).
# 2. Launch the seeded playbook against the target:
curl -X POST localhost:8085/api/scan-jobs -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"engagementId\":\"$ENGAGEMENT_ID\",\"targetId\":\"$TARGET_ID\",\"playbookName\":\"nuclei-quick\"}"
# 3. Poll until Completed (the Orchestrator picks it up within ~5s):
curl localhost:8085/api/scan-jobs/$SCAN_JOB_ID -H "$AUTH"
# 4. Raw Nuclei output, once Completed:
curl localhost:8085/api/scan-jobs/$SCAN_JOB_ID/steps -H "$AUTH"
# 5. No leaked containers:
docker ps -a --filter "name=strikeshield-step"   # expect: empty
```
**Acceptance:** the ScanJob reaches `Completed` (or `Failed`/`TimedOut` if
Nuclei itself errors — check the step's `errorMessage`), the raw Nuclei
JSON-lines output is retrievable via `GET /api/scan-jobs/{id}/steps`, and
`docker ps -a` shows no `strikeshield-step-*` containers left behind.

---

## Phase 3 — Normalization layer + SARIF + 3 more tools ✅

**Goal:** Unified `Finding`/`Asset` model, proving the "normalize everything"
promise with tools that have genuinely different native formats.

**Deliverables:**
- `Finding`/`Asset`/`FindingEvidence` entities per `ARCHITECTURE.md` §6.
- Adapters: Nuclei (from Phase 2), generic **SARIF importer** (reused later
  for Strix/Semgrep/CodeQL/Trivy), ZAP JSON adapter, nmap XML adapter (asset
  + vuln-script findings).
- Add `zap` baseline scan and `nmap` as additional playbook step types
  against Juice Shop.
- `dedupeFingerprint` computation + a basic Correlator that groups exact
  fingerprint matches into one `CorrelationGroup` (LLM-assisted correlation
  is Phase 6, not here).

**Test it:**
```bash
docker compose up --build
# run playbook "nuclei+zap+nmap" against juice-shop
curl localhost:8085/api/scan-jobs/{id}/findings | jq 'length, .[0]'
```
**Acceptance:** findings from all 3 tools appear in one `Finding` table with
populated `severity`/`cweIds` where the source tool provides them; a finding
flagged by both Nuclei and ZAP for the same CWE+endpoint collapses into a
single `CorrelationGroup` (verified by a fixture-based test with known
overlapping sample output, not by relying on live-scan nondeterminism).

---

## Phase 4 — Strix integration (the AI pentesting engine) ✅

**Goal:** Wire in the actual AI hacker, budget-capped, BYOK.

**What the phase spike found (worth knowing before touching this code):**
- Strix's own `containers/Dockerfile` is **not** something to build our
  runner from — it's the Kali-based *sandbox* Strix itself spawns as a
  nested container for dynamic testing. `strikeshield/strix-runner`
  (`containers/strix-runner/Dockerfile` in this repo) is instead a slim
  `python:3.12-slim` base with `pip install strix-agent`.
- Strix's runtime talks to the Docker daemon directly via the Python
  `docker` SDK (`docker>=7.1.0` in its own `pyproject.toml`) to launch that
  sandbox — so the `strix` step is a deliberate, narrow exception to
  "only the Orchestrator touches docker.sock": it gets `/var/run/docker.sock`
  bind-mounted too (see `PlaybookExecutor.RunStepContainerAsync`).
- `-n`/`--non-interactive` exits non-zero when it finds vulnerabilities —
  a result, not a failure (handled the same way zap-baseline's WARN/FAIL
  exit codes are: judged by whether `findings.sarif` actually exists, not
  by exit code alone).
- No CLI flag controls Strix's output path — it always writes to
  `./strix_runs/<auto-generated-run-name>/`. The Orchestrator points the
  container's working directory at its own tracked per-step output
  directory so that lands inside the shared volume, then globs for the
  `.sarif` file and `run.json` rather than assuming an exact path.

**Deliverables:**
- `strikeshield/strix-runner` image, built locally by `docker compose build`
  (Docker.DotNet only pulls from a registry, so a purely-local custom image
  has to be compose-built, unlike nuclei/zap/nmap).
- Orchestrator step type `strix`: injects `STRIX_LLM`/`LLM_API_KEY` (BYOK —
  simplified to instance-wide Orchestrator config for this phase rather than
  a full per-organization encrypted secret vault, which is Phase 10 scope),
  runs `strix -n --target <t> --scan-mode quick --max-budget-usd <cap>`,
  waits, then ingests `findings.sarif` via the Phase 3 SARIF importer (no
  new parser needed — and now extracts each SARIF run's real
  `tool.driver.name` as `Finding.sourceTool` instead of a hardcoded
  "sarif", so Strix's findings show up as `sourceTool: "strix"`) plus
  `run.json` stored as a second artifact for cost/run visibility.
- A seeded `strix-quick` playbook.

**Test it:**
```bash
# Copy .env.example to .env and set STRIKESHIELD_STRIX_LLM_API_KEY to your
# own key first — every other tool works without this, but strix has
# nothing to run against without it.
docker compose up --build
# run playbook "strix-quick" against juice-shop with max_budget_usd=1.00
curl localhost:8085/api/scan-jobs/{id}/findings | jq '[.[] | select(.sourceTool=="strix")]'
```
**Acceptance:** Strix findings appear with `cvssScore` and `cweIds`
populated where Strix's own SARIF output provides them (`PoC` best-effort —
SARIF's `properties` bag is tool-defined, not standardized); the run stops
at/under the configured budget; no leftover containers after the run. CI
verifies the `strix-runner` image builds and the playbook seeds correctly,
but doesn't launch a real scan — that needs a paid LLM key CI doesn't have,
and shouldn't be spending on every push regardless.

---

## Phase 5 — Multi-tool DAG playbooks + asset hand-off ✅

**Goal:** Recon output actually feeds later steps — the "orchestrate
multiple tools" promise, not just "run tools in parallel."

> **Schema change note:** this phase adds three required columns
> (`StepKey`, `DependsOn`, `Condition`) plus a unique `(PlaybookId,
> StepKey)` index to `PlaybookSteps`. If you already ran Phases 1-4
> locally, drop your Postgres volume first: `docker compose down -v` — the
> migration's default backfill (`StepKey = ""` for every existing row)
> would otherwise collide with that unique index the moment a playbook has
> more than one step (every playbook here except `nuclei-quick` does).

**Deliverables:**
- `PlaybookStep.StepKey`/`DependsOn[]`/`Condition` (`OnSuccess` | `Always`)
  and a DAG executor in `StrikeShield.Orchestrator`:
  `PlaybookDagPlanner` topologically sorts steps by `DependsOn` (falling
  back to the existing `Order` field/tie-break for every Phase 2-4 playbook,
  none of which declare any dependency, so their execution order is
  unchanged); a step whose `Condition` is `OnSuccess` (the default) and
  whose dependency didn't reach `Completed` is recorded as `Skipped`
  (`StepRunStatus.Skipped`) rather than launched. Independent branches no
  longer halt the whole job on an unrelated step's failure — only a step's
  own declared dependencies can skip it.
- Recon adapters: `SubdomainReconFindingAdapter` (Subfinder's plain-text
  subdomain list → `Asset`; Amass aliased to the same parser, same idea as
  Strix → the SARIF adapter), `KatanaFindingAdapter` (crawled-URL JSONL →
  `Asset`).
- `FfufFindingAdapter` (discovered paths → `Asset`, plus an Info `Finding`
  only for a sensitive-path heuristic match — `.git/`, `.env`, backups,
  etc.) and `NiktoFindingAdapter` (`Finding`, severity defaulted to Low)
  as new step types.
- `StepArgsBuilder` expands `"{assetsFile}"`/`"{assetsFile:<AssetType>}"`
  tokens in `ArgsTemplate` into a path to a newline-delimited file of the
  step's dependencies' discovered `Asset` values, written into the same
  shared scan-output volume `"{output}"` already uses — e.g. subfinder's
  subdomains become nmap's `-iL` host list, katana's URLs become
  ffuf's/nuclei's input list.
- A default `full-external-recon` playbook (subfinder + katana feeding
  nmap/ffuf/nuclei as a DAG, nikto standalone) seeded as a template.
- `GET /api/scan-jobs/{id}/assets`, and `StepKey`/`DependsOn` now visible on
  every `PlaybookStepResponse`/`StepRunResponse` so the DAG is inspectable
  over the API, not just in the seed code.

**Test it:**
```bash
docker compose up --build
# run "full-external-recon" against a scoped local multi-subdomain test target
curl localhost:8085/api/scan-jobs/{id}/steps   # expect ordered step graph w/ per-step status
curl localhost:8085/api/scan-jobs/{id}/assets  # expect subdomains/urls discovered by early steps
```
**Acceptance:** step graph executes in dependency order (visible via
step-run timestamps), and at least one downstream step's actual container
args are demonstrably built from an upstream step's asset output (not
hardcoded) — asserted directly against `StepArgsBuilder`/`PlaybookDagPlanner`
(the same code `PlaybookExecutor` calls to build a real container's `Cmd`)
in `tests/StrikeShield.Api.Tests/StepArgsBuilderTests.cs` and
`PlaybookDagPlannerTests.cs`, not just by eyeballing logs.

## Phase 6 — AI orchestration layer: correlation + adaptive planning ✅

**Goal:** The two LLM extension points from `ARCHITECTURE.md` §3 that go
beyond Strix itself.

**Deliverables:**
- `ILlmClient` (`StrikeShield.Application.Ai`): the one seam both
  extension points below call through — one prompt in, one JSON string
  out, no conversation/tool-use state. `AnthropicLlmClient` (a plain HTTP
  call to Anthropic's Messages API, no SDK) is registered when
  `AiOrchestration:LlmApiKey` is configured (BYOK, separate key/model from
  Strix's — see `.env.example`); `NullLlmClient` otherwise, the same
  "nothing to run against without a key" contract Strix's step type has.
- LLM-assisted Correlator escalation path (`Correlator.cs`): after the
  existing deterministic exact-fingerprint pass, ambiguous cross-tool
  matches (same normalized target + severity, no shared fingerprint) get
  **one batched LLM call per cluster** for a merge/no-merge + confidence
  judgment — never one call per pair. Deterministic matches, and every
  cluster once `NullLlmClient` is in use, never touch the LLM at all (cost
  control, proven via fixture call-count assertions, not just grouping
  outcomes — see `CorrelatorLlmEscalationTests.cs`).
- Adaptive Planner (`AdaptivePlanner.cs`): after a step that discovered new
  `Asset`s completes, may propose a `PlaybookAmendment` — a full
  replacement `ArgsTemplate` for one not-yet-run step later in the *same*
  `ScanJob` (e.g. a tech-fingerprint-driven nuclei tag addition) — as a
  **pending, human-approved diff**. Never auto-executed, and never mutates
  the shared `PlaybookStep` template row.
- The Orchestrator's DAG executor now pauses: a `ScanJob` with a Pending
  amendment targeting its next not-yet-run step is recorded
  `AwaitingApproval` and execution stops there. `PlaybookExecutor` is
  resumption-safe (steps that already have a `StepRun` from a prior
  invocation aren't re-launched), so once an operator decides, re-queuing
  the job picks execution back up exactly where it paused.
- API endpoints to view/approve/reject a pending playbook amendment:
  `GET /api/scan-jobs/{id}/amendments`,
  `POST /api/scan-jobs/{id}/amendments/{amendmentId}/approve`,
  `POST /api/scan-jobs/{id}/amendments/{amendmentId}/reject` — approving
  substitutes the proposed args for that one `ScanJob`'s run only;
  rejecting just un-pauses the job and runs the step unmodified.

**Test it:**
```bash
docker compose up --build
# seed a fixture set of overlapping findings from different tools
dotnet test --filter "Correlator|AdaptivePlanner"
# copy .env.example to .env and set STRIKESHIELD_AI_LLM_API_KEY first —
# every other feature/phase works without this, the Correlator's LLM
# escalation pass and the Adaptive Planner specifically have nothing to
# run against without it (same BYOK contract as Strix, Phase 4)
# run a recon playbook (e.g. full-external-recon) against a real target,
# expect a pending amendment once a downstream step is reached
curl localhost:8085/api/scan-jobs/{id}/amendments
curl -X POST localhost:8085/api/scan-jobs/{id}/amendments/{amendmentId}/approve \
  -H 'Content-Type: application/json' -d '{"decidedBy":"you@example.com"}'
```
**Acceptance:** fixture tests show overlapping findings collapse to one
`CorrelationGroup` via the LLM-escalation path *only* when a cluster is
genuinely ambiguous, and prove the LLM is never called for deterministic
matches or when no BYOK key is configured
(`CorrelatorLlmEscalationTests.cs`); a live recon run against a real,
authorized target produces a pending (not auto-run) amendment that a human
must approve or reject before the paused `ScanJob` resumes
(`AdaptivePlannerTests.cs` proves the proposal logic deterministically; the
live end-to-end pause/resume needs a real LLM key, same as Strix).

---

## Phase 7 — AI-powered, audience-specific reporting ✅

**Goal:** The reporting feature list from your prompt, verbatim.

**Deliverables:**
- `ReportGenerationService` (the Reporting Agent, `StrikeShield.Application.Reporting`):
  one structured-output LLM call per report, reusing Phase 6's `ILlmClient`
  (same BYOK key/model — `AiOrchestration:LlmApiKey`). Every
  quantitative/structural finding field (CVSS, CWE/CVE, repro steps,
  evidence, PoC, fix, verification steps) is sourced directly from the
  already-normalized `Finding`/`FindingEvidence` entities, never invented
  by the LLM — the call only supplies the two narrative fields those don't
  already carry (`businessImpact`/`riskRating`), framed per audience via a
  different system prompt for each of the 4 types from `ARCHITECTURE.md`
  §7. The response is schema-validated (every finding must get both
  fields) before anything renders — an incomplete LLM response throws
  rather than shipping a partial report.
- `ReportMarkdownBuilder`: deterministic (no LLM) Markdown rendering per
  report type — Executive (grouped by severity, no jargon/CVSS/PoC),
  Technical (full detail), Developer Remediation (grouped by affected
  asset/component), Compliance (OWASP/CWE matrix + MITRE ATT&CK coverage
  table).
- `PlaywrightReportRenderer`: Markdown → HTML (Markdig) → PDF (headless
  Chromium via Playwright print-to-PDF). The Api image's runtime stage now
  builds `FROM mcr.microsoft.com/playwright/dotnet:v1.47.0-jammy` (Chromium
  pre-installed, version-matched to the `Microsoft.Playwright` NuGet
  package) instead of the plain aspnet runtime image.
- `GET /api/engagements/{id}/reports/{type}` returning the rendered PDF —
  `type` is one of `executive|technical|dev-remediation|compliance`.
  Regenerates (and persists a new `Report` row) on every call rather than
  caching.

**Test it:**
```bash
docker compose up --build
# copy .env.example to .env and set STRIKESHIELD_AI_LLM_API_KEY first —
# reporting reuses Phase 6's BYOK key; every other feature/phase works
# without this, the Reporting Agent specifically has nothing to write
# narrative content with otherwise
curl localhost:8085/api/engagements/{id}/reports/executive -o exec.pdf
curl localhost:8085/api/engagements/{id}/reports/technical -o tech.pdf
curl localhost:8085/api/engagements/{id}/reports/dev-remediation -o dev.pdf
curl localhost:8085/api/engagements/{id}/reports/compliance -o compliance.pdf
```
**Acceptance:** all 4 PDFs generate from one completed engagement; an
automated schema-validation test asserts every finding in the technical
report contains all required fields (`ReportMarkdownBuilderTests.cs`), and
that report generation fails rather than shipping a partial report when
the LLM's response omits a finding's `businessImpact`/`riskRating`
(`ReportGenerationServiceTests.cs`, via a fake `ILlmClient` — not a live
call, same as Phase 6's fixture tests).

---

## Phase 8 — Scheduling & integrations

**Goal:** "Scheduling" and "automation" from your prompt.

**Deliverables:**
- Hangfire recurring jobs: cron-based recurring `ScanJob`s per
  `Engagement`, with the Phase 1 scope/window gate still enforced at fire
  time (an expired engagement should skip, not silently re-run forever).
- Slack + generic webhook notifications on scan completion / new
  critical finding; GitHub issue creation as an optional integration.
- Hangfire dashboard mounted (auth-gated) for visibility.

**Test it:**
```bash
docker compose up --build
# schedule a recurring scan at */5 * * * * (test cadence only)
# point the Slack integration at a local webhook receiver (e.g. requestbin/ngrok-free tool, or a tiny compose service that just logs POSTs)
```
**Acceptance:** recurring job fires on schedule (observed via Hangfire
dashboard + `ScanJob` history), notification received by the test receiver
within one cycle, and a job scheduled against an expired engagement
demonstrably skips with a logged reason instead of running.

---

## Phase 9 — Frontend (React) for the full lifecycle

**Goal:** You can do everything above from a browser, not just curl/Swagger.

**Deliverables:**
- React + TS + Vite SPA: auth, client/project/target management, playbook
  editor (visual DAG), scan launch wizard (with budget/scope checks
  surfaced, not just enforced server-side), live progress via SignalR,
  findings triage board (status transitions), report viewer/download,
  scheduling UI, integration settings.

**Test it:** manual E2E click-through, plus a Playwright test suite driving
the same flow headlessly (`docker compose -f docker-compose.e2e.yml up`).

**Acceptance:** a from-scratch operator (no API knowledge) can create a
client → project → target → engagement approval → launch a playbook →
watch it run → triage findings → generate and download a report, entirely
through the UI.

---

## Phase 10 — Multi-tenant hardening & "sell it" readiness

**Goal:** Only tackle this once phases 0-9 work for you personally.

**Deliverables:**
- Full RBAC (Owner/Admin/Analyst/Viewer) enforced per-organization.
- Secrets in a real vault (Azure Key Vault/HashiCorp Vault), not appsettings.
- `docker-socket-proxy` in front of the Orchestrator's Docker access,
  Orchestrator isolated from the public-facing Api network.
- Rate/concurrency limits per organization on concurrent scan jobs.
- Full audit log surfaced in UI, exportable for a client's own compliance
  needs.
- Load/soak test of the Orchestrator under N concurrent multi-step
  playbooks; a lightweight external security review of StrikeShield itself
  (dogfood it — point Strix at your own API, scoped and authorized, since
  you own it).

**Test it:** a second, non-admin test organization can be created and is
fully isolated (data, containers, scheduled jobs) from the first; a
concurrent-job load test hits the configured cap and queues gracefully
instead of degrading.
