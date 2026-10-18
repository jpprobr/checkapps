export type QualityLevel = 'A' | 'B' | 'C' | 'D' | 'E';

export interface Squad {
  id: string;
  name: string;
  description: string | null;
  status: string | null;
}

export interface AppSummary {
  id: string;
  squadId: string;
  name: string;
  criticality: string | null;
  status: string | null;
  tags: string[];
  currentQualityLevel: QualityLevel | null;
  lastSnapshotAt: string | null;
}

export interface QualityMetrics {
  coverage: number;
  bugs: number;
  vulnerabilities: number;
  codeSmells: number;
  duplications: number;
  reliability: string;
  maintainability: string;
  security: string;
}

export interface AppDetail {
  id: string;
  squadId: string;
  name: string;
  description: string | null;
  criticality: string | null;
  tags: string[];
  status: string | null;
  externalId: string | null;
  currentQualityLevel: QualityLevel | null;
  currentMetrics: QualityMetrics | null;
  lastSnapshotAt: string | null;
}

export interface AppLink {
  id: string;
  description: string;
  uri: string;
}

export interface RepositoryInfo {
  id: string;
  name: string;
  provider: string | null;
  url: string | null;
  status: string | null;
  sigla: string | null;
  siglaApp: string | null;
  role: string | null;
  source: string | null;
}

export interface UpsertRepositoryRequest {
  name: string;
  provider: string | null;
  url: string | null;
  status: string | null;
  sigla: string | null;
  siglaApp: string | null;
  role: string | null;
}

export interface SonarProjectInfo {
  id: string;
  projectKey: string;
  name: string | null;
  url: string | null;
  status: string | null;
  source: string;
}

export interface QualitySnapshot {
  id: string;
  sonarProjectId: string;
  collectedAt: string;
  source: string;
  metrics: QualityMetrics;
  qualityLevel: QualityLevel;
  assessmentRulesVersion: string;
}

export type QualityTrendDirection = 'New' | 'Improved' | 'Worsened' | 'Stable';

export interface AppQualityTrend {
  appId: string;
  appName: string;
  previousLevel: QualityLevel | null;
  currentLevel: QualityLevel;
  direction: QualityTrendDirection;
}

export interface SquadQualityEvolutionPoint {
  period: string;
  levelCounts: Partial<Record<QualityLevel, number>>;
}

export interface SquadQualityEvolution {
  points: SquadQualityEvolutionPoint[];
  appTrends: AppQualityTrend[];
}

export interface CreateSquadRequest {
  name: string;
  description: string | null;
}

export interface UpdateSquadRequest {
  name: string;
  description: string | null;
  status: string | null;
}

export interface CreateAppRequest {
  name: string;
  squadId: string;
  description: string | null;
  criticality: string | null;
  tags: string[] | null;
  externalId: string | null;
}

export interface UpdateAppRequest {
  name: string;
  squadId: string;
  description: string | null;
  criticality: string | null;
  tags: string[] | null;
  status: string | null;
  externalId: string | null;
}

export interface UpsertAppLinkRequest {
  description: string;
  uri: string;
}

// Checagens (spec 003). Status e tipos de evento chegam como os códigos do glossário.
export type CheckItemStatus = 'pendente' | 'em-andamento' | 'conforme' | 'nao-se-aplica' | 'concluido';
export type CheckStatus = 'open' | 'closed';
export type CheckEventType = 'closed' | 'reopened' | 'item-added' | 'item-removed' | 'item-status-changed';

export interface CheckProgress {
  total: number;
  done: number;
  percent: number | null;
  countsByStatus: Record<CheckItemStatus, number>;
}

export interface CheckSummary {
  id: string;
  title: string;
  category: string | null;
  author: string | null;
  startDate: string | null;
  endDate: string | null;
  status: CheckStatus;
  createdAt: string;
  closedAt: string | null;
  progress: CheckProgress;
}

export interface CheckItem {
  appId: string;
  appName: string;
  squadId: string;
  squadName: string;
  appStatus: string | null;
  status: CheckItemStatus;
  notes: string | null;
  updatedAt: string;
}

export interface CheckDetail extends CheckSummary {
  description: string | null;
  items: CheckItem[];
}

export interface AppCheck {
  checkId: string;
  title: string;
  checkStatus: CheckStatus;
  closedAt: string | null;
  itemStatus: CheckItemStatus;
  notes: string | null;
}

export interface CheckCandidate {
  appId: string;
  name: string;
  squadId: string;
  squadName: string;
  status: string | null;
  tags: string[];
}

export interface CheckEvent {
  id: string;
  appId: string | null;
  type: CheckEventType;
  fromStatus: CheckItemStatus | null;
  toStatus: CheckItemStatus | null;
  occurredAt: string;
}

export interface UpsertCheckRequest {
  title: string;
  description: string | null;
  category: string | null;
  author: string | null;
  startDate: string | null;
  endDate: string | null;
}

// Repositórios descobertos (spec 005). Status e confiança chegam como códigos.
export type TriageStatus = 'new' | 'ignored' | 'other-squad';
export type SuggestionConfidence = 'high' | 'low';

export interface Suggestion {
  role: string | null;
  name: string;
  confidence: SuggestionConfidence;
}

export interface DiscoveredRepository {
  id: string;
  provider: string;
  externalId: string;
  name: string;
  url: string | null;
  sigla: string;
  siglaApp: string | null;
  archived: boolean;
  pushedAt: string | null;
  status: TriageStatus;
  firstSeenAt: string;
  lastSeenAt: string;
  source: string;
  suggestion: Suggestion;
  /** Preenchido quando o repositório já está no catálogo (só nas listagens `catalog` e `all`). */
  catalogApp: { appId: string; appName: string } | null;
}

export interface CreateAppFromDiscoveryRequest {
  name: string;
  squadId: string;
  description: string | null;
  role: string | null;
}

export interface LinkDiscoveryRequest {
  appId: string;
  role: string | null;
}

// Scan pela tela (spec 006). O "problem" diz O QUE configurar, nunca com que valor.
export interface ScanStatus {
  enabled: boolean;
  source: string;
  ready: boolean;
  problem: string | null;
  maxResults: number;
  siglaPattern: string;
}

export type ScanOutcome = 'new' | 'updated' | 'unchanged';

export interface ScanItem {
  id: string;
  name: string;
  url: string | null;
  sigla: string;
  siglaApp: string | null;
  archived: boolean;
  pushedAt: string | null;
  outcome: ScanOutcome;
  suggestion: Suggestion;
  /** Preenchido quando o repositório já virou app — e por isso não está na caixa de entrada. */
  catalogApp: { appId: string; appName: string } | null;
}

export interface ScanReport {
  dryRun: boolean;
  /** `inCatalog` se sobrepõe às demais contagens (um item pode ser inalterado e já estar no catálogo). */
  counts: { new: number; updated: number; unchanged: number; inCatalog: number };
  items: ScanItem[];
  warnings: string[];
}
