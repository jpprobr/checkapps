import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';

import { AppsApiService } from '../../core/apps-api.service';
import { CurrentSquadService } from '../../core/current-squad.service';
import { SquadsApiService } from '../../core/squads-api.service';
import { AppSummary, QualityLevel } from '../../core/models';
import { QualityBadge } from '../../shared/quality-badge/quality-badge';

type SortKey = 'name' | 'qualityLevel';

/**
 * Lista de apps. Com `squadId` na rota (`/squads/:squadId/apps`) mostra os apps da squad e permite criar; sem ele
 * (`/apps`, spec 016) mostra os apps de todas as squads, com a squad em cada linha e um filtro de squad. O detalhe do
 * app é o mesmo nos dois caminhos.
 */
@Component({
  selector: 'app-app-list',
  imports: [RouterLink, QualityBadge, DatePipe, FormsModule],
  templateUrl: './app-list.html',
  styleUrl: './app-list.css',
})
export class AppList {
  private readonly route = inject(ActivatedRoute);
  private readonly squadsApi = inject(SquadsApiService);
  private readonly appsApi = inject(AppsApiService);
  protected readonly currentSquad = inject(CurrentSquadService);

  /** Nulo na lista geral de apps (`/apps`). */
  protected readonly squadId = this.route.snapshot.paramMap.get('squadId');
  protected readonly isGlobal = this.squadId === null;
  protected readonly apps = signal<AppSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly actionError = signal<string | null>(null);

  protected readonly search = signal('');
  protected readonly qualityLevelFilter = signal<QualityLevel | ''>('');
  protected readonly squadFilter = signal('');
  protected readonly sortKey = signal<SortKey>('name');
  protected readonly showInactive = signal(false);

  protected readonly levels: QualityLevel[] = ['A', 'B', 'C', 'D', 'E'];

  protected readonly filteredApps = computed(() => {
    const term = this.search().trim().toLowerCase();
    const level = this.qualityLevelFilter();
    const squad = this.squadFilter();
    const sortKey = this.sortKey();
    const showInactive = this.showInactive();

    return this.apps()
      .filter((app) => showInactive || app.status !== 'inactive')
      .filter((app) => !term || app.name.toLowerCase().includes(term))
      .filter((app) => !level || app.currentQualityLevel === level)
      .filter((app) => !squad || app.squadId === squad)
      .sort((a, b) => {
        if (sortKey === 'qualityLevel') {
          return (a.currentQualityLevel ?? 'Z').localeCompare(b.currentQualityLevel ?? 'Z');
        }
        return a.name.localeCompare(b.name);
      });
  });

  constructor() {
    if (this.squadId) {
      this.currentSquad.set(this.squadId);
    }
    this.reload();
  }

  private reload(): void {
    this.loading.set(true);
    const request = this.squadId ? this.squadsApi.listApps(this.squadId) : this.appsApi.list();
    request.subscribe({
      next: (apps) => {
        this.apps.set(apps);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  protected squadName(app: AppSummary): string {
    return this.currentSquad.squads().find((s) => s.id === app.squadId)?.name ?? '—';
  }

  protected toggleStatus(app: AppSummary): void {
    // AppSummary não carrega description/externalId; busca o AppDetail completo
    // antes de salvar para não apagar esses campos no PUT (que substitui o item inteiro).
    this.appsApi.get(app.id).subscribe({
      next: (detail) => {
        const nextStatus = detail.status === 'inactive' ? 'active' : 'inactive';
        this.appsApi
          .update(app.id, {
            name: detail.name,
            squadId: detail.squadId,
            description: detail.description,
            criticality: detail.criticality,
            tags: detail.tags,
            status: nextStatus,
            externalId: detail.externalId,
          })
          .subscribe({
            next: () => this.reload(),
            error: () => this.actionError.set(`Não foi possível atualizar o status de "${app.name}".`),
          });
      },
      error: () => this.actionError.set(`Não foi possível atualizar o status de "${app.name}".`),
    });
  }

  protected remove(app: AppSummary): void {
    if (!confirm(`Remover o app "${app.name}"? Essa ação não pode ser desfeita.`)) {
      return;
    }

    this.actionError.set(null);
    this.appsApi.delete(app.id).subscribe({
      next: () => this.reload(),
      error: (err) =>
        this.actionError.set(typeof err?.error === 'string' ? err.error : `Não foi possível remover "${app.name}".`),
    });
  }
}
