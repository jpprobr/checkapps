import { AnalysisStatus, Confidence } from './models';

/** Confiança sempre em texto, nunca só por cor. */
export const CONFIDENCE_LABELS: Record<Confidence, string> = {
  alta: 'alta',
  media: 'média',
  baixa: 'baixa',
};

export const ANALYSIS_STATUS_LABELS: Record<AnalysisStatus, string> = {
  ok: 'ok',
  parcial: 'parcial',
  'sem-scanner': 'sem scanner aplicável',
  falha: 'falha',
};

/** Os 7 primeiros caracteres bastam para reconhecer um commit. */
export function shortSha(sha: string | null): string {
  return sha ? sha.slice(0, 7) : '—';
}
