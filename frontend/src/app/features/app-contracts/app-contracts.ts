import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { forkJoin } from 'rxjs';

import {
  DECLARED_IN_LABELS,
  SEVERITY_LABELS,
  contractsSummary,
  countBySeverity,
  fieldPlace,
  groupByTag,
  groupChanges,
  matchesPath,
} from '../../core/contract-labels';
import { ContractsApiService } from '../../core/contracts-api.service';
import { ANALYSIS_STATUS_LABELS, CONFIDENCE_LABELS, shortSha } from '../../core/dependency-labels';
import { isNote, noteText } from '../../core/integration-labels';
import { ApiEndpoint, AppContractChanges, AppContracts, RepositoryContractChanges, RepositoryContracts } from '../../core/models';
import { SectionStateService } from '../../core/section-state.service';

/**
 * Seção "Contratos" do detalhe do app (spec 021): os endpoints declarados em contratos e o que mudou neles desde o commit
 * analisado anteriormente. Só lê: a análise roda pela ferramenta CheckApps.Analyze. O contrato é o que foi declarado, e pode
 * estar desatualizado em relação à aplicação.
 */
@Component({
  selector: 'app-app-contracts',
  imports: [DatePipe],
  templateUrl: './app-contracts.html',
  styleUrl: './app-contracts.css',
})
export class AppContractsSection {
  private readonly api = inject(ContractsApiService);

  private readonly sections = inject(SectionStateService);

  readonly appId = input.required<string>();

  /** Fechada por padrão; a escolha da pessoa é lembrada no navegador (por seção, não por app). */
  protected readonly open = signal(this.sections.isOpen('contratos'));

  protected readonly contracts = signal<AppContracts | null>(null);
  protected readonly changes = signal<AppContractChanges | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly filter = signal('');

  protected readonly summary = computed(() => {
    const contracts = this.contracts();
    const changes = this.changes();
    if (this.loading() || !contracts || !changes) {
      return this.failed() ? 'não foi possível carregar' : 'Carregando…';
    }
    return contractsSummary(contracts, changes);
  });

  protected readonly confidenceLabels = CONFIDENCE_LABELS;
  protected readonly statusLabels = ANALYSIS_STATUS_LABELS;
  protected readonly severityLabels = SEVERITY_LABELS;
  protected readonly declaredInLabels = DECLARED_IN_LABELS;
  protected readonly shortSha = shortSha;
  protected readonly isNote = isNote;
  protected readonly noteText = noteText;
  protected readonly fieldPlace = fieldPlace;
  protected readonly groupChanges = groupChanges;
  protected readonly countBySeverity = countBySeverity;

  constructor() {
    effect(() => {
      const appId = this.appId();
      this.loading.set(true);
      this.failed.set(false);
      forkJoin({ contracts: this.api.forApp(appId), changes: this.api.changesForApp(appId) }).subscribe({
        next: ({ contracts, changes }) => {
          this.contracts.set(contracts);
          this.changes.set(changes);
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
    this.sections.setOpen('contratos', open);
  }

  protected onFilter(event: Event): void {
    this.filter.set((event.target as HTMLInputElement).value);
  }

  protected changesOf(repo: RepositoryContracts): RepositoryContractChanges | undefined {
    return this.changes()?.repositories.find((r) => r.repositoryId === repo.repositoryId);
  }

  protected visibleGroups(repo: RepositoryContracts) {
    return groupByTag(repo.endpoints.filter((endpoint) => matchesPath(endpoint, this.filter())));
  }

  protected hasAnyEndpoint(): boolean {
    return (this.contracts()?.repositories ?? []).some((r) => r.endpoints.length > 0);
  }

  protected needsNote(endpoint: ApiEndpoint): boolean {
    return endpoint.confidence !== 'alta' && !!endpoint.confidenceNote;
  }
}
