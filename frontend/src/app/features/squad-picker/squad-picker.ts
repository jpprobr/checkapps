import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { SquadsApiService } from '../../core/squads-api.service';
import { Squad } from '../../core/models';

@Component({
  selector: 'app-squad-picker',
  imports: [RouterLink],
  templateUrl: './squad-picker.html',
  styleUrl: './squad-picker.css',
})
export class SquadPicker {
  private readonly squadsApi = inject(SquadsApiService);

  protected readonly squads = signal<Squad[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly showInactive = signal(false);
  protected readonly actionError = signal<string | null>(null);

  protected readonly visibleSquads = computed(() =>
    this.showInactive() ? this.squads() : this.squads().filter((s) => s.status !== 'inactive'),
  );

  constructor() {
    this.reload();
  }

  private reload(): void {
    this.loading.set(true);
    this.squadsApi.list().subscribe({
      next: (squads) => {
        this.squads.set(squads);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  protected toggleStatus(squad: Squad): void {
    const nextStatus = squad.status === 'inactive' ? 'active' : 'inactive';
    this.squadsApi.update(squad.id, { name: squad.name, description: squad.description, status: nextStatus }).subscribe({
      next: () => this.reload(),
      error: () => this.actionError.set(`Não foi possível atualizar o status de "${squad.name}".`),
    });
  }

  protected remove(squad: Squad): void {
    if (!confirm(`Remover a squad "${squad.name}"? Essa ação não pode ser desfeita.`)) {
      return;
    }

    this.actionError.set(null);
    this.squadsApi.delete(squad.id).subscribe({
      next: () => this.reload(),
      error: (err) =>
        this.actionError.set(typeof err?.error === 'string' ? err.error : `Não foi possível remover "${squad.name}".`),
    });
  }
}
