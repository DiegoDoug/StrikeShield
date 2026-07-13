# StrikeShield.Domain

Core domain entities and business rules (Organization, Client, Project,
Target, Engagement, ScanJob, Finding, Playbook, ...).

Empty in Phase 0 by design — entities land in Phase 1 (see
`docs/PHASED_PLAN.md`). Kept as its own project from the start so the
dependency direction (Domain has no dependencies on Application/
Infrastructure/Api) is enforced by the project graph, not just convention.
