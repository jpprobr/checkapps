import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { AppsApiService } from '../../core/apps-api.service';
import { AppDetail as AppDetailModel, QualitySnapshot, RepositoryInfo, SonarProjectInfo } from '../../core/models';
import { QualityBadge } from '../../shared/quality-badge/quality-badge';
import { QualityHistoryChart } from '../../shared/quality-history-chart/quality-history-chart';

@Component({
  selector: 'app-app-detail',
  imports: [RouterLink, DatePipe, QualityBadge, QualityHistoryChart],
  templateUrl: './app-detail.html',
  styleUrl: './app-detail.css',
})
export class AppDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly appsApi = inject(AppsApiService);

  protected readonly appId = this.route.snapshot.paramMap.get('appId')!;

  protected readonly app = signal<AppDetailModel | null>(null);
  protected readonly repositories = signal<RepositoryInfo[]>([]);
  protected readonly sonarProjects = signal<SonarProjectInfo[]>([]);
  protected readonly history = signal<QualitySnapshot[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  constructor() {
    this.appsApi.get(this.appId).subscribe({
      next: (app) => {
        this.app.set(app);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });

    this.appsApi.listRepositories(this.appId).subscribe((repos) => this.repositories.set(repos));
    this.appsApi.listSonarProjects(this.appId).subscribe((projects) => this.sonarProjects.set(projects));
    this.appsApi.getQualityHistory(this.appId).subscribe((history) => this.history.set(history));
  }
}
