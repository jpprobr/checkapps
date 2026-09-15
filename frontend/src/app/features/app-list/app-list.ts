import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';

import { SquadsApiService } from '../../core/squads-api.service';
import { AppSummary, QualityLevel } from '../../core/models';
import { QualityBadge } from '../../shared/quality-badge/quality-badge';

type SortKey = 'name' | 'qualityLevel';

@Component({
  selector: 'app-app-list',
  imports: [RouterLink, QualityBadge, DatePipe, FormsModule],
  templateUrl: './app-list.html',
  styleUrl: './app-list.css',
})
export class AppList {
  private readonly route = inject(ActivatedRoute);
  private readonly squadsApi = inject(SquadsApiService);

  protected readonly squadId = this.route.snapshot.paramMap.get('squadId')!;
  protected readonly apps = signal<AppSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  protected readonly search = signal('');
  protected readonly qualityLevelFilter = signal<QualityLevel | ''>('');
  protected readonly sortKey = signal<SortKey>('name');

  protected readonly levels: QualityLevel[] = ['A', 'B', 'C', 'D', 'E'];

  protected readonly filteredApps = computed(() => {
    const term = this.search().trim().toLowerCase();
    const level = this.qualityLevelFilter();
    const sortKey = this.sortKey();

    return this.apps()
      .filter((app) => !term || app.name.toLowerCase().includes(term))
      .filter((app) => !level || app.currentQualityLevel === level)
      .sort((a, b) => {
        if (sortKey === 'qualityLevel') {
          return (a.currentQualityLevel ?? 'Z').localeCompare(b.currentQualityLevel ?? 'Z');
        }
        return a.name.localeCompare(b.name);
      });
  });

  constructor() {
    this.squadsApi.listApps(this.squadId).subscribe({
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
}
