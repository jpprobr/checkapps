import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';

import { API_BASE_URL } from './api-config';

export interface AppInfo {
  /** `real` ou `demo` (spec 023). */
  mode: 'real' | 'demo';
  /** Só o nome do arquivo do banco. */
  database: string;
}

/** Em que modo a API está (spec 023). Lido uma vez; se a leitura falhar, nada é mostrado (o modo real é o padrão). */
@Injectable({ providedIn: 'root' })
export class AppInfoService {
  private readonly http = inject(HttpClient);

  readonly info = signal<AppInfo | null>(null);
  readonly isDemo = computed(() => this.info()?.mode === 'demo');

  constructor() {
    this.http.get<AppInfo>(`${API_BASE_URL}/info`).subscribe({
      next: (info) => this.info.set(info),
      error: () => this.info.set(null),
    });
  }
}
