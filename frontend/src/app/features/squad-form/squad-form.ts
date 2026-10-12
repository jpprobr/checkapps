import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { CurrentSquadService } from '../../core/current-squad.service';
import { SquadsApiService } from '../../core/squads-api.service';

@Component({
  selector: 'app-squad-form',
  imports: [FormsModule, RouterLink],
  templateUrl: './squad-form.html',
  styleUrl: './squad-form.css',
})
export class SquadForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly squadsApi = inject(SquadsApiService);
  private readonly currentSquad = inject(CurrentSquadService);

  protected readonly squadId = this.route.snapshot.paramMap.get('squadId');
  protected readonly isEditMode = this.squadId !== null;

  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly status = signal('active');
  protected readonly loading = signal(this.isEditMode);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    if (this.squadId) {
      this.squadsApi.get(this.squadId).subscribe({
        next: (squad) => {
          this.name.set(squad.name);
          this.description.set(squad.description ?? '');
          this.status.set(squad.status ?? 'active');
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Não foi possível carregar a squad.');
          this.loading.set(false);
        },
      });
    }
  }

  protected submit(): void {
    if (!this.name().trim()) {
      this.error.set('Nome é obrigatório.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const description = this.description().trim() || null;

    const request$ = this.isEditMode
      ? this.squadsApi.update(this.squadId!, { name: this.name().trim(), description, status: this.status() })
      : this.squadsApi.create({ name: this.name().trim(), description });

    request$.subscribe({
      next: (squad) => {
        this.currentSquad.refresh();
        this.router.navigate(['/squads', squad.id]);
      },
      error: () => {
        this.error.set('Não foi possível salvar a squad.');
        this.saving.set(false);
      },
    });
  }
}
