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
  links: string[];
  status: string | null;
  externalId: string | null;
  currentQualityLevel: QualityLevel | null;
  currentMetrics: QualityMetrics | null;
  lastSnapshotAt: string | null;
}

export interface RepositoryInfo {
  id: string;
  name: string;
  provider: string | null;
  url: string | null;
  status: string | null;
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
