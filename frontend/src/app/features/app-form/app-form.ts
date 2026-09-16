import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AppsApiService } from '../../core/apps-api.service';
import { SquadsApiService } from '../../core/squads-api.service';
import { AppLink, Squad } from '../../core/models';

@Component({
  selector: 'app-app-form',
  imports: [FormsModule, RouterLink],
  templateUrl: './app-form.html',
  styleUrl: './app-form.css',
})
export class AppForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly appsApi = inject(AppsApiService);
  private readonly squadsApi = inject(SquadsApiService);

  protected readonly appId = this.route.snapshot.paramMap.get('appId');
  protected readonly isEditMode = this.appId !== null;

  protected readonly squads = signal<Squad[]>([]);
  protected readonly name = signal('');
  protected readonly squadId = signal(this.route.snapshot.paramMap.get('squadId') ?? '');
  protected readonly description = signal('');
  protected readonly criticality = signal('');
  protected readonly tagsInput = signal('');
  protected readonly status = signal('active');
  protected readonly loading = signal(this.isEditMode);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly links = signal<AppLink[]>([]);
  protected readonly newLinkDescription = signal('');
  protected readonly newLinkUri = signal('');
  protected readonly linkError = signal<string | null>(null);

  constructor() {
    this.squadsApi.list().subscribe((squads) => this.squads.set(squads));

    if (this.appId) {
      this.appsApi.get(this.appId).subscribe({
        next: (app) => {
          this.name.set(app.name);
          this.squadId.set(app.squadId);
          this.description.set(app.description ?? '');
          this.criticality.set(app.criticality ?? '');
          this.tagsInput.set(app.tags.join(', '));
          this.status.set(app.status ?? 'active');
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Não foi possível carregar o app.');
          this.loading.set(false);
        },
      });
      this.reloadLinks();
    }
  }

  private reloadLinks(): void {
    this.appsApi.listLinks(this.appId!).subscribe((links) => this.links.set(links));
  }

  private parsedTags(): string[] {
    return this.tagsInput()
      .split(',')
      .map((t) => t.trim())
      .filter((t) => t.length > 0);
  }

  protected submit(): void {
    if (!this.name().trim() || !this.squadId()) {
      this.error.set('Nome e Squad são obrigatórios.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const description = this.description().trim() || null;
    const criticality = this.criticality().trim() || null;
    const tags = this.parsedTags();

    const request$ = this.isEditMode
      ? this.appsApi.update(this.appId!, {
          name: this.name().trim(),
          squadId: this.squadId(),
          description,
          criticality,
          tags,
          status: this.status(),
          externalId: null,
        })
      : this.appsApi.create({
          name: this.name().trim(),
          squadId: this.squadId(),
          description,
          criticality,
          tags,
          externalId: null,
        });

    request$.subscribe({
      next: (app) => this.router.navigate(['/apps', app.id]),
      error: (err) => {
        this.error.set(typeof err?.error === 'string' ? err.error : 'Não foi possível salvar o app.');
        this.saving.set(false);
      },
    });
  }

  protected addLink(): void {
    if (!this.newLinkDescription().trim() || !this.newLinkUri().trim()) {
      this.linkError.set('Descrição e Endereço são obrigatórios.');
      return;
    }

    this.appsApi
      .addLink(this.appId!, { description: this.newLinkDescription().trim(), uri: this.newLinkUri().trim() })
      .subscribe({
        next: () => {
          this.newLinkDescription.set('');
          this.newLinkUri.set('');
          this.linkError.set(null);
          this.reloadLinks();
        },
        error: () => this.linkError.set('Não foi possível adicionar o link.'),
      });
  }

  protected removeLink(linkId: string): void {
    this.appsApi.deleteLink(this.appId!, linkId).subscribe(() => this.reloadLinks());
  }
}
