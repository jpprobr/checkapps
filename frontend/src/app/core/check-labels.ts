import { CheckItemStatus } from './models';

export const CHECK_ITEM_STATUSES: CheckItemStatus[] = ['pendente', 'em-andamento', 'conforme', 'nao-se-aplica', 'concluido'];

export const CHECK_ITEM_STATUS_LABELS: Record<CheckItemStatus, string> = {
  pendente: 'Pendente',
  'em-andamento': 'Em andamento',
  conforme: 'Conforme',
  'nao-se-aplica': 'Não se aplica',
  concluido: 'Concluído',
};

/** Percentual com zero apps não existe: mostra "—", não 0%. */
export function formatPercent(percent: number | null): string {
  return percent === null ? '—' : `${percent.toLocaleString('pt-BR')}%`;
}
