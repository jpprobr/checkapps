import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { CONFIDENCE_LABELS } from '../../core/dependency-labels';
import { DependenciesApiService } from '../../core/dependencies-api.service';
import { FrameworkCell, FrameworkOverview, PackageSearchResult, Squad } from '../../core/models';
import { SquadsApiService } from '../../core/squads-api.service';

type Tab = 'packages' | 'frameworks';

/**
 * Tela "Dependências" (spec 013): quem usa um pacote, em que versão e de qual squad, e a versão do .NET por squad.
 * Só lê as análises já gravadas; a análise roda pela ferramenta CheckApps.Analyze.
 */
@Component({
  selector: 'app-dependencies',
  imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './dependencies.html',
  styleUrl: './dependencies.css',
})
export class Dependencies {
  private readonly api = inject(DependenciesApiService);
  private readonly squadsApi = inject(SquadsApiService);

  protected readonly confidenceLabels = CONFIDENCE_LABELS;

  protected readonly tab = signal<Tab>('packages');
  protected readonly squads = signal<Squad[]>([]);

  // ---- Pacotes ----
  protected readonly name = signal('');
  protected readonly squadId = signal('');
  protected readonly internalOnly = signal(false);
  protected readonly searching = signal(false);
  protected readonly searchError = signal<string | null>(null);
  protected readonly result = signal<PackageSearchResult | null>(null);
  /** O que foi consultado de fato: a mensagem de "ninguém usa" cita este nome, não o que se digita depois. */
  protected readonly searched = signal<{ name: string; internalOnly: boolean } | null>(null);

  // ---- .NET por squad ----
  protected readonly overview = signal<FrameworkOverview | null>(null);
  protected readonly overviewLoading = signal(false);
  protected readonly overviewFailed = signal(false);
  protected readonly selected = signal<{ framework: string | null; squadId: string | null } | null>(null);

  /** Frameworks que aparecem na tabela, do mais novo para o mais antigo; "sem informação" por último. */
  protected readonly frameworks = computed(() => {
    const cells = this.overview()?.cells ?? [];
    return [...new Set(cells.map((c) => c.framework))].sort((a, b) => {
      if (a === null) return 1;
      if (b === null) return -1;
      return b.localeCompare(a, 'pt-BR', { numeric: true });
    });
  });

  protected readonly overviewSquads = computed(() => {
    const byId = new Map<string | null, string>();
    for (const cell of this.overview()?.cells ?? []) {
      byId.set(cell.squadId, cell.squadName ?? 'Sem squad');
    }
    return [...byId.entries()]
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.name.localeCompare(b.name, 'pt-BR'));
  });

  protected readonly selectedCell = computed(() => {
    const selection = this.selected();
    return selection ? this.cellFor(selection.framework, selection.squadId) ?? null : null;
  });

  constructor() {
    this.squadsApi.list().subscribe((squads) => this.squads.set(squads));
  }

  protected showTab(tab: Tab): void {
    this.tab.set(tab);
    if (tab === 'frameworks' && this.overview() === null && !this.overviewLoading()) {
      this.loadOverview();
    }
  }

  protected search(): void {
    this.searchError.set(null);
    if (!this.name().trim() && !this.internalOnly()) {
      this.searchError.set('Informe o nome do pacote (ou marque "apenas internos").');
      return;
    }

    const query = { name: this.name().trim(), internalOnly: this.internalOnly() };
    this.searching.set(true);
    this.api.searchPackages(query.name, this.squadId() || null, query.internalOnly).subscribe({
      next: (result) => {
        this.result.set(result);
        this.searched.set(query);
        this.searching.set(false);
      },
      error: (err) => {
        this.searchError.set(typeof err?.error === 'string' ? err.error : 'Não foi possível consultar os pacotes.');
        this.searching.set(false);
      },
    });
  }

  protected loadOverview(): void {
    this.overviewLoading.set(true);
    this.overviewFailed.set(false);
    this.api.frameworks().subscribe({
      next: (overview) => {
        this.overview.set(overview);
        this.overviewLoading.set(false);
      },
      error: () => {
        this.overviewFailed.set(true);
        this.overviewLoading.set(false);
      },
    });
  }

  protected cellFor(framework: string | null, squadId: string | null): FrameworkCell | undefined {
    return this.overview()?.cells.find((c) => c.framework === framework && c.squadId === squadId);
  }

  protected select(framework: string | null, squadId: string | null): void {
    this.selected.set({ framework, squadId });
  }

  protected isSelected(framework: string | null, squadId: string | null): boolean {
    const selection = this.selected();
    return selection?.framework === framework && selection?.squadId === squadId;
  }

  protected frameworkLabel(framework: string | null): string {
    return framework ?? 'Sem informação';
  }
}
