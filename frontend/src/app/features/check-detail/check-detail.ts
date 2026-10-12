import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { CHECK_ITEM_STATUSES, CHECK_ITEM_STATUS_LABELS, formatPercent } from '../../core/check-labels';
import { ChecksApiService } from '../../core/checks-api.service';
import { CheckCandidate, CheckDetail as CheckDetailModel, CheckEvent, CheckItem, CheckItemStatus } from '../../core/models';

@Component({
  selector: 'app-check-detail',
  imports: [RouterLink, DatePipe, FormsModule],
  templateUrl: './check-detail.html',
  styleUrl: './check-detail.css',
})
export class CheckDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly checksApi = inject(ChecksApiService);

  protected readonly checkId = this.route.snapshot.paramMap.get('checkId')!;
  protected readonly statuses = CHECK_ITEM_STATUSES;
  protected readonly statusLabels = CHECK_ITEM_STATUS_LABELS;
  protected readonly formatPercent = formatPercent;

  protected readonly check = signal<CheckDetailModel | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly actionError = signal<string | null>(null);

  // Filtros da tabela (FR-009)
  protected readonly search = signal('');
  protected readonly squadFilter = signal('');
  protected readonly statusFilter = signal<CheckItemStatus | ''>('');

  // Painel "Adicionar apps" (FR-003)
  protected readonly pickerOpen = signal(false);
  protected readonly candidates = signal<CheckCandidate[]>([]);
  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  protected readonly pickerSearch = signal('');
  protected readonly pickerSquad = signal('');
  protected readonly pickerTag = signal('');
  protected readonly adding = signal(false);

  // Histórico (FR-016), carregado sob demanda
  protected readonly historyOpen = signal(false);
  protected readonly history = signal<CheckEvent[]>([]);

  /** Nome por appId, para descrever eventos — inclui apps que já saíram da checagem. */
  private readonly appNames = signal<ReadonlyMap<string, string>>(new Map());

  protected readonly squads = computed(() => uniqueSquads(this.check()?.items ?? []));

  protected readonly filteredItems = computed(() => {
    const term = this.search().trim().toLowerCase();
    const squad = this.squadFilter();
    const status = this.statusFilter();
    return (this.check()?.items ?? [])
      .filter((i) => !term || i.appName.toLowerCase().includes(term))
      .filter((i) => !squad || i.squadId === squad)
      .filter((i) => !status || i.status === status);
  });

  protected readonly candidateSquads = computed(() => uniqueSquads(this.candidates()));

  protected readonly candidateTags = computed(() =>
    [...new Set(this.candidates().flatMap((c) => c.tags))].sort((a, b) => a.localeCompare(b)),
  );

  protected readonly filteredCandidates = computed(() => {
    const term = this.pickerSearch().trim().toLowerCase();
    const squad = this.pickerSquad();
    const tag = this.pickerTag();
    return this.candidates()
      .filter((c) => !term || c.name.toLowerCase().includes(term))
      .filter((c) => !squad || c.squadId === squad)
      .filter((c) => !tag || c.tags.includes(tag));
  });

  constructor() {
    this.checksApi.get(this.checkId).subscribe({
      next: (check) => {
        this.setCheck(check);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  protected openPicker(): void {
    this.pickerOpen.set(true);
    this.selected.set(new Set());
    this.checksApi.listCandidates(this.checkId).subscribe({
      next: (candidates) => {
        this.candidates.set(candidates);
        this.rememberNames(candidates.map((c) => [c.appId, c.name]));
      },
      error: () => this.actionError.set('Não foi possível carregar os apps disponíveis.'),
    });
  }

  protected toggleSelected(appId: string): void {
    const next = new Set(this.selected());
    if (next.has(appId)) {
      next.delete(appId);
    } else {
      next.add(appId);
    }
    this.selected.set(next);
  }

  protected selectAllFiltered(): void {
    this.selected.set(new Set([...this.selected(), ...this.filteredCandidates().map((c) => c.appId)]));
  }

  protected addSelected(): void {
    const appIds = [...this.selected()];
    if (appIds.length === 0) {
      return;
    }

    this.adding.set(true);
    this.actionError.set(null);
    this.checksApi.addApps(this.checkId, appIds).subscribe({
      next: () => {
        this.adding.set(false);
        this.pickerOpen.set(false);
        this.reload();
      },
      error: (err) => {
        this.adding.set(false);
        this.showError(err, 'Não foi possível adicionar os apps.');
      },
    });
  }

  protected changeStatus(item: CheckItem, status: CheckItemStatus): void {
    this.saveItem(item, status, item.notes);
  }

  /** Observação salva ao sair do campo, só se mudou. */
  protected saveNotes(item: CheckItem, notes: string): void {
    const normalized = notes.trim() || null;
    if (normalized === item.notes) {
      return;
    }
    this.saveItem(item, item.status, normalized);
  }

  protected removeApp(item: CheckItem): void {
    if (!confirm(`Retirar "${item.appName}" desta checagem? O histórico registra a saída.`)) {
      return;
    }

    this.actionError.set(null);
    this.checksApi.removeApp(this.checkId, item.appId).subscribe({
      next: () => this.reload(),
      error: (err) => this.showError(err, `Não foi possível retirar "${item.appName}".`),
    });
  }

  protected close(): void {
    const check = this.check()!;
    const pending = check.progress.total - check.progress.done;
    const message =
      pending > 0
        ? `Fechar a checagem com ${pending} app(s) ainda não concluído(s)? Ela continua consultável e pode ser reaberta.`
        : 'Fechar a checagem? Ela continua consultável e pode ser reaberta.';
    if (!confirm(message)) {
      return;
    }

    this.actionError.set(null);
    this.checksApi.close(this.checkId).subscribe({
      next: (updated) => this.setCheck(updated),
      error: (err) => this.showError(err, 'Não foi possível fechar a checagem.'),
    });
  }

  protected reopen(): void {
    this.actionError.set(null);
    this.checksApi.reopen(this.checkId).subscribe({
      next: (updated) => this.setCheck(updated),
      error: (err) => this.showError(err, 'Não foi possível reabrir a checagem.'),
    });
  }

  protected remove(): void {
    if (!confirm('Remover esta checagem? Essa ação não pode ser desfeita.')) {
      return;
    }

    this.checksApi.delete(this.checkId).subscribe({
      next: () => this.router.navigate(['/checks']),
      error: (err) => this.showError(err, 'Não foi possível remover a checagem.'),
    });
  }

  protected toggleHistory(): void {
    this.historyOpen.set(!this.historyOpen());
    if (this.historyOpen()) {
      this.loadHistory();
      // Apps que já saíram da checagem não estão nos itens; os candidatos trazem o nome deles.
      this.checksApi.listCandidates(this.checkId).subscribe((candidates) =>
        this.rememberNames(candidates.map((c) => [c.appId, c.name])),
      );
    }
  }

  protected describe(event: CheckEvent): string {
    const app = event.appId ? (this.appNames().get(event.appId) ?? event.appId) : '';
    const label = (status: CheckItemStatus | null) => (status ? this.statusLabels[status] : '—');

    switch (event.type) {
      case 'closed':
        return 'Checagem fechada';
      case 'reopened':
        return 'Checagem reaberta';
      case 'item-added':
        return `${app} entrou na checagem`;
      case 'item-removed':
        return `${app} saiu da checagem (estava: ${label(event.fromStatus)})`;
      case 'item-status-changed':
        return `${app}: ${label(event.fromStatus)} → ${label(event.toStatus)}`;
    }
  }

  private saveItem(item: CheckItem, status: CheckItemStatus, notes: string | null): void {
    this.actionError.set(null);
    this.checksApi.updateItem(this.checkId, item.appId, status, notes).subscribe({
      next: (updated) => this.setCheck(updated),
      error: (err) => {
        this.showError(err, `Não foi possível atualizar "${item.appName}".`);
        this.reload(); // desfaz na tela o valor que não foi gravado
      },
    });
  }

  private reload(): void {
    this.checksApi.get(this.checkId).subscribe({
      next: (check) => this.setCheck(check),
      error: () => this.actionError.set('Não foi possível recarregar a checagem.'),
    });
  }

  private setCheck(check: CheckDetailModel): void {
    this.check.set(check);
    this.rememberNames(check.items.map((i) => [i.appId, i.appName]));
    if (this.historyOpen()) {
      this.loadHistory();
    }
  }

  private loadHistory(): void {
    this.checksApi.getHistory(this.checkId).subscribe({
      next: (history) => this.history.set(history),
      error: () => this.actionError.set('Não foi possível carregar o histórico.'),
    });
  }

  private rememberNames(entries: [string, string][]): void {
    this.appNames.set(new Map([...this.appNames(), ...entries]));
  }

  private showError(err: unknown, fallback: string): void {
    const body = (err as { error?: unknown })?.error;
    this.actionError.set(typeof body === 'string' ? body : fallback);
  }
}

function uniqueSquads(rows: { squadId: string; squadName: string }[]): { id: string; name: string }[] {
  const byId = new Map(rows.map((r) => [r.squadId, r.squadName]));
  return [...byId].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
}
