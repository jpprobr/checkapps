import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AppsApiService } from '../../core/apps-api.service';
import { SquadsApiService } from '../../core/squads-api.service';
import { AppLink, RepositoryInfo, Squad } from '../../core/models';

interface RepositoryDraft {
  name: string;
  provider: string;
  url: string;
  sigla: string;
  siglaApp: string;
  role: string;
  status: string;
}

function emptyDraft(): RepositoryDraft {
  return { name: '', provider: '', url: '', sigla: '', siglaApp: '', role: '', status: 'active' };
}

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

  protected readonly repositories = signal<RepositoryInfo[]>([]);
  protected readonly repoDraft = signal<RepositoryDraft>(emptyDraft());
  /** Id do repositório em edição; null = formulário de novo repositório. */
  protected readonly editingRepoId = signal<string | null>(null);
  protected readonly repoError = signal<string | null>(null);
  protected readonly repoNotice = signal<string | null>(null);

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
      this.reloadRepositories();
    }
  }

  private reloadRepositories(): void {
    this.appsApi.listRepositories(this.appId!).subscribe((repos) => this.repositories.set(repos));
  }

  protected setDraft(field: keyof RepositoryDraft, value: string): void {
    this.repoDraft.update((d) => ({ ...d, [field]: value }));
  }

  protected editRepository(repo: RepositoryInfo): void {
    this.editingRepoId.set(repo.id);
    this.repoError.set(null);
    this.repoNotice.set(null);
    this.repoDraft.set({
      name: repo.name,
      provider: repo.provider ?? '',
      url: repo.url ?? '',
      sigla: repo.sigla ?? '',
      siglaApp: repo.siglaApp ?? '',
      role: repo.role ?? '',
      status: repo.status ?? 'active',
    });
  }

  protected cancelRepositoryEdit(): void {
    this.editingRepoId.set(null);
    this.repoError.set(null);
    this.repoDraft.set(emptyDraft());
  }

  protected saveRepository(): void {
    const d = this.repoDraft();
    if (!d.name.trim()) {
      this.repoError.set('O nome do repositório é obrigatório.');
      return;
    }

    const blankToNull = (v: string) => v.trim() || null;
    const request = {
      name: d.name.trim(),
      provider: blankToNull(d.provider),
      url: blankToNull(d.url),
      status: blankToNull(d.status),
      sigla: blankToNull(d.sigla),
      siglaApp: blankToNull(d.siglaApp),
      role: blankToNull(d.role),
    };
    const editingId = this.editingRepoId();
    const request$ = editingId
      ? this.appsApi.updateRepository(this.appId!, editingId, request)
      : this.appsApi.addRepository(this.appId!, request);

    request$.subscribe({
      next: () => {
        this.cancelRepositoryEdit();
        this.reloadRepositories();
      },
      error: (err) =>
        this.repoError.set(typeof err?.error === 'string' ? err.error : 'Não foi possível salvar o repositório.'),
    });
  }

  protected activateRepository(repo: RepositoryInfo): void {
    this.repoError.set(null);
    this.repoNotice.set(null);
    this.appsApi.activateRepository(this.appId!, repo.id).subscribe({
      next: (result) => {
        if (result.demoted.length > 0) {
          this.repoNotice.set(`"${repo.name}" agora é o ativo. Passou a legacy: ${result.demoted.join(', ')}.`);
        }
        this.reloadRepositories();
      },
      error: (err) =>
        this.repoError.set(typeof err?.error === 'string' ? err.error : 'Não foi possível marcar o repositório como ativo.'),
    });
  }

  protected removeRepository(repo: RepositoryInfo): void {
    if (!confirm(`Remover o repositório "${repo.name}" deste app?`)) {
      return;
    }
    this.appsApi.deleteRepository(this.appId!, repo.id).subscribe({
      next: () => {
        if (this.editingRepoId() === repo.id) {
          this.cancelRepositoryEdit();
        }
        this.reloadRepositories();
      },
      error: (err) =>
        this.repoError.set(typeof err?.error === 'string' ? err.error : 'Não foi possível remover o repositório.'),
    });
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
