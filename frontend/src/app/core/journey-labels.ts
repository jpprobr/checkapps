import { ImpactComparison, JourneyImpact, JourneySummary, StepResolution } from './models';

/** Estado do passo contra o contrato corrente, sempre em texto (nunca só por cor). */
export const STEP_RESOLUTION_LABELS: Record<StepResolution, string> = {
  catalogado: 'no contrato',
  'fora-do-contrato': 'fora do contrato',
  'app-sem-contrato': 'app sem contrato analisado',
};

export const COMPARISON_LABELS: Record<ImpactComparison, string> = {
  comparado: 'comparado',
  'primeira-analise': 'primeira análise: ainda não há com o que comparar',
  'sem-contrato': 'sem contrato analisado: não há com o que comparar',
};

/** Sugestões do campo de nível, que continua texto livre (decisão de 2026-11-08, a validar com o uso). */
export const LEVEL_SUGGESTIONS = ['plataforma', 'sistema', 'jornada'];

export const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'];

/** Mensagem de erro da API (texto simples nos 400, 404 e 409) ou a padrão. */
export function messageOf(err: unknown, fallback: string): string {
  const body = (err as { error?: unknown } | null)?.error;
  return typeof body === 'string' && body.length > 0 ? body : fallback;
}

export interface TreeRow {
  journey: JourneySummary;
  depth: number;
  expanded: boolean;
  /** Tem filhos: mostra o botão de expandir ou recolher. */
  expandable: boolean;
}

export interface TreeFilter {
  search: string;
  squadId: string;
}

function matches(journey: JourneySummary, filter: TreeFilter): boolean {
  const text = filter.search.trim().toLowerCase();
  const textOk =
    text === '' ||
    journey.name.toLowerCase().includes(text) ||
    (journey.level ?? '').toLowerCase().includes(text) ||
    journey.tags.some((t) => t.toLowerCase().includes(text));
  return textOk && (filter.squadId === '' || journey.squadId === filter.squadId);
}

export function isFiltering(filter: TreeFilter): boolean {
  return filter.search.trim() !== '' || filter.squadId !== '';
}

/**
 * A árvore em linhas, na ordem de leitura. Com filtro, ficam as jornadas que casam **e os ancestrais delas** (para a árvore continuar
 * navegável), tudo expandido. Sem filtro, só as recolhidas escondem os filhos. Quem não tem o pai na lista vira raiz.
 */
export function visibleRows(all: JourneySummary[], collapsed: ReadonlySet<string>, filter: TreeFilter): TreeRow[] {
  const byId = new Map(all.map((j) => [j.id, j]));
  const filtering = isFiltering(filter);

  const included = new Set<string>();
  if (filtering) {
    for (const journey of all.filter((j) => matches(j, filter))) {
      let current: JourneySummary | undefined = journey;
      while (current && !included.has(current.id)) {
        included.add(current.id);
        current = current.parentId ? byId.get(current.parentId) : undefined;
      }
    }
  } else {
    all.forEach((j) => included.add(j.id));
  }

  const childrenOf = new Map<string | null, JourneySummary[]>();
  for (const journey of all.filter((j) => included.has(j.id))) {
    const key = journey.parentId && byId.has(journey.parentId) ? journey.parentId : null;
    childrenOf.set(key, [...(childrenOf.get(key) ?? []), journey]);
  }
  for (const list of childrenOf.values()) {
    list.sort((a, b) => a.name.localeCompare(b.name, 'pt-BR', { sensitivity: 'base' }));
  }

  const rows: TreeRow[] = [];
  const visited = new Set<string>();
  const walk = (parent: string | null, depth: number) => {
    for (const journey of childrenOf.get(parent) ?? []) {
      if (visited.has(journey.id)) {
        continue;
      }
      visited.add(journey.id);
      const expandable = (childrenOf.get(journey.id) ?? []).length > 0;
      const expanded = filtering || !collapsed.has(journey.id);
      rows.push({ journey, depth, expanded, expandable });
      if (expandable && expanded) {
        walk(journey.id, depth + 1);
      }
    }
  };
  walk(null, 0);
  return rows;
}

/** Nome com os ancestrais ("Plataforma › Sistema › Compra"), para escolher o pai sem ambiguidade. */
export function pathLabels(all: JourneySummary[]): Map<string, string> {
  const byId = new Map(all.map((j) => [j.id, j]));
  const labels = new Map<string, string>();
  for (const journey of all) {
    const names = [journey.name];
    const seen = new Set([journey.id]);
    let current = journey.parentId ? byId.get(journey.parentId) : undefined;
    while (current && !seen.has(current.id)) {
      names.unshift(current.name);
      seen.add(current.id);
      current = current.parentId ? byId.get(current.parentId) : undefined;
    }
    labels.set(journey.id, names.join(' › '));
  }
  return labels;
}

/** Os ids da jornada e de tudo o que está dentro dela. */
export function subtreeIds(all: JourneySummary[], rootId: string): Set<string> {
  const result = new Set([rootId]);
  let grew = true;
  while (grew) {
    grew = false;
    for (const journey of all) {
      if (journey.parentId && result.has(journey.parentId) && !result.has(journey.id)) {
        result.add(journey.id);
        grew = true;
      }
    }
  }
  return result;
}

function plural(count: number, singular: string, pluralForm: string): string {
  return `${count} ${count === 1 ? singular : pluralForm}`;
}

/** O resumo do impacto numa linha: o que quebra e, separado, o que ainda não dá para dizer. */
export function impactSummary(impact: JourneyImpact): string {
  if (impact.steps.length === 0) {
    return 'nenhum passo cadastrado';
  }

  const parts: string[] = [];
  if (impact.stepsWithoutComparison < impact.steps.length) {
    parts.push(
      impact.breaking > 0
        ? `${plural(impact.breaking, 'mudança que quebra', 'mudanças que quebram')} em ${plural(impact.breakingSteps, 'passo', 'passos')}`
        : 'nada que quebra',
    );
  }
  if (impact.stepsWithoutComparison > 0) {
    parts.push(`${plural(impact.stepsWithoutComparison, 'passo', 'passos')} sem comparação ainda`);
  }
  return parts.join(' · ');
}
