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

/** Repositório do catálogo sem descoberto correspondente: cadastrado à mão ou pelo seed (spec 010). */
export interface CatalogRepository extends RepositoryInfo {
  appId: string;
  appName: string;
  squadId: string | null;
  squadName: string | null;
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
  catalogApp: { appId: string; appName: string; squadId: string | null; squadName: string | null } | null;
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
  catalogApp: { appId: string; appName: string; squadId: string | null; squadName: string | null } | null;
}

export interface ScanReport {
  dryRun: boolean;
  /** `inCatalog` se sobrepõe às demais contagens (um item pode ser inalterado e já estar no catálogo). */
  counts: { new: number; updated: number; unchanged: number; inCatalog: number };
  items: ScanItem[];
  warnings: string[];
}

// ---- Dependências e versão do .NET (spec 013) -----------------------------------------------------------------------
// Situação da análise e confiança circulam como códigos em texto; os rótulos ficam em core/dependency-labels.ts.

export type Confidence = 'alta' | 'media' | 'baixa';

export type AnalysisStatus = 'ok' | 'parcial' | 'sem-scanner' | 'falha';

export interface DependencyFact {
  id: string;
  ecosystem: string;
  kind: 'package' | 'framework';
  name: string;
  version: string | null;
  projectPath: string;
  sourceFile: string;
  origin: string;
  confidence: Confidence;
  confidenceNote: string | null;
  scannerId: string;
  scannerVersion: string;
  /** Calculado na leitura a partir do prefixo configurado; nunca gravado. */
  isInternal: boolean;
}

export interface RepositoryAnalysis {
  id: string;
  analyzedAt: string;
  commitSha: string | null;
  status: AnalysisStatus;
  warnings: string[];
}

export interface RepositoryDependencies {
  repositoryId: string;
  repositoryName: string;
  repositoryStatus: string | null;
  role: string | null;
  url: string | null;
  sigla: string | null;
  /** A análise corrente; nula se o repositório nunca foi analisado. */
  analysis: RepositoryAnalysis | null;
  /** Tentativa que falhou depois da corrente. */
  lastFailure: RepositoryAnalysis | null;
  frameworks: DependencyFact[];
  packages: DependencyFact[];
}

export interface AppDependencies {
  appId: string;
  internalPrefixesConfigured: boolean;
  repositories: RepositoryDependencies[];
}

export interface PackageUsage {
  appId: string;
  appName: string;
  squadId: string | null;
  squadName: string | null;
  repositoryId: string;
  repositoryName: string;
  repositoryStatus: string | null;
  name: string;
  version: string | null;
  projectPath: string;
  confidence: Confidence;
  confidenceNote: string | null;
  isInternal: boolean;
  analyzedAt: string;
}

export interface PackageVersionSummary {
  name: string;
  version: string | null;
  appCount: number;
  isInternal: boolean;
}

export interface AnalysisCoverage {
  /** Repositórios catalogados que nunca foram analisados: a resposta pode estar incompleta. */
  unanalyzedRepositories: number;
  latestAnalyzedAt: string | null;
}

export interface PackageSearchResult {
  usages: PackageUsage[];
  versions: PackageVersionSummary[];
  coverage: AnalysisCoverage;
  internalPrefixesConfigured: boolean;
}

export interface FrameworkApp {
  appId: string;
  appName: string;
  repositories: string[];
}

export interface FrameworkCell {
  /** Nulo = "sem informação". */
  framework: string | null;
  squadId: string | null;
  squadName: string | null;
  apps: FrameworkApp[];
}

export interface FrameworkOverview {
  cells: FrameworkCell[];
  coverage: AnalysisCoverage;
}


// ---- Integrações declaradas (spec 014) ------------------------------------------------------------------------------
// Estado, ambiente e confiança circulam como códigos em texto; os rótulos ficam em core/integration-labels.ts.
// Nenhum campo traz o valor bruto lido do arquivo: só o endereço já normalizado.

export type IntegrationState = 'resolvida' | 'parametrizada' | 'nao-resolvida';

export interface Integration {
  id: string;
  mechanism: string;
  scheme: string | null;
  /** Nulo quando não resolvida; pode ser um modelo com {env}. */
  host: string | null;
  port: number | null;
  basePath: string | null;
  /** Seção de configuração (ex.: IPagamentosServiceCliente). */
  reference: string;
  configKey: string;
  /** Nome do ambiente como está no arquivo; nulo no arquivo base. */
  environment: string | null;
  /** Categoria normalizada (dev, hom, prod); nula se o nome do ambiente não é conhecido. */
  environmentKind: string | null;
  state: IntegrationState;
  projectPath: string;
  sourceFile: string;
  origin: string;
  confidence: Confidence;
  confidenceNote: string | null;
  scannerId: string;
  scannerVersion: string;
}

export interface IntegrationSection {
  reference: string;
  integrations: Integration[];
  /** App destino por id de integração; só as confirmadas (spec 017). */
  destinations: Record<string, HostLinkMatch>;
  /** Serviço externo por id de integração, onde não há app confirmado (spec 019). */
  externals: Record<string, HostExternalMatch>;
}

export interface RepositoryIntegrations {
  repositoryId: string;
  repositoryName: string;
  repositoryStatus: string | null;
  role: string | null;
  url: string | null;
  sigla: string | null;
  /** A análise corrente; nula se o repositório nunca foi analisado. */
  analysis: RepositoryAnalysis | null;
  /** Tentativa que falhou depois da corrente. */
  lastFailure: RepositoryAnalysis | null;
  sections: IntegrationSection[];
}

export interface AppIntegrations {
  appId: string;
  repositories: RepositoryIntegrations[];
}

export interface SquadRef {
  id: string;
  name: string;
}

export interface HostSummary {
  /** host[:porta] literal: a identidade do host nesta fase. */
  key: string;
  host: string;
  port: number | null;
  /** O host tem {env}: depende do ambiente. */
  isTemplate: boolean;
  appCount: number;
  repositoryCount: number;
  squads: SquadRef[];
  references: string[];
  environmentKinds: string[];
  latestAnalyzedAt: string;
  link: HostLinkInfo;
}

export interface HostList {
  hosts: HostSummary[];
  coverage: AnalysisCoverage;
}

export interface HostUsage {
  appId: string;
  appName: string;
  squadId: string | null;
  squadName: string | null;
  repositoryId: string;
  repositoryName: string;
  repositoryStatus: string | null;
  integration: Integration;
  analyzedAt: string;
}

export interface HostDetail {
  key: string;
  host: string;
  port: number | null;
  isTemplate: boolean;
  usages: HostUsage[];
  link: HostLinkInfo;
}

// ---- Vínculo host→app (spec 017): decisão humana de que um host é o app que recebe a chamada ---------------------------

export type HostLinkState = 'confirmado' | 'rejeitado';
/** Filtro da lista de hosts; 'sem-vinculo' = nenhum vínculo (nem rejeito). */
export type HostLinkStatus = 'sem-vinculo' | 'externo' | HostLinkState;

/** Host decidido como serviço de fora do catálogo (spec 019). */
export interface HostExternalMatch {
  externalId: string;
  hostKey: string;
  name: string | null;
  via: 'exact' | 'template';
  template: string | null;
  decidedAt: string;
  note: string | null;
}

export type BatchHostState = HostLinkState | 'externo';

export interface BatchHostLinkRequest {
  hostKeys: string[];
  state: BatchHostState;
  /** Obrigatório, exceto em 'externo'. */
  appId: string | null;
  /** Nome do serviço externo (só em 'externo'). */
  name: string | null;
  note: string | null;
}

export interface BatchHostResult {
  hostKey: string;
  ok: boolean;
  error: string | null;
}

export interface HostLinkMatch {
  linkId: string;
  hostKey: string;
  appId: string;
  appName: string;
  state: HostLinkState;
  /** 'exact' = vínculo feito neste host; 'template' = feito no modelo {env} que o cobre. */
  via: 'exact' | 'template';
  /** O modelo usado, quando via é 'template'. */
  template: string | null;
  decidedAt: string;
  note: string | null;
}

export interface HostLinkInfo {
  status: HostLinkStatus;
  confirmed: HostLinkMatch | null;
  rejected: HostLinkMatch[];
  external: HostExternalMatch | null;
}

/** Um vínculo gravado, com se o host ainda aparece nas análises correntes (spec 017). */
export interface HostLinkRecord {
  id: string;
  hostKey: string;
  isTemplate: boolean;
  appId: string;
  appName: string;
  state: HostLinkState;
  origin: string;
  decidedAt: string;
  note: string | null;
  seen: boolean;
}

export interface SetHostExternalRequest {
  name: string | null;
  note: string | null;
}

export interface SetHostLinkRequest {
  appId: string;
  state: HostLinkState;
  note: string | null;
}

// ---- Mapa de aplicações (spec 020): grafo "quem chama quem", derivado das integrações e das decisões humanas ---------------

/** 'sem-app' é um nó só, que reúne todos os hosts sem decisão. */
export type MapNodeKind = 'app' | 'externo' | 'sem-app';

export interface MapNode {
  id: string;
  kind: MapNodeKind;
  label: string;
  squadId: string | null;
  squadName: string | null;
  qualityLevel: QualityLevel | null;
}

export interface MapEdge {
  fromId: string;
  toId: string;
  /** dev, hom, prod; nulo = arquivo base ou ambiente desconhecido (vale para todos). */
  environmentKind: string | null;
  count: number;
  hosts: string[];
  /** O app chama o próprio host. */
  selfLoop: boolean;
}

export interface NoScannerRepository {
  repositoryId: string;
  repositoryName: string;
  appId: string;
  appName: string;
}

export interface MapCoverage {
  unanalyzedRepositories: number;
  latestAnalyzedAt: string | null;
  /** Repositórios para os quais nenhum scanner se aplica: o mapa não os enxerga. */
  noScannerRepositories: NoScannerRepository[];
}

export interface AppMap {
  nodes: MapNode[];
  edges: MapEdge[];
  coverage: MapCoverage;
}
