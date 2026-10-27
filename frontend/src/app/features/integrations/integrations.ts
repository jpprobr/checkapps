import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { CONFIDENCE_LABELS } from '../../core/dependency-labels';
import { IntegrationsApiService } from '../../core/integrations-api.service';
import { INTEGRATION_STATE_LABELS, environmentLabel, integrationAddress } from '../../core/integration-labels';
import { HostDetail, HostList, Squad } from '../../core/models';
import { SquadsApiService } from '../../core/squads-api.service';

/**
 * Tela "Integrações" (spec 014): os hosts que os repositórios catalogados declaram chamar, segundo a última análise de
 * cada um, e quem os chama. O host é a chave literal (host[:porta], inclusive o modelo com {env}): o CheckApps ainda
 * não diz a que app do catálogo cada host pertence. Só lê; a análise roda pela ferramenta CheckApps.Analyze.
 */
@Component({
  selector: 'app-integrations',
  imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './integrations.html',
  styleUrl: './integrations.css',
})
export class Integrations {
  private readonly api = inject(IntegrationsApiService);
  private readonly squadsApi = inject(SquadsApiService);

  protected readonly confidenceLabels = CONFIDENCE_LABELS;
  protected readonly stateLabels = INTEGRATION_STATE_LABELS;
  protected readonly address = integrationAddress;
  protected readonly environment = environmentLabel;

  protected readonly squads = signal<Squad[]>([]);
  protected readonly host = signal('');
  protected readonly squadId = signal('');

  protected readonly loading = signal(false);
  protected readonly failed = signal(false);
  protected readonly result = signal<HostList | null>(null);

  protected readonly selectedKey = signal<string | null>(null);
  protected readonly detail = signal<HostDetail | null>(null);
  protected readonly detailLoading = signal(false);
  protected readonly detailFailed = signal(false);

  constructor() {
    this.squadsApi.list().subscribe((squads) => this.squads.set(squads));
    this.search();
  }

  protected search(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.selectedKey.set(null);
    this.detail.set(null);
    this.api.hosts(this.host(), this.squadId() || null).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  protected select(key: string): void {
    this.selectedKey.set(key);
    this.detail.set(null);
    this.detailLoading.set(true);
    this.detailFailed.set(false);
    this.api.hostDetail(key).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.detailLoading.set(false);
      },
      error: () => {
        this.detailFailed.set(true);
        this.detailLoading.set(false);
      },
    });
  }

  protected squadNames(squads: { name: string }[]): string {
    return squads.length === 0 ? '—' : squads.map((s) => s.name).join(', ');
  }
}
