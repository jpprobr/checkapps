import { Injectable, computed, inject, signal } from '@angular/core';

import { Squad } from './models';
import { SquadsApiService } from './squads-api.service';

const STORAGE_KEY = 'checkapps.lastSquadId';

/**
 * Squad em contexto: a que a pessoa está olhando agora e a última visitada.
 * Alimenta o seletor do header e o redirecionamento de "/". As telas de squad e de app
 * informam a squad delas com {@link set}.
 *
 * Sem autenticação (ADR-007) não há "minha squad"; lembrar a última visitada no
 * navegador é o paliativo — quando houver identidade, vira o default real.
 */
@Injectable({ providedIn: 'root' })
export class CurrentSquadService {
  private readonly squadsApi = inject(SquadsApiService);

  readonly squadId = signal<string | null>(readStored());
  readonly squads = signal<Squad[]>([]);
  readonly squadName = computed(() => this.squads().find((s) => s.id === this.squadId())?.name ?? null);

  constructor() {
    this.refresh();
  }

  set(squadId: string): void {
    this.squadId.set(squadId);
    try {
      localStorage.setItem(STORAGE_KEY, squadId);
    } catch {
      // Armazenamento bloqueado (janela privada, política do navegador): só não lembra.
    }
  }

  /** Esquece a squad em contexto — ex.: ela foi removida ou não existe mais. */
  clear(squadId?: string): void {
    if (squadId && this.squadId() !== squadId) {
      return;
    }
    this.squadId.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // idem
    }
  }

  /** Recarrega a lista do seletor (após criar, renomear, inativar ou remover squad). */
  refresh(): void {
    this.squadsApi.list().subscribe({
      next: (squads) => this.squads.set(squads),
      error: () => this.squads.set([]),
    });
  }
}

function readStored(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}
