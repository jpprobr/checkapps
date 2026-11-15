import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { CurrentSquadService } from '../../core/current-squad.service';
import { LEVEL_SUGGESTIONS, messageOf, pathLabels, subtreeIds } from '../../core/journey-labels';
import { JourneysApiService } from '../../core/journeys-api.service';
import { JourneySummary } from '../../core/models';

interface ParentOption {
  id: string;
  label: string;
  /** Jornada com passos não aceita jornadas dentro (só as folhas têm passos). */
  disabled: boolean;
}

/** Cadastro e edição de uma jornada (spec 022): nome, nível (texto livre com sugestões), squad e pai opcionais, tags. */
@Component({
  selector: 'app-journey-form',
  imports: [FormsModule, RouterLink],
  templateUrl: './journey-form.html',
  styleUrl: './journey-form.css',
})
export class JourneyForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(JourneysApiService);
  protected readonly currentSquad = inject(CurrentSquadService);

  protected readonly journeyId = this.route.snapshot.paramMap.get('journeyId');
  protected readonly isEditMode = this.journeyId !== null;
  protected readonly levelSuggestions = LEVEL_SUGGESTIONS;

  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly level = signal('');
  protected readonly squadId = signal('');
  protected readonly parentId = signal(this.route.snapshot.queryParamMap.get('parentId') ?? '');
  protected readonly tagsInput = signal('');

  private readonly all = signal<JourneySummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Os pais possíveis: tudo, menos a própria jornada e o que está dentro dela (formaria um ciclo). */
  protected readonly parentOptions = computed<ParentOption[]>(() => {
    const all = this.all();
    const labels = pathLabels(all);
    const excluded = this.journeyId ? subtreeIds(all, this.journeyId) : new Set<string>();
    return all
      .filter((j) => !excluded.has(j.id))
      .map((j) => ({ id: j.id, label: labels.get(j.id) ?? j.name, disabled: j.stepCount > 0 }))
      .sort((a, b) => a.label.localeCompare(b.label, 'pt-BR', { sensitivity: 'base' }));
  });

  constructor() {
    this.api.list().subscribe({
      next: (journeys) => {
        this.all.set(journeys);
        if (this.journeyId) {
          this.loadForEdit(journeys);
        } else {
          this.loading.set(false);
        }
      },
      error: () => {
        this.error.set('Não foi possível carregar as jornadas.');
        this.loading.set(false);
      },
    });
  }

  private loadForEdit(journeys: JourneySummary[]): void {
    this.api.get(this.journeyId!).subscribe({
      next: (detail) => {
        const journey = detail.journey;
        this.name.set(journey.name);
        this.description.set(journey.description ?? '');
        this.level.set(journey.level ?? '');
        this.squadId.set(journey.squadId ?? '');
        this.parentId.set(journey.parentId ?? '');
        this.tagsInput.set(journey.tags.join(', '));
        this.all.set(journeys);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar a jornada.');
        this.loading.set(false);
      },
    });
  }

  protected submit(): void {
    this.error.set(null);
    this.saving.set(true);

    const request = {
      name: this.name().trim(),
      description: this.description().trim() || null,
      level: this.level().trim() || null,
      squadId: this.squadId() || null,
      parentId: this.parentId() || null,
      tags: this.tagsInput()
        .split(',')
        .map((t) => t.trim())
        .filter((t) => t.length > 0),
    };

    const call = this.journeyId ? this.api.update(this.journeyId, request) : this.api.create(request);
    call.subscribe({
      next: (journey) => void this.router.navigate(['/journeys', journey.id]),
      error: (err) => {
        this.error.set(messageOf(err, 'Não foi possível salvar a jornada.'));
        this.saving.set(false);
      },
    });
  }
}
