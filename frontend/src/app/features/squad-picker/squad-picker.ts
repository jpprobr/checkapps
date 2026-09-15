import { Component, inject, signal } from '@angular/core';
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

  constructor() {
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
}
