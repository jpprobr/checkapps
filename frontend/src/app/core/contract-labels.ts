import {
  ApiEndpoint,
  ApiField,
  ApiFieldLocation,
  AppContractChanges,
  AppContracts,
  ContractChange,
  ContractChangeSeverity,
  RepositoryContractChanges,
} from './models';

/** Classificação de uma mudança, sempre em texto (nunca só por cor). */
export const SEVERITY_LABELS: Record<ContractChangeSeverity, string> = {
  quebra: 'quebra',
  atencao: 'atenção',
  segura: 'segura',
};

export const FIELD_LOCATION_LABELS: Record<ApiFieldLocation, string> = {
  path: 'rota',
  query: 'query',
  header: 'cabeçalho',
  cookie: 'cookie',
  pedido: 'pedido',
  resposta: 'resposta',
};

/** O contrato é o que foi declarado: o repositório diz onde, e não se é de um gateway. */
export const DECLARED_IN_LABELS: Record<string, string> = {
  'repositorio-de-contrato': 'declarado no repositório de contrato',
  'repositorio-da-aplicacao': 'declarado no repositório da aplicação',
};

/** Onde o campo fica, com o status e a mídia quando houver (`resposta 200`, `pedido`). */
export function fieldPlace(field: Pick<ApiField, 'location' | 'statusCode'>): string {
  const place = FIELD_LOCATION_LABELS[field.location] ?? field.location;
  return field.statusCode ? `${place} ${field.statusCode}` : place;
}

export function matchesPath(endpoint: ApiEndpoint, filter: string): boolean {
  return endpoint.path.toLowerCase().includes(filter.trim().toLowerCase());
}

export interface EndpointTagGroup {
  tag: string;
  endpoints: ApiEndpoint[];
}

/** Agrupa pela primeira tag, para um endpoint não aparecer duas vezes. Sem tag fica por último. */
export function groupByTag(endpoints: ApiEndpoint[]): EndpointTagGroup[] {
  const groups = new Map<string, ApiEndpoint[]>();
  for (const endpoint of endpoints) {
    const tag = endpoint.tags[0] ?? '';
    groups.set(tag, [...(groups.get(tag) ?? []), endpoint]);
  }
  return [...groups.entries()]
    .sort(([a], [b]) => (a === '' ? 1 : b === '' ? -1 : a.localeCompare(b)))
    .map(([tag, list]) => ({ tag, endpoints: list }));
}

export interface ChangeGroup {
  key: string;
  method: string;
  path: string;
  /** A pior classificação do grupo, que decide a ordem. */
  severity: ContractChangeSeverity;
  changes: ContractChange[];
}

const SEVERITY_ORDER: Record<ContractChangeSeverity, number> = { quebra: 0, atencao: 1, segura: 2 };

/** As mudanças por endpoint, com os que quebram primeiro. */
export function groupChanges(changes: ContractChange[]): ChangeGroup[] {
  const groups = new Map<string, ChangeGroup>();
  for (const change of changes) {
    const key = `${change.method} ${change.path}`;
    const group = groups.get(key);
    if (group) {
      group.changes.push(change);
      if (SEVERITY_ORDER[change.severity] < SEVERITY_ORDER[group.severity]) {
        group.severity = change.severity;
      }
    } else {
      groups.set(key, { key, method: change.method, path: change.path, severity: change.severity, changes: [change] });
    }
  }
  return [...groups.values()].sort(
    (a, b) => SEVERITY_ORDER[a.severity] - SEVERITY_ORDER[b.severity] || a.key.localeCompare(b.key),
  );
}

export function countBySeverity(repository: RepositoryContractChanges, severity: ContractChangeSeverity): number {
  return repository.changes.filter((c) => c.severity === severity).length;
}

function plural(count: number, singular: string, pluralForm: string): string {
  return `${count} ${count === 1 ? singular : pluralForm}`;
}

/** O resumo da linha fechada da seção. */
export function contractsSummary(contracts: AppContracts, changes: AppContractChanges): string {
  const endpoints = contracts.repositories.reduce((total, r) => total + r.endpoints.length, 0);
  if (endpoints === 0) {
    return contracts.repositories.some((r) => r.analysis) ? 'nenhum contrato encontrado' : 'ainda não analisado';
  }

  const breaking = changes.repositories.reduce((total, r) => total + countBySeverity(r, 'quebra'), 0);
  const compared = changes.repositories.some((r) => r.state === 'comparado');
  const count = plural(endpoints, 'endpoint', 'endpoints');
  if (!compared) {
    return `${count} · primeira análise do contrato`;
  }
  return breaking > 0 ? `${count} · ${plural(breaking, 'mudança que quebra', 'mudanças que quebram')}` : `${count} · nada que quebra`;
}
