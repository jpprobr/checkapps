import { Integration, IntegrationState } from './models';

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
