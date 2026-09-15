import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { SquadsApiService } from '../../core/squads-api.service';
import { AppQualityTrend, AppSummary, QualityLevel, Squad, SquadQualityEvolution } from '../../core/models';
import { QualityBadge } from '../../shared/quality-badge/quality-badge';

const LEVELS: QualityLevel[] = ['A', 'B', 'C', 'D', 'E'];
const ATTENTION_LEVELS: QualityLevel[] = ['C', 'D', 'E'];

@Component({
  selector: 'app-squad-dashboard',
  imports: [RouterLink, QualityBadge, DatePipe],
  templateUrl: './squad-dashboard.html',
  styleUrl: './squad-dashboard.css',
})
export class SquadDashboard {
  private readonly route = inject(ActivatedRoute);
  private readonly squadsApi = inject(SquadsApiService);

  protected readonly squadId = this.route.snapshot.paramMap.get('squadId')!;
  protected readonly squad = signal<Squad | null>(null);
  protected readonly apps = signal<AppSummary[]>([]);
  protected readonly evolution = signal<SquadQualityEvolution | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  protected readonly levels = LEVELS;

  protected readonly distribution = computed(() => {
    const counts: Record<QualityLevel, number> = { A: 0, B: 0, C: 0, D: 0, E: 0 };
    for (const app of this.apps()) {
      if (app.currentQualityLevel) counts[app.currentQualityLevel]++;
    }
    return counts;
  });

  protected readonly improved = computed(() => this.trendsWithDirection('Improved'));
  protected readonly worsened = computed(() => this.trendsWithDirection('Worsened'));

  protected readonly attentionApps = computed(() =>
    this.apps().filter((app) => app.currentQualityLevel && ATTENTION_LEVELS.includes(app.currentQualityLevel)),
  );

  constructor() {
    this.load(this.squadId);
  }

  private load(squadId: string): void {
    this.loading.set(true);
    this.error.set(false);

    this.squadsApi.get(squadId).subscribe({
      next: (squad) => this.squad.set(squad),
      error: () => this.error.set(true),
    });

    this.squadsApi.listApps(squadId).subscribe({
      next: (apps) => this.apps.set(apps),
      error: () => this.error.set(true),
    });

    this.squadsApi.getQualityEvolution(squadId).subscribe({
      next: (evolution) => {
        this.evolution.set(evolution);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  private trendsWithDirection(direction: AppQualityTrend['direction']): AppQualityTrend[] {
    return this.evolution()?.appTrends.filter((t) => t.direction === direction) ?? [];
  }
}
