// Mirrors StrikeShield.Application's *Models.cs records and
// StrikeShield.Domain's Enums/*.cs — kept in lockstep by hand since the
// API has no generated OpenAPI client wired up yet. JsonStringEnumConverter
// is registered in Program.cs, so every enum below travels over the wire
// as its string name, not its numeric value.

export type UserRole = 'Owner' | 'Admin' | 'Analyst' | 'Viewer'

export type TargetType = 'Url' | 'Domain' | 'IpAddress' | 'Repository' | 'LocalPath'

export type AssetType = 'Host' | 'Port' | 'Url' | 'Subdomain' | 'TechFingerprint'

export type FindingSeverity = 'Info' | 'Low' | 'Medium' | 'High' | 'Critical'

export type FindingStatus = 'New' | 'Confirmed' | 'FalsePositive' | 'Fixed' | 'AcceptedRisk' | 'Regressed'

export type ScanJobStatus = 'Queued' | 'Running' | 'Completed' | 'Failed' | 'TimedOut' | 'AwaitingApproval'

export type StepRunStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'TimedOut' | 'Skipped'

export type StepCondition = 'OnSuccess' | 'Always'

export type PlaybookAmendmentStatus = 'Pending' | 'Approved' | 'Rejected'

export type IntegrationType = 'Slack' | 'Webhook' | 'GitHub'

export type ScanScheduleFireOutcome = 'Triggered' | 'Skipped'

export type ReportTypeSlug = 'executive' | 'technical' | 'dev-remediation' | 'compliance'

export interface AuthResponse {
  token: string
  expiresAtUtc: string
  userId: string
  email: string
  role: UserRole
}

export interface OrganizationResponse {
  id: string
  name: string
  createdAt: string
}

export interface ClientResponse {
  id: string
  organizationId: string
  name: string
  createdAt: string
}

export interface ProjectResponse {
  id: string
  clientId: string
  name: string
  createdAt: string
}

export interface TargetResponse {
  id: string
  projectId: string
  type: TargetType
  value: string
  createdAt: string
}

export interface EngagementResponse {
  id: string
  projectId: string
  name: string
  rulesOfEngagement: string | null
  authorizationEvidenceUri: string | null
  approvedBy: string | null
  approvedAt: string | null
  scopeStart: string
  scopeEnd: string
  allowedScopeRules: string[]
  isApproved: boolean
  createdAt: string
}

export interface PlaybookStepResponse {
  id: string
  order: number
  stepKey: string
  toolName: string
  imageRepository: string
  imageTag: string
  timeoutSeconds: number
  dependsOn: string[]
  condition: StepCondition
}

export interface PlaybookResponse {
  id: string
  slug: string
  name: string
  description: string | null
  steps: PlaybookStepResponse[]
}

export interface ScanJobResponse {
  id: string
  engagementId: string
  targetId: string
  playbookId: string
  playbookName: string
  status: ScanJobStatus
  createdAt: string
}

export interface ArtifactResponse {
  id: string
  fileName: string
  contentType: string
  content: string
  createdAt: string
}

export interface StepRunResponse {
  id: string
  scanJobId: string
  playbookStepId: string
  stepKey: string
  toolName: string
  dependsOn: string[]
  status: StepRunStatus
  exitCode: number | null
  errorMessage: string | null
  startedAt: string | null
  completedAt: string | null
  artifacts: ArtifactResponse[]
}

export interface AssetResponse {
  id: string
  type: AssetType
  value: string
  metadata: string | null
  discoveredByStepRunId: string | null
  createdAt: string
}

export interface FindingResponse {
  id: string
  scanJobId: string
  stepRunId: string
  correlationGroupId: string | null
  sourceTool: string
  title: string
  description: string | null
  severity: FindingSeverity
  cweIds: string[]
  cveIds: string[]
  affectedAsset: string
  status: FindingStatus
  dedupeFingerprint: string
  firstSeenAt: string
  lastSeenAt: string
}

export interface PlaybookAmendmentResponse {
  id: string
  scanJobId: string
  proposedByStepRunId: string
  targetPlaybookStepId: string
  targetStepKey: string
  rationale: string
  proposedArgsTemplate: string
  status: PlaybookAmendmentStatus
  createdAt: string
  decidedAt: string | null
  decidedBy: string | null
}

export interface ScanScheduleResponse {
  id: string
  engagementId: string
  targetId: string
  playbookId: string
  playbookName: string
  cronExpression: string
  enabled: boolean
  createdBy: string
  createdAt: string
  lastFiredAt: string | null
  lastFireOutcome: ScanScheduleFireOutcome | null
  lastSkipReason: string | null
  lastTriggeredScanJobId: string | null
}

export interface IntegrationResponse {
  id: string
  organizationId: string
  type: IntegrationType
  enabled: boolean
  notifyOnScanCompletion: boolean
  notifyOnCriticalFinding: boolean
  webhookUrl: string | null
  gitHubRepository: string | null
  hasGitHubAccessToken: boolean
  createdAt: string
}

// ExceptionHandlingMiddleware always writes { status, detail }; a handful
// of framework-level failures (malformed JSON body, [Authorize] 401s with
// no body) never reach it, so every field here stays optional.
export interface ApiErrorBody {
  status?: number
  detail?: string
  title?: string
  error?: string
}
