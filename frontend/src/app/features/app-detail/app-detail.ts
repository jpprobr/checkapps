import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { AppsApiService } from '../../core/apps-api.service';
import { CHECK_ITEM_STATUS_LABELS } from '../../core/check-labels';
import { ChecksApiService } from '../../core/checks-api.service';
import { CurrentSquadService } from '../../core/current-squad.service';
import {
  AppCheck,
  AppDetail as AppDetailModel,
  AppLink,
  QualitySnapshot,
  RepositoryInfo,
  SonarProjectInfo,
} from '../../core/models';
import { AppDependenciesSection } from '../app-dependencies/app-dependencies';
import { AppIntegrationsSection } from '../app-integrations/app-integrations';
import { QualityBadge } from '../../shared/quality-badge/quality-badge';
import { QualityHistoryChart } from '../../shared/quality-history-chart/quality-history-chart';

@Component({
  selector: 'app-app-detail',
  imports: [RouterLink, DatePipe, QualityBadge, QualityHistoryChart, AppDependenciesSection, AppIntegrationsSection],
  templateUrl: './app-detail.html',
  styleUrl: './app-detail.css',
})
export class AppDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly appsApi = inject(AppsApiService);
  private readonly checksApi = inject(ChecksApiService);
  protected readonly currentSquad = inject(CurrentSquadService);

  protected readonly appId = this.route.snapshot.paramMap.get('appId')!;

  protected readonly app = signal<AppDetailModel | null>(null);
  protected readonly repositories = signal<RepositoryInfo[]>([]);
  protected readonly sonarProjects = signal<SonarProjectInfo[]>([]);
  protected readonly links = signal<AppLink[]>([]);
  protected readonly history = signal<QualitySnapshot[]>([]);
  protected readonly checks = signal<AppCheck[]>([]);
  protected readonly openChecks = computed(() => this.checks().filter((c) => c.checkStatus === 'open'));
  protected readonly closedChecks = computed(() => this.checks().filter((c) => c.checkStatus === 'closed'));
  protected readonly statusLabels = CHECK_ITEM_STATUS_LABELS;
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  constructor() {
    this.appsApi.get(this.appId).subscribe({
      next: (app) => {
        this.app.set(app);
        this.currentSquad.set(app.squadId);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });

    this.appsApi.listRepositories(this.appId).subscribe((repos) => this.repositories.set(repos));
    this.appsApi.listSonarProjects(this.appId).subscribe((projects) => this.sonarProjects.set(projects));
    this.appsApi.listLinks(this.appId).subscribe((links) => this.links.set(links));
    this.appsApi.getQualityHistory(this.appId).subscribe((history) => this.history.set(history));
    this.checksApi.listByApp(this.appId).subscribe((checks) => this.checks.set(checks));
  }
}
