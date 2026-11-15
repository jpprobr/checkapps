import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { CurrentSquadService } from '../../core/current-squad.service';
import { TreeRow, isFiltering, visibleRows } from '../../core/journey-labels';
import { JourneysApiService } from '../../core/journeys-api.service';
import { JourneySummary } from '../../core/models';

/**
 * Jornadas em árvore (spec 022): plataforma, sistema, jornada… O nível é texto livre. Com filtro, ficam as jornadas que casam e os
 * ancestrais delas, expandidos. Só lê e navega: o cadastro está no formulário e o impacto, no detalhe.
 */
@Component({
  selector: 'app-journeys',
  imports: [FormsModule, RouterLink],
  templateUrl: './journeys.html',
  styleUrl: './journeys.css',
})
export class JourneysPage {
  private readonly api = inject(JourneysApiService);
  protected readonly currentSquad = inject(CurrentSquadService);

  protected readonly journeys = signal<JourneySummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  protected readonly search = signal('');
  protected readonly squadFilter = signal('');
  protected readonly collapsed = signal<ReadonlySet<string>>(new Set());

  protected readonly filtering = computed(() => isFiltering({ search: this.search(), squadId: this.squadFilter() }));

  protected readonly rows = computed<TreeRow[]>(() =>
    visibleRows(this.journeys(), this.collapsed(), { search: this.search(), squadId: this.squadFilter() }),
  );

  constructor() {
    this.api.list().subscribe({
      next: (journeys) => {
        this.journeys.set(journeys);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  protected toggle(journeyId: string): void {
    const next = new Set(this.collapsed());
    if (!next.delete(journeyId)) {
      next.add(journeyId);
    }
    this.collapsed.set(next);
  }

  protected expandAll(): void {
    this.collapsed.set(new Set());
  }

  protected collapseAll(): void {
    this.collapsed.set(new Set(this.journeys().filter((j) => j.childCount > 0).map((j) => j.id)));
  }
}
