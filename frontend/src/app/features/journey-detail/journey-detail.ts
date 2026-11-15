import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { AppsApiService } from '../../core/apps-api.service';
import { SEVERITY_LABELS, fieldPlace } from '../../core/contract-labels';
import { ContractsApiService } from '../../core/contracts-api.service';
import { CONFIDENCE_LABELS } from '../../core/dependency-labels';
import { COMPARISON_LABELS, HTTP_METHODS, STEP_RESOLUTION_LABELS, impactSummary, messageOf } from '../../core/journey-labels';
import { JourneysApiService } from '../../core/journeys-api.service';
import { AppSummary, JourneyDetail, JourneyImpact, JourneyStepRequest } from '../../core/models';

/** Um passo em edição. A chave só serve para o Angular identificar a linha ao reordenar. */
interface StepDraft {
  key: number;
  appId: string;
  method: string;
  path: string;
  label: string;
  /** O endpoint escolhido na lista do contrato (`GET /rota`); vazio quando o método e a rota foram digitados. */
  picked: string;
}

interface EndpointOption {
  key: string;
  method: string;
  path: string;
  summary: string | null;
}

/**
 * Detalhe de uma jornada (spec 022): caminho na árvore, dados, jornadas de dentro, passos com o estado em texto, apps derivados e o
 * impacto das mudanças de contrato. O editor de passos substitui a lista inteira. O estado dos passos e o impacto são calculados pela API
 * a cada leitura; o contrato é o que foi declarado e pode estar desatualizado em relação à aplicação.
 */
@Component({
  selector: 'app-journey-detail',
  imports: [FormsModule, RouterLink],
  templateUrl: './journey-detail.html',
  styleUrl: './journey-detail.css',
})
export class JourneyDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(JourneysApiService);
  private readonly appsApi = inject(AppsApiService);
  private readonly contractsApi = inject(ContractsApiService);

  protected readonly journeyId = this.route.snapshot.paramMap.get('journeyId')!;

  protected readonly detail = signal<JourneyDetail | null>(null);
  protected readonly impact = signal<JourneyImpact | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly editorError = signal<string | null>(null);
  protected readonly drafts = signal<StepDraft[]>([]);
  protected readonly apps = signal<AppSummary[]>([]);
  private readonly endpointsByApp = signal<Record<string, EndpointOption[]>>({});
  private nextKey = 1;

  protected readonly resolutionLabels = STEP_RESOLUTION_LABELS;
  protected readonly comparisonLabels = COMPARISON_LABELS;
  protected readonly confidenceLabels = CONFIDENCE_LABELS;
  protected readonly severityLabels = SEVERITY_LABELS;
  protected readonly methods = HTTP_METHODS;
  protected readonly fieldPlace = fieldPlace;

  /** Só as jornadas-folha têm passos; só as sem passos aceitam jornadas dentro. */
  protected readonly isLeaf = computed(() => (this.detail()?.children.length ?? 0) === 0);
  protected readonly hasSteps = computed(() => (this.detail()?.steps.length ?? 0) > 0);

  protected readonly impactLine = computed(() => {
    const impact = this.impact();
    return impact ? impactSummary(impact) : '';
  });
  protected readonly changedSteps = computed(() => this.impact()?.steps.filter((s) => s.changes.length > 0) ?? []);
  protected readonly unknownSteps = computed(() => this.impact()?.steps.filter((s) => s.comparison !== 'comparado') ?? []);
  protected readonly unchangedCount = computed(
    () => (this.impact()?.steps.filter((s) => s.comparison === 'comparado' && s.changes.length === 0).length ?? 0),
  );

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    forkJoin({ detail: this.api.get(this.journeyId), impact: this.api.impact(this.journeyId) }).subscribe({
      next: ({ detail, impact }) => {
        this.detail.set(detail);
        this.impact.set(impact);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.status === 404 ? 'Esta jornada não existe.' : 'Não foi possível carregar a jornada.');
        this.loading.set(false);
      },
    });
  }

  protected remove(): void {
    const journey = this.detail()?.journey;
    if (!journey || !confirm(`Remover a jornada "${journey.name}"? Os passos e as tags dela também são removidos.`)) {
      return;
    }
    this.actionError.set(null);
    this.api.delete(journey.id).subscribe({
      next: () => void this.router.navigate(['/journeys']),
      error: (err) => this.actionError.set(messageOf(err, 'Não foi possível remover a jornada.')),
    });
  }

  // ---- Editor de passos -----------------------------------------------------------------------------------------------

  protected startEditing(): void {
    this.editorError.set(null);
    this.appsApi.list().subscribe({
      next: (apps) => this.apps.set([...apps].sort((a, b) => a.name.localeCompare(b.name, 'pt-BR', { sensitivity: 'base' }))),
      error: () => this.editorError.set('Não foi possível carregar a lista de apps.'),
    });

    const steps = this.detail()?.steps ?? [];
    this.drafts.set(
      steps.map((s) => ({ key: this.nextKey++, appId: s.appId, method: s.method, path: s.path, label: s.label ?? '', picked: '' })),
    );
    steps.forEach((s) => this.loadEndpoints(s.appId));
    this.editing.set(true);
  }

  protected cancelEditing(): void {
    this.editing.set(false);
    this.editorError.set(null);
  }

  protected addStep(): void {
    this.drafts.update((list) => [...list, { key: this.nextKey++, appId: '', method: 'GET', path: '', label: '', picked: '' }]);
  }

  protected removeStep(index: number): void {
    this.drafts.update((list) => list.filter((_, i) => i !== index));
  }

  protected move(index: number, by: -1 | 1): void {
    this.drafts.update((list) => {
      const target = index + by;
      if (target < 0 || target >= list.length) {
        return list;
      }
      const next = [...list];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  protected patch(index: number, changes: Partial<StepDraft>): void {
    this.drafts.update((list) => list.map((d, i) => (i === index ? { ...d, ...changes } : d)));
  }

  protected changeApp(index: number, appId: string): void {
    this.patch(index, { appId, picked: '' });
    this.loadEndpoints(appId);
  }

  protected pick(index: number, key: string): void {
    const draft = this.drafts()[index];
    const option = this.endpointsFor(draft.appId).find((o) => o.key === key);
    this.patch(index, option ? { picked: key, method: option.method, path: option.path } : { picked: '' });
  }

  protected endpointsFor(appId: string): EndpointOption[] {
    return this.endpointsByApp()[appId] ?? [];
  }

  private loadEndpoints(appId: string): void {
    if (!appId || this.endpointsByApp()[appId]) {
      return;
    }
    this.contractsApi.forApp(appId).subscribe({
      next: (contracts) => {
        const seen = new Set<string>();
        const options: EndpointOption[] = [];
        for (const endpoint of contracts.repositories.flatMap((r) => r.endpoints)) {
          const key = `${endpoint.method} ${endpoint.path}`;
          if (!seen.has(key)) {
            seen.add(key);
            options.push({ key, method: endpoint.method, path: endpoint.path, summary: endpoint.summary });
          }
        }
        options.sort((a, b) => a.path.localeCompare(b.path) || a.method.localeCompare(b.method));
        this.endpointsByApp.update((all) => ({ ...all, [appId]: options }));
      },
      // Sem contrato legível, o passo continua podendo ser digitado.
      error: () => this.endpointsByApp.update((all) => ({ ...all, [appId]: [] })),
    });
  }

  protected saveSteps(): void {
    this.editorError.set(null);
    const steps: JourneyStepRequest[] = this.drafts().map((d) => ({
      appId: d.appId,
      method: d.method,
      path: d.path.trim(),
      label: d.label.trim() || null,
      note: null,
    }));

    this.saving.set(true);
    this.api.replaceSteps(this.journeyId, steps).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.saving.set(false);
        this.editing.set(false);
        this.api.impact(this.journeyId).subscribe({ next: (impact) => this.impact.set(impact) });
      },
      error: (err) => {
        this.editorError.set(messageOf(err, 'Não foi possível salvar os passos.'));
        this.saving.set(false);
      },
    });
  }
}
