import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import { AppDependencies, FrameworkOverview, PackageSearchResult } from './models';

/** Só leitura: a análise roda pela ferramenta CheckApps.Analyze, nunca por esta API. */
@Injectable({ providedIn: 'root' })
export class DependenciesApiService {
  private readonly http = inject(HttpClient);

  forApp(appId: string): Observable<AppDependencies> {
    return this.http.get<AppDependencies>(`${API_BASE_URL}/apps/${appId}/dependencies`);
  }

  searchPackages(name: string, squadId: string | null, internalOnly: boolean): Observable<PackageSearchResult> {
    let params = new HttpParams().set('internalOnly', internalOnly);
    if (name.trim()) {
      params = params.set('name', name.trim());
    }
    if (squadId) {
      params = params.set('squadId', squadId);
    }
    return this.http.get<PackageSearchResult>(`${API_BASE_URL}/dependencies/packages`, { params });
  }

  frameworks(): Observable<FrameworkOverview> {
    return this.http.get<FrameworkOverview>(`${API_BASE_URL}/dependencies/frameworks`);
  }
}
