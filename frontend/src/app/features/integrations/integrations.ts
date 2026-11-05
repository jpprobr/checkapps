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
import {
  AppSummary,
  BatchHostResult,
  BatchHostState,
  HostDetail,
  HostExternalMatch,
  HostLinkMatch,
  HostLinkRecord,
  HostLinkState,
  HostLinkStatus,
  HostList,
  Squad,
} from '../../core/models';
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

  protected readonly externalName = signal('');

  /** Vínculos cujo host não aparece mais nas análises correntes (spec 019): ficam aqui para serem revistos ou removidos. */
  protected readonly unseen = signal<HostLinkRecord[]>([]);

  // Decisão em lote (spec 019): hosts marcados na lista e o resultado de cada um.
  protected readonly checked = signal<ReadonlySet<string>>(new Set());
  protected readonly batchAppId = signal('');
  protected readonly batchName = signal('');
  protected readonly batchRunning = signal(false);
  protected readonly batchResults = signal<BatchHostResult[] | null>(null);
  protected readonly batchError = signal<string | null>(null);

  constructor() {
    this.squadsApi.list().subscribe((squads) => this.squads.set(squads));
    this.appsApi.list().subscribe((apps) => this.apps.set([...apps].sort((a, b) => a.name.localeCompare(b.name))));
    this.loadUnseen();
    this.search();
  }

  protected search(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.selectedKey.set(null);
    this.detail.set(null);
    this.checked.set(new Set());
    this.batchResults.set(null);
    this.batchError.set(null);
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
    this.externalName.set('');
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

  /** Marca o host aberto como serviço de fora do catálogo. */
  protected markExternal(): void {
    const key = this.selectedKey();
    if (!key || this.saving()) {
      return;
    }
    this.runAction(
      this.api.setExternal(key, { name: this.externalName().trim() || null, note: this.linkNote().trim() || null }),
      () => {
        this.externalName.set('');
        this.linkNote.set('');
      },
    );
  }

  protected removeExternal(match: HostExternalMatch): void {
    if (this.saving()) {
      return;
    }
    this.runAction(this.api.removeExternal(match.hostKey));
  }

  protected toggleChecked(key: string, on: boolean): void {
    const next = new Set(this.checked());
    if (on) {
      next.add(key);
    } else {
      next.delete(key);
    }
    this.checked.set(next);
  }

  protected allChecked(hosts: { key: string }[]): boolean {
    return hosts.length > 0 && hosts.every((h) => this.checked().has(h.key));
  }

  /** Marca ou desmarca todos os hosts visíveis (a lista já respeita os filtros). */
  protected toggleAll(hosts: { key: string }[], on: boolean): void {
    this.checked.set(on ? new Set(hosts.map((h) => h.key)) : new Set());
  }

  /** Aplica a mesma decisão aos hosts marcados; cada host responde por si (um conflito não desfaz os outros). */
  protected runBatch(state: BatchHostState): void {
    const hostKeys = [...this.checked()];
    if (hostKeys.length === 0 || this.batchRunning() || (state !== 'externo' && !this.batchAppId())) {
      return;
    }
    this.batchRunning.set(true);
    this.batchResults.set(null);
    this.batchError.set(null);
    this.api
      .batch({
        hostKeys,
        state,
        appId: state === 'externo' ? null : this.batchAppId(),
        name: state === 'externo' ? this.batchName().trim() || null : null,
        note: null,
      })
      .subscribe({
        next: (results) => {
          this.batchResults.set(results);
          this.batchRunning.set(false);
          // Os que deram certo saem da seleção; as falhas ficam marcadas para nova tentativa.
          this.checked.set(new Set(results.filter((r) => !r.ok).map((r) => r.hostKey)));
          this.batchName.set('');
          this.refreshList();
          const open = this.selectedKey();
          if (open) {
            this.reloadDetail(open);
          }
        },
        error: (err: HttpErrorResponse) => {
          this.batchError.set(typeof err.error === 'string' && err.error ? err.error : 'Não foi possível aplicar a decisão em lote.');
          this.batchRunning.set(false);
        },
      });
  }

  protected batchOkCount(results: BatchHostResult[]): number {
    return results.filter((r) => r.ok).length;
  }

  protected batchFailures(results: BatchHostResult[]): BatchHostResult[] {
    return results.filter((r) => !r.ok);
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

  private loadUnseen(): void {
    this.api.links().subscribe((links) => this.unseen.set(links.filter((l) => !l.seen)));
  }

  /** Remove um vínculo que não aparece mais nas análises. */
  protected removeUnseen(link: HostLinkRecord): void {
    if (this.saving()) {
      return;
    }
    this.runAction(this.api.removeLink(link.hostKey, link.appId));
  }

  /** Atualiza a lista com os filtros atuais sem fechar o host aberto. */
  private refreshList(): void {
    this.loadUnseen();
    this.api.hosts(this.host(), this.squadId() || null, this.linkStatus()).subscribe((result) => this.result.set(result));
  }

  private reloadDetail(key: string): void {
    this.api.hostDetail(key).subscribe((detail) => this.detail.set(detail));
  }

  protected squadNames(squads: { name: string }[]): string {
    return squads.length === 0 ? '—' : squads.map((s) => s.name).join(', ');
  }
}
