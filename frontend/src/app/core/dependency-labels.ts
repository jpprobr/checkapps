import { AnalysisStatus, AppDependencies, Confidence } from './models';

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

/** Resumo de uma linha da seção "Pacotes" do app, mostrado também com a seção fechada (spec 016). */
export function packagesSummary(deps: AppDependencies): string {
  const repositories = deps.repositories;
  if (repositories.length === 0) {
    return 'sem repositórios cadastrados';
  }

  const analyzed = repositories.filter((r) => r.analysis);
  const failed = repositories.some((r) => r.lastFailure);
  if (analyzed.length === 0) {
    return failed ? 'a última tentativa de análise falhou' : 'ainda não analisado';
  }

  const frameworks = [...new Set(analyzed.flatMap((r) => r.frameworks.map((f) => f.name)))];
  const packages = analyzed.reduce((total, r) => total + r.packages.length, 0);
  if (frameworks.length === 0 && packages === 0) {
    return analyzed.every((r) => r.analysis!.status === 'sem-scanner')
      ? 'nenhum scanner se aplica'
      : 'nenhum framework ou pacote encontrado';
  }

  const parts: string[] = [];
  if (frameworks.length > 0) {
    parts.push(frameworks.join(', '));
  }
  parts.push(`${packages} ${packages === 1 ? 'pacote' : 'pacotes'}`);
  if (analyzed.some((r) => r.analysis!.status === 'parcial')) {
    parts.push('parcial');
  }
  if (failed) {
    parts.push('última tentativa falhou');
  }
  return parts.join(' · ');
}
