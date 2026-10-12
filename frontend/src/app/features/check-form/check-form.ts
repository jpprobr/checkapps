import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ChecksApiService } from '../../core/checks-api.service';

@Component({
  selector: 'app-check-form',
  imports: [FormsModule, RouterLink],
  templateUrl: './check-form.html',
  styleUrl: './check-form.css',
})
export class CheckForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly checksApi = inject(ChecksApiService);

  protected readonly checkId = this.route.snapshot.paramMap.get('checkId');
  protected readonly isEditMode = this.checkId !== null;

  protected readonly title = signal('');
  protected readonly description = signal('');
  protected readonly category = signal('');
  protected readonly author = signal('');
  protected readonly startDate = signal('');
  protected readonly endDate = signal('');
  protected readonly loading = signal(this.isEditMode);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    if (this.checkId) {
      this.checksApi.get(this.checkId).subscribe({
        next: (check) => {
          this.title.set(check.title);
          this.description.set(check.description ?? '');
          this.category.set(check.category ?? '');
          this.author.set(check.author ?? '');
          this.startDate.set(check.startDate ?? '');
          this.endDate.set(check.endDate ?? '');
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Não foi possível carregar a checagem.');
          this.loading.set(false);
        },
      });
    }
  }

  protected submit(): void {
    if (!this.title().trim()) {
      this.error.set('Título é obrigatório.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const request = {
      title: this.title().trim(),
      description: this.description().trim() || null,
      category: this.category().trim() || null,
      author: this.author().trim() || null,
      startDate: this.startDate() || null,
      endDate: this.endDate() || null,
    };

    const request$ = this.isEditMode ? this.checksApi.update(this.checkId!, request) : this.checksApi.create(request);

    request$.subscribe({
      next: (check) => this.router.navigate(['/checks', check.id]),
      error: (err) => {
        this.error.set(typeof err?.error === 'string' ? err.error : 'Não foi possível salvar a checagem.');
        this.saving.set(false);
      },
    });
  }
}
