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

Status: **Phases 0 and 1 implemented** (see repo root). Phase 2+ are
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
curl -s http://localhost:8080/health | jq
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

## Phase 2 — Scan orchestrator + first real tool (Nuclei) against a safe target

**Goal:** Prove the Docker-orchestration primitive end to end with the
simplest tool before adding Strix or multi-step DAGs.

**Deliverables:**
- `StrikeShield.Orchestrator` worker service using `Docker.DotNet`.
- Add `juice-shop` (OWASP Juice Shop, intentionally vulnerable) as a compose
  service — this becomes the standing safe test target for every phase from
  here on.
- `Playbook`/`PlaybookStep`/`ScanJob`/`StepRun` entities (single-step
  playbooks only in this phase: "Nuclei quick scan").
- Orchestrator launches a pinned `projectdiscovery/nuclei` container against
  `http://juice-shop:3000`, with per-step timeout + resource limits, output
  bind-mounted, container removed after collection.
- Scan status endpoint (`Queued → Running → Completed/Failed/TimedOut`).

**Test it:**
```bash
docker compose up --build
curl -X POST localhost:8080/api/scan-jobs -d '{"targetId":"...","playbook":"nuclei-quick"}'
curl localhost:8080/api/scan-jobs/{id}   # poll until Completed
docker ps -a | grep strikeshield-step    # expect: nothing left running/lingering
```
**Acceptance:** scan reaches `Completed`, raw Nuclei JSON artifact is
retrievable via API, and `docker ps -a` shows no leaked containers/networks
after completion.

---

## Phase 3 — Normalization layer + SARIF + 3 more tools

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
curl localhost:8080/api/scan-jobs/{id}/findings | jq 'length, .[0]'
```
**Acceptance:** findings from all 3 tools appear in one `Finding` table with
populated `severity`/`cweIds` where the source tool provides them; a finding
flagged by both Nuclei and ZAP for the same CWE+endpoint collapses into a
single `CorrelationGroup` (verified by a fixture-based test with known
overlapping sample output, not by relying on live-scan nondeterminism).

---

## Phase 4 — Strix integration (the AI pentesting engine)

**Goal:** Wire in the actual AI hacker, budget-capped, BYOK.

**Deliverables:**
- `strikeshield/strix-runner` image (built from Strix's own
  `containers/Dockerfile`, or `pip install strix-agent` inside a slim base —
  pick whichever the phase spike shows is more reliable to pin).
- Orchestrator step type `strix`: injects `STRIX_LLM`/`LLM_API_KEY` from the
  organization's encrypted BYOK secret, runs
  `strix -n --target <t> --scan-mode quick --max-budget-usd <cap> --config <mounted json>`,
  waits, then ingests `findings.sarif` via the Phase 3 SARIF importer (no new
  parser needed) plus `run.json` for cost/run metadata.
- Cost ceiling surfaced and enforced in the UI/API before launch.

**Test it:**
```bash
export LLM_API_KEY=... # your own key
docker compose up --build
# run playbook "strix-quick" against juice-shop with max_budget_usd=1.00
curl localhost:8080/api/scan-jobs/{id}/findings | jq '[.[] | select(.sourceTool=="strix")]'
```
**Acceptance:** Strix findings appear with `cvssScore`, `cweIds`, and PoC
populated; the run stops at/under the configured budget (visible in
`run.json`); no leftover containers after the run.

---

## Phase 5 — Multi-tool DAG playbooks + asset hand-off

**Goal:** Recon output actually feeds later steps — the "orchestrate
multiple tools" promise, not just "run tools in parallel."

**Deliverables:**
- `PlaybookStep.dependsOn[]` + `condition` support in the Orchestrator's DAG
  executor.
- Recon adapters: Subfinder/Amass (subdomains → `Asset`), Katana
  (crawled URLs → `Asset`).
- Add ffuf and Nikto as step types.
- A per-job shared read-only volume so step N+1 can consume step N's asset
  list as its target/wordlist input (e.g., subfinder's subdomains become
  nmap's target list; katana's URLs become ffuf's/nuclei's input list).
- A default "Full External Recon+Scan" playbook shipped as a template.

**Test it:**
```bash
docker compose up --build
# run "full-external-recon" against a scoped local multi-subdomain test target
curl localhost:8080/api/scan-jobs/{id}/steps   # expect ordered step graph w/ per-step status
curl localhost:8080/api/scan-jobs/{id}/assets  # expect subdomains/urls discovered by early steps
```
**Acceptance:** step graph executes in dependency order (visible via
step-run timestamps), and at least one downstream step's actual container
args are demonstrably built from an upstream step's asset output (not
hardcoded) — assert this in an integration test, not just by eyeballing logs.

---

## Phase 6 — AI orchestration layer: correlation + adaptive planning

**Goal:** The two LLM extension points from `ARCHITECTURE.md` §3 that go
beyond Strix itself.

**Deliverables:**
- LLM-assisted Correlator escalation path: ambiguous cross-tool matches
  (same target/severity, different everything else) get one batched LLM
  call for a merge/no-merge + confidence judgment; deterministic matches
  never touch the LLM (cost control, tested via fixture counts).
- Adaptive Planner: after a recon step, proposes playbook amendments (e.g.
  tech-fingerprint-driven nuclei tag additions) as a **pending, human-
  approved diff** — never auto-executed.
- API endpoints to view/approve/reject a pending playbook amendment.

**Test it:**
```bash
docker compose up --build
# seed a fixture set of 3 overlapping findings from different tools
dotnet test --filter Correlator
# run a recon-only playbook against juice-shop, expect a pending amendment
curl localhost:8080/api/scan-jobs/{id}/amendments
```
**Acceptance:** fixture test shows overlapping findings collapse to one
`CorrelationGroup` with combined evidence; a live recon run produces a
pending (not auto-run) amendment that a human must approve before it affects
the playbook.

---

## Phase 7 — AI-powered, audience-specific reporting

**Goal:** The reporting feature list from your prompt, verbatim.

**Deliverables:**
- Reporting Agent service: structured-output LLM calls (JSON
  schema-constrained) producing the 4 report types from
  `ARCHITECTURE.md` §7, each with every required per-finding field (business
  impact, risk rating, CVSS, repro steps, screenshot refs, PoC, fix,
  verification steps).
- Markdown → HTML → PDF render pipeline (Playwright print-to-PDF).
- `GET /api/engagements/{id}/reports/{type}` returning the rendered artifact.

**Test it:**
```bash
docker compose up --build
curl localhost:8080/api/engagements/{id}/reports/executive -o exec.pdf
curl localhost:8080/api/engagements/{id}/reports/technical -o tech.pdf
curl localhost:8080/api/engagements/{id}/reports/dev-remediation -o dev.pdf
curl localhost:8080/api/engagements/{id}/reports/compliance -o compliance.pdf
```
**Acceptance:** all 4 PDFs generate from one completed engagement; an
automated schema-validation test asserts every finding in the technical
report contains all required fields (fails the build if the LLM response
was incomplete, rather than shipping a partial report).

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
