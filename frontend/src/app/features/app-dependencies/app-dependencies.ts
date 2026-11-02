import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';

import { ANALYSIS_STATUS_LABELS, CONFIDENCE_LABELS, packagesSummary, shortSha } from '../../core/dependency-labels';
import { SectionStateService } from '../../core/section-state.service';
import { DependenciesApiService } from '../../core/dependencies-api.service';
import { AppDependencies, DependencyFact } from '../../core/models';

/**
 * Seção "Dependências" do detalhe do app (spec 013): por repositório, a versão do .NET e os pacotes diretos, com
 * a confiança em texto. Só lê: a análise roda pela ferramenta CheckApps.Analyze.
 */
@Component({
  selector: 'app-app-dependencies',
  imports: [DatePipe],
  templateUrl: './app-dependencies.html',
  styleUrl: './app-dependencies.css',
})
export class AppDependenciesSection {
  private readonly api = inject(DependenciesApiService);

  private readonly sections = inject(SectionStateService);

  readonly appId = input.required<string>();

  /** Fechada por padrão; a escolha da pessoa é lembrada no navegador (por seção, não por app). */
  protected readonly open = signal(this.sections.isOpen('pacotes'));

  protected readonly data = signal<AppDependencies | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  /** Resumo da linha fechada, derivado dos dados que o componente já carrega ao montar. */
  protected readonly summary = computed(() => {
    const data = this.data();
    if (this.loading() || !data) {
      return this.failed() ? 'não foi possível carregar' : 'Carregando…';
    }
    return packagesSummary(data);
  });

  protected readonly confidenceLabels = CONFIDENCE_LABELS;
  protected readonly statusLabels = ANALYSIS_STATUS_LABELS;
  protected readonly shortSha = shortSha;

  constructor() {
    effect(() => {
      const appId = this.appId();
      this.loading.set(true);
      this.failed.set(false);
      this.api.forApp(appId).subscribe({
        next: (data) => {
          this.data.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.failed.set(true);
          this.loading.set(false);
        },
      });
    });
  }

  protected onToggle(event: Event): void {
    const open = (event.target as HTMLDetailsElement).open;
    this.open.set(open);
    this.sections.setOpen('pacotes', open);
  }

  /** Só mostra a explicação quando a confiança não é alta: o motivo vem da API, em texto. */
  protected needsNote(fact: DependencyFact): boolean {
    return fact.confidence !== 'alta' && !!fact.confidenceNote;
  }
}
