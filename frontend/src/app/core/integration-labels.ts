import { AppIntegrations, HostLinkInfo, HostLinkStatus, Integration, IntegrationState } from './models';

/** Estado do endereço, sempre em texto (nunca só por cor). */
export const INTEGRATION_STATE_LABELS: Record<IntegrationState, string> = {
  resolvida: 'resolvida',
  parametrizada: 'parametrizada (modelo)',
  'nao-resolvida': 'não resolvida',
};

export const MECHANISM_LABELS: Record<string, string> = {
  config: 'configuração',
};

export function mechanismLabel(mechanism: string): string {
  return MECHANISM_LABELS[mechanism] ?? mechanism;
}

/** O endereço normalizado: esquema, host (ou modelo), porta e caminho base; "—" quando não foi resolvido. */
export function integrationAddress(i: Integration): string {
  if (!i.host) {
    return '—';
  }
  const port = i.port ? `:${i.port}` : '';
  return `${i.scheme ?? 'https'}://${i.host}${port}${i.basePath ?? ''}`;
}

/** Ambiente do arquivo: o nome como está no arquivo e, se ele for conhecido, a categoria. */
export function environmentLabel(i: Pick<Integration, 'environment' | 'environmentKind'>): string {
  if (!i.environment) {
    return 'base (sem ambiente)';
  }
  return i.environmentKind && i.environmentKind.toLowerCase() !== i.environment.toLowerCase()
    ? `${i.environment} (${i.environmentKind})`
    : i.environment;
}

/** As notas informativas da análise vêm na lista de avisos, com este prefixo. */
const NOTE_PREFIX = 'nota: ';

export function isNote(warning: string): boolean {
  return warning.startsWith(NOTE_PREFIX);
}

export function noteText(warning: string): string {
  return isNote(warning) ? warning.slice(NOTE_PREFIX.length) : warning;
}

/** Resumo de uma linha da seção "Integrações" do app, mostrado também com a seção fechada (spec 016). */
export function integrationsSummary(data: AppIntegrations): string {
  const repositories = data.repositories;
  if (repositories.length === 0) {
    return 'sem repositórios cadastrados';
  }

  const analyzed = repositories.filter((r) => r.analysis);
  const failed = repositories.some((r) => r.lastFailure);
  if (analyzed.length === 0) {
    return failed ? 'a última tentativa de análise falhou' : 'ainda não analisado';
  }

  const sections = analyzed.flatMap((r) => r.sections);
  const integrations = sections.flatMap((s) => s.integrations);
  if (integrations.length === 0) {
    return analyzed.every((r) => r.analysis!.status === 'sem-scanner') ? 'nenhum scanner se aplica' : 'nenhuma integração declarada';
  }

  const parts = [
    `${integrations.length} ${integrations.length === 1 ? 'integração' : 'integrações'} em ${sections.length} ${sections.length === 1 ? 'seção' : 'seções'}`,
  ];
  const unresolved = integrations.filter((i) => i.state === 'nao-resolvida').length;
  if (unresolved > 0) {
    parts.push(`${unresolved} ${unresolved === 1 ? 'não resolvida' : 'não resolvidas'}`);
  }
  if (failed) {
    parts.push('última tentativa falhou');
  }
  return parts.join(' · ');
}

/** Rótulos do filtro "Vínculo" da lista de hosts (spec 017). */
export const LINK_STATUS_LABELS: Record<HostLinkStatus, string> = {
  'sem-vinculo': 'Sem app',
  confirmado: 'Confirmado',
  externo: 'Externo',
  rejeitado: 'Só rejeitado',
};

/** Texto da coluna "App": sempre em palavras, nunca só por cor. */
export function linkSummary(link: HostLinkInfo): string {
  if (link.confirmed) {
    return `→ ${link.confirmed.appName}`;
  }
  if (link.external) {
    return link.external.name ? `externo: ${link.external.name}` : 'externo';
  }
  if (link.rejected.length > 0) {
    return `rejeitado: ${link.rejected.map((r) => r.appName).join(', ')}`;
  }
  return 'sem app';
}
