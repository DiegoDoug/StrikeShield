# StrikeShield Architecture

StrikeShield is an audit/orchestration platform that wraps **Strix** (an
autonomous AI pentesting agent) and a set of classic security tools behind a
single pane of glass: target management, scheduling, isolated execution,
cross-tool finding normalization, and AI-generated, audience-specific
reporting.

This document covers:
1. How Strix actually works (verified against its source), and what that
   means for integration.
2. Tech stack decision.
3. System architecture and the two distinct "AI layers" in the platform.
4. Data model.
5. Tool orchestration & isolation design.
6. Finding normalization schema and per-tool adapters.
7. AI-powered reporting design.
8. Security/legal guardrails that are non-negotiable for a product that
   launches active attacks.

See `docs/PHASED_PLAN.md` for the build order and per-phase acceptance tests.

---

## 1. How Strix works (read from source, not the marketing page)

Repo: `usestrix/strix`, Apache-2.0, Python, PyPI package `strix-agent`,
CLI entry point `strix = strix.interface.main:main`.

**Runtime model:** Strix is *not* a library you import and drive
programmatically in-process — it's a CLI tool that, on invocation, builds a
plan and executes it by spinning up **its own Docker sandbox** (via a fork of
OpenAI's `agents.sandbox` SDK — see `strix/runtime/docker_client.py`,
pinned to `openai-agents==0.14.6`). Inside that sandbox it has a full
offensive toolkit: an HTTP intercept proxy (Caido), a Playwright-driven
browser, a shell, a Python exploit sandbox, and its own recon/vuln
skill library (`strix/skills/*`). It runs a **graph of LLM agents**
(`strix/tools/agents_graph`) that plan, execute, and validate exploits
against the target, similar to a human red-team pairing session, and only
reports a finding once it has a working PoC — not just a signature match.

**Key integration facts:**

| Fact | Where verified | Why it matters for us |
|---|---|---|
| Invoked as `strix -n --target <t> --scan-mode {quick,standard,deep} --instruction "..." --max-budget-usd N --config <path>` | `strix/interface/main.py` | We can drive it entirely via CLI args from a container we launch — no custom SDK needed. |
| `-n/--non-interactive` prints findings + final report and exits with non-zero code if vulns found | README | Perfect for our orchestrator: predictable exit codes, no TUI to scrape. |
| Needs `STRIX_LLM` + `LLM_API_KEY` env vars (or `--config` JSON) | README, `strix/config/loader.py` | We store the operator's own LLM key (BYOK) per-project/org and inject it as env vars into the Strix container at launch — never bake it into an image. |
| `--max-budget-usd` stops the scan cleanly at a cost ceiling | `interface/main.py` | We surface this as a required field on every Strix step so cost is bounded and visible before launch. |
| Output lands under `strix_runs/<run-name>/`: `run.json` (run record), `penetration_test_report.md` (narrative), `vulnerabilities/*.md`, `vulnerabilities.csv`, **`findings.sarif`** | `strix/report/writer.py`, `strix/report/sarif.py` | `findings.sarif` is a first-class, schema-valid SARIF 2.1.0 document with CVSS + CWE preserved in `properties.strix`. This becomes our single ingestion path for Strix — we don't need a bespoke Strix parser, we reuse the generic SARIF adapter (see §6). |
| Findings already carry CVSS (calculated from an 8-metric breakdown via the `cvss` library), CWE, severity, PoC evidence, fix suggestions | `strix/tools/reporting/tool.py` | Strix does more per-finding enrichment out of the box than any other tool in our pipeline — it's the "gold" tier source; our correlator should prefer Strix's evidence when merging duplicate findings. |
| `--scope-mode {auto,diff,full}` + `--diff-base` for source targets | `interface/main.py` | Lets us offer "PR-diff-only" quick scans for CI-style jobs, distinct from full engagements. |
| `--resume RUN_NAME` resumes a prior run's full agent history | `interface/main.py` | We persist `run-name` + the run directory so a paused/failed StrikeShield job can be resumed instead of restarted (cost control). |
| Needs `NET_ADMIN`/`NET_RAW` caps for raw sockets (`nmap -sS` internally) and a `host.docker.internal` gateway | `docker_client.py` | Strix's own sandbox already needs elevated container caps — this reinforces that **Strix must run in its own container, isolated from our other tool containers and from the host**, never with `--network host`. |

**Conclusion:** Strix already *is* an AI orchestration layer, but scoped to
one target at a time with one flat toolkit. It has no concept of: multiple
clients/projects, scheduling, cross-scan history, cross-tool correlation, or
audience-specific reporting. That is exactly the gap StrikeShield fills.
Treat Strix as **one specialized worker in a larger playbook**, not as the
platform itself — invoke it as an isolated container step, and ingest its
SARIF output through the same normalization pipeline as every other tool.

---

## 2. Tech stack decision

### Backend: ASP.NET Core (confirmed as a good choice — here's why)

You asked to learn ASP.NET Core, and also asked me to override that if
something else fits better. It doesn't need overriding: this workload is
**I/O-orchestration + CRUD + background jobs + reporting**, not
Python-native tooling glue, because every scanning tool runs in its own
Docker container regardless of what language drives it. The backend talks to
Docker over its Engine API, reads structured files (JSON/XML/SARIF) back out,
and does CRUD + scheduling + report rendering — ASP.NET Core is very strong
at exactly that:

- **Docker.DotNet** gives first-class Docker Engine API access (create
  network, create container, attach volumes, stream logs, wait/remove) —
  no shelling out to the `docker` CLI needed.
- **SARIF is a Microsoft-designed format** with an official
  `Microsoft.CodeAnalysis.Sarif` SDK for .NET — and SARIF turns out to be
  our primary cross-tool normalization format (§6), so .NET has
  first-party tooling for the most important adapter.
- **Hangfire** (or `Quartz.NET`) gives recurring/cron scheduling, retries,
  and a built-in dashboard for free — directly serves the "scheduling"
  feature.
- **SignalR** gives real-time scan progress push to the UI with minimal code.
- **EF Core + Npgsql** for the relational domain (clients/projects/targets/
  findings/reports) with strong typing, which matters a lot once you're
  modeling compliance mappings and audit trails you'll eventually want to
  defend to an auditor.
- Strongly-typed C# is a genuine asset for a domain with strict schemas
  (CVSS vectors, CWE IDs, SARIF) where a typo in a JSON key should be a
  compile error, not a runtime surprise in a report you send a client.
- If you ever sell this, "ASP.NET Core SaaS platform" is a very normal,
  supportable, enterprise-palatable stack (identity, RBAC, licensing,
  Azure/AWS deployment story are all mature).

The one place Python would have an edge is if you wanted to **embed** Strix's
Python internals directly in-process — but Strix doesn't expose itself that
way (it's a CLI that owns its own sandbox lifecycle), so that edge doesn't
actually apply here. You lose nothing by orchestrating it from .NET.

**Decision:** ASP.NET Core 8 (LTS) Web API backend, Clean
Architecture layering (Domain / Application / Infrastructure / Api),
C#/.NET throughout the backend and orchestrator.

> Note on version: I pinned **.NET 8 LTS** rather than the newest release.
> It's supported through Nov 2026, all NuGet packages referenced below are
> confirmed-stable on it, and nothing in this design needs a newer runtime.
> Bumping the TFM later (`net8.0` → `net10.0` in `.csproj` + `global.json`)
> is a cheap, mechanical change whenever you want to track current .NET —
> it does not require replanning the architecture.

### Frontend

React + TypeScript + Vite SPA calling the ASP.NET Core Web API (+ SignalR
for live scan progress). Reasoning: findings triage tables, DAG playbook
editors, and report viewers benefit from the mature React component/charting
ecosystem, and "React + a REST/typed API" is the most hire-able and
sellable shape if this becomes a product. Blazor Server/WASM is a legitimate
alternative if you'd rather stay 100% in C# while learning — flagged as an
option, not the recommendation, since it doesn't get its own phase until
late anyway (see Phase 9).

### Data & infra services

| Concern | Choice | Why |
|---|---|---|
| Primary DB | PostgreSQL | Rich JSON(B) columns for raw tool output/SARIF blobs, mature EF Core provider (Npgsql), free, battle-tested. |
| Cache / job coordination / SignalR backplane | Redis | Cheap, standard, doubles as Hangfire storage backend option. |
| Background jobs / scheduling | Hangfire (Postgres or Redis storage) | Cron-like recurring scans, retries, dashboard, minimal code. |
| Container orchestration | Docker Engine API via `Docker.DotNet` from a dedicated **Orchestrator** worker | See §5. |
| Object storage (artifacts: screenshots, PoCs, PDFs) | Local volume in dev → S3-compatible (MinIO) in prod | Keep DB rows light; findings reference artifact URIs. |
| Report rendering | Markdown → HTML → PDF via a headless-Chromium print step (Playwright, already needed for UI e2e tests) | Avoids a second templating stack. |

---

## 3. System architecture

```
                        ┌───────────────────────────┐
                        │        React SPA          │
                        └─────────────┬─────────────┘
                                      │ REST + SignalR
                        ┌─────────────▼─────────────┐
                        │   StrikeShield.Api         │  ASP.NET Core
                        │  (Clients/Projects/Targets │
                        │   Playbooks/Scans/Findings │
                        │   Reports/Auth/Scheduling) │
                        └──────┬───────────────┬─────┘
                               │               │
                    ┌──────────▼───┐   ┌───────▼────────────┐
                    │ Postgres      │   │ Hangfire (Redis/PG)│
                    │ (domain data) │   │ scheduled/recurring│
                    └───────────────┘   └───────┬────────────┘
                                                 │ enqueues
                                        ┌────────▼─────────────┐
                                        │ StrikeShield.Orchestrator │  worker service
                                        │  - Playbook DAG executor │
                                        │  - Docker Engine API     │
                                        │  - per-step isolation    │
                                        └───────┬───────────┬─────┘
                     ┌───────────────┬──────────┤           │
                     ▼               ▼          ▼           ▼
              [nmap container] [nuclei container] ... [strix container]
                     │               │          │           │
                     └───────────────┴────┬─────┴───────────┘
                                           ▼
                              per-scan output volume (JSON/XML/SARIF/CSV)
                                           │
                                ┌──────────▼───────────┐
                                │  Normalization layer  │  (Application service,
                                │  per-tool adapters →  │   runs in Api or Orchestrator)
                                │  unified Finding model│
                                └──────────┬────────────┘
                                           │
                          ┌────────────────▼─────────────────┐
                          │   AI Orchestration Layer (ours)   │
                          │  1. Correlator (dedupe/confidence)│
                          │  2. Adaptive Planner (advisory)   │
                          │  3. Reporting Agent (LLM calls)   │
                          └────────────────┬───────────────────┘
                                           │
                                 ┌─────────▼─────────┐
                                 │  Reports (Exec/Tech/│
                                 │  Dev/Compliance)    │
                                 └─────────────────────┘
```

### The two AI layers — and why they're kept separate

It's tempting to build one giant "meta-agent" that decides everything: which
tools to run, in what order, how to interpret output, what to write in the
report. That's the wrong shape for a product you want to be able to test,
audit, and eventually sell, for three reasons: determinism (a client asking
"why did you scan this endpoint" needs a config-file answer, not "the LLM
decided to"), cost control (free-roaming agents re-planning at every step is
expensive and slow), and blast radius (an LLM autonomously deciding to fire
an active exploit tool against a new host with no human in the loop is a
liability you don't want).

So StrikeShield has a **deterministic core** with **two narrow, well-scoped
AI extension points**:

1. **Playbooks are explicit, user-editable DAGs**, not agent improvisation.
   A playbook is a graph of steps (`tool`, `config`, `image`, `dependsOn`,
   `condition`). You author/clone/edit them (JSON/YAML), same way you'd
   edit a CI pipeline. Strix itself is just one possible step type — the
   one step that happens to be internally agentic.

2. **AI orchestration layer** = three focused services, not one free agent:
   - **Correlator** — deterministic fingerprinting first (target + CWE/CVE +
     normalized location hash); only escalates genuinely ambiguous
     cross-tool matches to an LLM call for a merge/no-merge judgment. Keeps
     LLM usage small and auditable.
   - **Adaptive Planner** (optional, advisory only) — after a recon step
     (e.g., subfinder/amass/katana/nmap) completes, it may *propose*
     additions to the remaining playbook (e.g., "fingerprinted WordPress on
     3 subdomains → add these nuclei tags"). It never auto-executes; the
     proposal is a pending diff the operator approves or rejects before the
     next stage runs. This is what gives you "coordinate all the other
     tools intelligently" without giving up control.
   - **Reporting Agent** — takes the correlated, normalized finding set (not
     raw scanner output) and generates the four audience-specific documents
     (§7). This is the highest-value use of the LLM in the whole platform
     and the one place a large, expensive model call is clearly justified.

---

## 4. Data model (core entities)

```
Organization ──< User (RBAC: Owner/Admin/Analyst/Viewer)
Organization ──< Client ──< Project ──< Target
                                   └──< Engagement (a scoped body of work,
                                        has RulesOfEngagement + authorization
                                        sign-off + start/end window)
Engagement ──< ScanJob (one playbook run, scheduled or ad hoc)
ScanJob ──< PlaybookRun ──< StepRun (one container execution) ──< Artifact (raw file refs)
ScanJob ──< Finding (normalized, see §6) ──< FindingEvidence (screenshots/requests/PoC)
Finding >── CorrelationGroup (many findings → one deduped issue across tools/scans)
Engagement ──< Report (type: Executive|Technical|DevRemediation|Compliance; format: MD/HTML/PDF)
Target ──< Asset (subdomains/hosts/ports/urls/tech-fingerprints discovered by recon steps)
Playbook ──< PlaybookStep (tool, image, args template, dependsOn[], condition)
Integration (Slack/Jira/GitHub/Webhook) ── Organization
AuditLogEntry (who launched what against which target/engagement, when, from what IP)
```

**Non-negotiable fields on `Target`/`Engagement`:** `authorizationEvidenceUri`,
`approvedBy`, `approvedAt`, `scopeStart`, `scopeEnd`, `allowedScopeRules`
(CIDR/domain allow-list). The orchestrator's step-launch code path is the one
place in the whole system that **must** hard-fail (not warn) if an active-scan
step's target isn't inside an approved, currently-in-window Engagement scope.
This is cheap to build now and expensive to retrofit later, and it's the
difference between "pentesting platform" and "attack tool with no rails" —
build it in Phase 1, not as an afterthought.

---

## 5. Docker isolation design (per your requirement)

Each `ScanJob` gets:
- A dedicated Docker network (`strikeshield-scan-<jobId>`, bridge, internal
  where possible) so parallel scans/tenants never share L2/L3 space.
- Each `StepRun` = one container from a pinned image
  (`nuclei:v3.x`, `ghcr.io/zaproxy/zaproxy:stable`, our own
  `strikeshield/strix-runner` — a slim Python base with `pip install
  strix-agent`; Strix's own `containers/Dockerfile` turned out to be the
  Kali-based *sandbox* Strix spawns as a nested container, not something to
  build our runner from — see `docs/PHASED_PLAN.md` Phase 4), with:
  - `--cpus`, `--memory` limits sized per tool tier
  - a per-step timeout enforced by the orchestrator (`docker stop` + record
    `TimedOut` status), independent of any timeout the tool itself has
  - read-only root FS + tmpfs scratch where the tool allows it
  - only the capabilities that tool actually needs (`NET_ADMIN`/`NET_RAW`
    only for nmap/Strix, nothing elevated for ffuf/nuclei/nikto/trivy)
  - an output bind-mount (`/out`) that is the *only* write path collected
    after exit; container + ephemeral volume removed immediately after
    artifact collection (`docker rm -f`, `docker network rm` once the job's
    last step finishes)
- The **Orchestrator** talks to the Docker Engine over `/var/run/docker.sock`
  (Docker-outside-of-Docker). Mounting the socket is powerful — document
  and enforce that only the Orchestrator worker process gets that mount,
  never the public-facing Api container, and put a `docker-socket-proxy`
  (read/write-scoped) in front of it once this moves beyond localhost dev.

This directly satisfies "each scan runs in an isolated container" while
also giving you the primitive multi-step playbooks need: step N's asset
output (e.g. subfinder's subdomain list) is written to the shared job
volume and mounted read-only into step N+1's container as its input list.

---

## 6. Finding normalization

### Why SARIF is the backbone, not a from-scratch schema

Semgrep, CodeQL, Trivy, and **Strix itself** all emit valid SARIF 2.1.0
natively. That means **one SARIF importer** covers four of our most
important sources for free. We still need small bespoke adapters for tools
that don't speak SARIF, but they all funnel into the same internal `Finding`
shape:

| Tool | Native output | Adapter | Produces |
|---|---|---|---|
| Strix | `findings.sarif` (+ `vulnerabilities.csv`, `run.json`) | **SARIF importer** | Finding (CVSS+CWE already populated, PoC in `properties`) |
| Semgrep | `--sarif` | **SARIF importer** | Finding (SAST, file/line location) |
| CodeQL | `codeql database analyze --format=sarif-latest` | **SARIF importer** | Finding (SAST) |
| Trivy | `--format sarif` | **SARIF importer** | Finding (SCA/container/IaC) |
| Nuclei | `-jsonl` (JSON Lines) | Nuclei adapter | Finding (template-id → title, `info.classification.cwe-id`/`cve-id`, severity) |
| OWASP ZAP | `-J report.json` (baseline/full-scan scripts) | ZAP adapter | Finding (alert → title, `cweid`, `riskdesc` → severity, `instances[]` → evidence) |
| Nikto | `-Format json` | Nikto adapter | Finding, severity defaulted to Low/Info (Nikto doesn't emit CVSS/CWE) |
| ffuf | `-of json` | ffuf adapter | **Asset**, not Finding (discovered path/file → feeds next step + becomes an Info finding only if sensitive-path heuristics match, e.g. `.git/`, `.env`) |
| nmap | `-oX` | nmap adapter | **Asset** (host/port/service/version) + Finding only for `--script vuln` hits that report a CVE |
| Amass / Subfinder | text/JSON subdomain list | Recon adapter | **Asset** (subdomain), feeds subsequent steps' target list |
| Katana | JSONL crawled URLs | Recon adapter | **Asset** (URL), feeds ffuf/nuclei/ZAP target list |

Internal `Finding` shape (superset, adapters populate what they can):

```
id, correlationGroupId, scanJobId, stepRunId, sourceTool,
title, description, severity (Critical|High|Medium|Low|Info),
cvssVector, cvssScore, cweIds[], cveIds[], owaspCategory, mitreAttackTechniques[],
affectedAsset (host/url/file/line/port), evidence[] (request/response/screenshot/log refs),
pocCode, reproSteps[], recommendedFix, verificationSteps[],
status (New|Confirmed|FalsePositive|Fixed|AcceptedRisk|Regressed),
dedupeFingerprint, confidence, firstSeenAt, lastSeenAt, rawArtifactUri
```

`dedupeFingerprint` = stable hash of `(normalizedTarget, cweId ?? cveId ?? templateId, normalizedLocation)`.
The Correlator groups on this deterministically first; the LLM-assisted path
only runs for the residual set where two findings share a target and
severity but disagree on everything else worth a human/LLM glance.

---

## 7. AI-powered reporting

Input: one `CorrelationGroup` set for a completed `Engagement`. Output: four
documents, each a Markdown → HTML → PDF pipeline, generated from the *same*
correlated finding set with a different system prompt / audience template
per document type:

- **Executive Summary** — business risk framing, no jargon, findings grouped
  by business impact rather than technical category, a risk trend chart if
  history exists.
- **Technical Report** — full finding detail: CVSS vector breakdown, repro
  steps, evidence, source tool provenance, MITRE ATT&CK technique mapping.
- **Developer Remediation Guide** — grouped by codebase/component instead of
  severity, PoC + concrete fix diff/snippet + verification steps a dev can
  run locally, links to the exact file/line from the SAST adapters.
- **Compliance Mapping** — a matrix view: OWASP Top 10 category × CWE ×
  finding, plus a MITRE ATT&CK technique coverage table. Framed as "what we
  tested and mapped," not a certification.

Every finding, regardless of report type, keeps the fields you listed:
business impact, risk rating, CVSS score, reproduction steps, screenshots,
PoC code, recommended fix, and post-remediation verification steps — those
live on the normalized `Finding`/`FindingEvidence` records so all four report
generators pull from one source of truth instead of re-deriving it.

The Reporting Agent calls the LLM with **structured output** (JSON
schema/function-calling) constrained to the report template's required
sections, so a malformed or incomplete LLM response fails validation instead
of silently shipping a report missing a CVSS score.

---

## 8. Guardrails (build these, don't bolt them on later)

- Hard scope/authorization gate before any active-scan step can be queued
  (§4) — enforced server-side in the Orchestrator, not just UI-level.
- Full audit log of who launched what, against what target, under which
  engagement authorization, and the resulting containers' IDs/lifetimes.
- BYOK LLM keys and tool credentials stored per-organization (encrypted at
  rest), injected as container env vars at launch time only, never written
  into images or logs.
- Cost ceilings (`--max-budget-usd` for Strix; a per-playbook LLM-token
  budget for the Correlator/Reporting Agent) enforced before launch, visible
  in the UI.
- Rate/concurrency limits per organization on how many scan jobs can run
  simultaneously, to keep this from doubling as an accidental DoS tool
  against someone else's infrastructure if scope is ever misconfigured.
