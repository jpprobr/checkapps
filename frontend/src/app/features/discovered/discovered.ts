import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { AppsApiService } from '../../core/apps-api.service';
import { CurrentSquadService } from '../../core/current-squad.service';
import { DiscoveredApiService } from '../../core/discovered-api.service';
import { AppSummary, DiscoveredRepository, ScanOutcome, ScanReport, ScanStatus, TriageStatus } from '../../core/models';
import { SquadsApiService } from '../../core/squads-api.service';

type StatusFilter = TriageStatus | 'all';
type PanelMode = 'create' | 'link';

interface AppOption {
  id: string;
  label: string;
}

const SIGLAS_STORAGE_KEY = 'checkapps.scanSiglas';

/**
 * Caixa de entrada do leitor de GitHub (spec 005): o scan propõe, a pessoa decide. "Criar app"
 * abre um painel pré-preenchido e não grava nada até a confirmação. O bloco "Buscar no GitHub"
 * (spec 006) dispara o mesmo scan do CLI, quando a API o habilita.
 */
@Component({
  selector: 'app-discovered',
  imports: [RouterLink, DatePipe, FormsModule],
  templateUrl: './discovered.html',
  styleUrl: './discovered.css',
})
export class Discovered {
  private readonly discoveredApi = inject(DiscoveredApiService);
  private readonly squadsApi = inject(SquadsApiService);
  protected readonly currentSquad = inject(CurrentSquadService);

  protected readonly rows = signal<DiscoveredRepository[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly notice = signal<{ text: string; appId?: string; warnings: string[] } | null>(null);

  protected readonly statusFilter = signal<StatusFilter>('new');
  protected readonly siglaFilter = signal('');

  protected readonly roles = ['aplicacao', 'infra', 'contrato', 'secrets'];

  // Painel inline: um por vez
  protected readonly panelFor = signal<string | null>(null);
  protected readonly panelMode = signal<PanelMode>('create');
  protected readonly formName = signal('');
  protected readonly formSquadId = signal('');
  protected readonly formDescription = signal('');
  protected readonly formRole = signal('');
  protected readonly formAppId = signal('');
  protected readonly saving = signal(false);
  protected readonly appOptions = signal<AppOption[]>([]);

  // Scan pela tela (spec 006)
  protected readonly scanOpen = signal(false);
  protected readonly scanStatus = signal<ScanStatus | null>(null);
  protected readonly scanStatusFailed = signal(false);
  protected readonly scanSiglas = signal(readSavedSiglas());
  protected readonly scanning = signal(false);
  protected readonly scanError = signal<string | null>(null);
  protected readonly scanReport = signal<ScanReport | null>(null);
  protected readonly outcomeLabels: Record<ScanOutcome, string> = {
    new: 'novo',
    updated: 'atualizado',
    unchanged: 'inalterado',
  };

  protected readonly siglas = computed(() => [...new Set(this.rows().map((r) => r.sigla))].sort());

  protected readonly visibleRows = computed(() => {
    const sigla = this.siglaFilter();
    return this.rows().filter((r) => !sigla || r.sigla === sigla);
  });

  /** Siglas digitadas, separadas por vírgula ou espaço; a API valida o formato e recusa o resto. */
  protected readonly parsedSiglas = computed(() =>
    this.scanSiglas()
      .split(/[\s,;]+/)
      .map((s) => s.trim())
      .filter((s) => s.length > 0),
  );

  constructor() {
    this.reload();
    this.loadScanStatus();
  }

  protected setStatus(status: StatusFilter): void {
    this.statusFilter.set(status);
    this.siglaFilter.set('');
    this.closePanel();
    this.reload();
  }

  // ---- scan pela tela ----

  protected toggleScan(): void {
    this.scanOpen.set(!this.scanOpen());
  }

  protected runScan(dryRun: boolean): void {
    if (this.parsedSiglas().length === 0) {
      this.scanError.set('Informe ao menos uma sigla (ex.: ab1, cd2).');
      return;
    }

    this.scanning.set(true);
    this.scanError.set(null);
    this.scanReport.set(null);
    saveSiglas(this.scanSiglas());

    this.discoveredApi.scan(this.parsedSiglas(), dryRun).subscribe({
      next: (report) => {
        this.scanning.set(false);
        this.scanReport.set(report);
        if (!report.dryRun) {
          this.statusFilter.set('new');
          this.siglaFilter.set('');
          this.reload();
        }
      },
      error: (err) => {
        this.scanning.set(false);
        const body = (err as { error?: unknown })?.error;
        this.scanError.set(typeof body === 'string' ? body : 'Não foi possível executar o scan.');
        this.loadScanStatus();
      },
    });
  }

  // ---- triagem ----

  protected openCreate(row: DiscoveredRepository): void {
    this.panelFor.set(row.id);
    this.panelMode.set('create');
    this.formName.set(row.suggestion.name);
    this.formSquadId.set(this.currentSquad.squadId() ?? this.currentSquad.squads()[0]?.id ?? '');
    this.formDescription.set('');
    this.formRole.set(row.suggestion.role ?? '');
    this.actionError.set(null);
  }

  protected openLink(row: DiscoveredRepository): void {
    this.panelFor.set(row.id);
    this.panelMode.set('link');
    this.formAppId.set('');
    this.formRole.set(row.suggestion.role ?? '');
    this.actionError.set(null);
    this.loadAppOptions();
  }

  protected closePanel(): void {
    this.panelFor.set(null);
  }

  protected confirmCreate(row: DiscoveredRepository): void {
    if (!this.formName().trim()) {
      this.actionError.set('Nome do app é obrigatório.');
      return;
    }

    this.saving.set(true);
    this.actionError.set(null);
    this.discoveredApi
      .createApp(row.id, {
        name: this.formName().trim(),
        squadId: this.formSquadId(),
        description: this.formDescription().trim() || null,
        role: this.formRole().trim() || null,
      })
      .subscribe({
        next: (app) => {
          this.finish({ text: `App "${app.name}" criado e vinculado a ${row.name}.`, appId: app.id, warnings: [] });
        },
        error: (err) => this.fail(err, 'Não foi possível criar o app.'),
      });
  }

  protected confirmLink(row: DiscoveredRepository): void {
    if (!this.formAppId()) {
      this.actionError.set('Escolha o app.');
      return;
    }

    this.saving.set(true);
    this.actionError.set(null);
    const appId = this.formAppId();
    this.discoveredApi.link(row.id, { appId, role: this.formRole().trim() || null }).subscribe({
      next: (result) => this.finish({ text: `${row.name} vinculado ao app.`, appId, warnings: result.warnings }),
      error: (err) => this.fail(err, 'Não foi possível vincular o repositório.'),
    });
  }

  protected markOtherSquad(row: DiscoveredRepository): void {
    this.act(this.discoveredApi.markOtherSquad(row.id), `${row.name} marcado como de outra squad.`);
  }

  protected ignore(row: DiscoveredRepository): void {
    this.act(this.discoveredApi.ignore(row.id), `${row.name} ignorado.`);
  }

  protected restore(row: DiscoveredRepository): void {
    this.act(this.discoveredApi.restore(row.id), `${row.name} voltou para a caixa de entrada.`);
  }

  private act(request: ReturnType<DiscoveredApiService['ignore']>, text: string): void {
    this.actionError.set(null);
    request.subscribe({
      next: () => this.finish({ text, warnings: [] }),
      error: (err) => this.fail(err, 'Não foi possível concluir a ação.'),
    });
  }

  private finish(notice: { text: string; appId?: string; warnings: string[] }): void {
    this.saving.set(false);
    this.closePanel();
    this.notice.set(notice);
    this.reload();
    this.currentSquad.refresh();
  }

  private fail(err: unknown, fallback: string): void {
    this.saving.set(false);
    const body = (err as { error?: unknown })?.error;
    this.actionError.set(typeof body === 'string' ? body : fallback);
  }

  private reload(): void {
    this.loading.set(true);
    this.discoveredApi.list(this.statusFilter()).subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.error.set(false);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  private loadScanStatus(): void {
    this.discoveredApi.scanStatus().subscribe({
      next: (status) => {
        this.scanStatus.set(status);
        this.scanStatusFailed.set(false);
      },
      error: () => this.scanStatusFailed.set(true),
    });
  }

  /** Todos os apps, com a squad no rótulo — o vínculo pode ser com um app de qualquer squad. */
  private loadAppOptions(): void {
    this.squadsApi.list().subscribe((squads) => {
      if (squads.length === 0) {
        this.appOptions.set([]);
        return;
      }

      forkJoin(squads.map((s) => this.squadsApi.listApps(s.id))).subscribe((groups: AppSummary[][]) => {
        const options = groups.flatMap((apps, i) =>
          apps.map((a) => ({ id: a.id, label: `${a.name} — ${squads[i].name}` })),
        );
        this.appOptions.set(options.sort((a, b) => a.label.localeCompare(b.label)));
      });
    });
  }
}

// Conveniência por navegador: quem escaneia sempre as mesmas siglas não as digita de novo.
function readSavedSiglas(): string {
  try {
    return localStorage.getItem(SIGLAS_STORAGE_KEY) ?? '';
  } catch {
    return '';
  }
}

function saveSiglas(value: string): void {
  try {
    localStorage.setItem(SIGLAS_STORAGE_KEY, value);
  } catch {
    // Armazenamento bloqueado: só não lembra.
  }
}
