import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';

import { AppsApiService } from '../../core/apps-api.service';
import { CONFIDENCE_LABELS } from '../../core/dependency-labels';
import { IntegrationsApiService } from '../../core/integrations-api.service';
import {
  INTEGRATION_STATE_LABELS,
  LINK_STATUS_LABELS,
  environmentLabel,
  integrationAddress,
  linkSummary,
} from '../../core/integration-labels';
import { AppSummary, HostDetail, HostLinkMatch, HostLinkState, HostLinkStatus, HostList, Squad } from '../../core/models';
import { SquadsApiService } from '../../core/squads-api.service';

/**
 * Tela "Integrações" (spec 014): os hosts que os repositórios catalogados declaram chamar, segundo a última análise de
 * cada um, e quem os chama. O host é a chave literal (host[:porta], inclusive o modelo com {env}). Spec 018: aqui uma
 * pessoa também decide a que app do catálogo cada host pertence (confirmar, rejeitar, remover; spec 017). A análise
 * roda pela ferramenta CheckApps.Analyze.
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
  private readonly appsApi = inject(AppsApiService);

  protected readonly confidenceLabels = CONFIDENCE_LABELS;
  protected readonly stateLabels = INTEGRATION_STATE_LABELS;
  protected readonly address = integrationAddress;
  protected readonly environment = environmentLabel;
  protected readonly linkLabels = LINK_STATUS_LABELS;
  protected readonly linkStatuses = Object.keys(LINK_STATUS_LABELS) as HostLinkStatus[];
  protected readonly linkText = linkSummary;

  protected readonly squads = signal<Squad[]>([]);
  protected readonly host = signal('');
  protected readonly squadId = signal('');
  protected readonly linkStatus = signal<HostLinkStatus | ''>('');

  protected readonly loading = signal(false);
  protected readonly failed = signal(false);
  protected readonly result = signal<HostList | null>(null);

  protected readonly selectedKey = signal<string | null>(null);
  protected readonly detail = signal<HostDetail | null>(null);
  protected readonly detailLoading = signal(false);
  protected readonly detailFailed = signal(false);

  protected readonly apps = signal<AppSummary[]>([]);
  protected readonly linkAppId = signal('');
  protected readonly linkNote = signal('');
  protected readonly saving = signal(false);
  protected readonly actionError = signal<string | null>(null);

  constructor() {
    this.squadsApi.list().subscribe((squads) => this.squads.set(squads));
    this.appsApi.list().subscribe((apps) => this.apps.set([...apps].sort((a, b) => a.name.localeCompare(b.name))));
    this.search();
  }

  protected search(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.selectedKey.set(null);
    this.detail.set(null);
    this.api.hosts(this.host(), this.squadId() || null, this.linkStatus()).subscribe({
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
    this.actionError.set(null);
    this.linkAppId.set('');
    this.linkNote.set('');
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

  /** Confirma ou rejeita o app escolhido para o host selecionado. */
  protected decide(state: HostLinkState): void {
    const key = this.selectedKey();
    if (!key || !this.linkAppId() || this.saving()) {
      return;
    }
    this.runAction(
      this.api.setLink(key, { appId: this.linkAppId(), state, note: this.linkNote().trim() || null }),
      () => {
        this.linkAppId.set('');
        this.linkNote.set('');
      },
    );
  }

  /** Remove o vínculo; o de um modelo {env} é removido pela chave do modelo, não pela do host aberto. */
  protected remove(match: HostLinkMatch): void {
    if (this.saving()) {
      return;
    }
    this.runAction(this.api.removeLink(match.hostKey, match.appId));
  }

  private runAction(request: Observable<unknown>, onDone?: () => void): void {
    const key = this.selectedKey();
    this.saving.set(true);
    this.actionError.set(null);
    request.subscribe({
      next: () => {
        onDone?.();
        this.saving.set(false);
        this.refreshList();
        if (key) {
          this.reloadDetail(key);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.actionError.set(typeof err.error === 'string' && err.error ? err.error : 'Não foi possível gravar o vínculo.');
        this.saving.set(false);
      },
    });
  }

  /** Atualiza a lista com os filtros atuais sem fechar o host aberto. */
  private refreshList(): void {
    this.api.hosts(this.host(), this.squadId() || null, this.linkStatus()).subscribe((result) => this.result.set(result));
  }

  private reloadDetail(key: string): void {
    this.api.hostDetail(key).subscribe((detail) => this.detail.set(detail));
  }

  protected squadNames(squads: { name: string }[]): string {
    return squads.length === 0 ? '—' : squads.map((s) => s.name).join(', ');
  }
}
