import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import { AppIntegrations, HostDetail, HostList } from './models';

/** Só leitura: a análise roda pela ferramenta CheckApps.Analyze, nunca por esta API. */
@Injectable({ providedIn: 'root' })
export class IntegrationsApiService {
  private readonly http = inject(HttpClient);

  forApp(appId: string): Observable<AppIntegrations> {
    return this.http.get<AppIntegrations>(`${API_BASE_URL}/apps/${appId}/integrations`);
  }

  hosts(host: string, squadId: string | null): Observable<HostList> {
    let params = new HttpParams();
    if (host.trim()) {
      params = params.set('host', host.trim());
    }
    if (squadId) {
      params = params.set('squadId', squadId);
    }
    return this.http.get<HostList>(`${API_BASE_URL}/integrations/hosts`, { params });
  }

  /** A chave tem ":" e "{env}": vai codificada na URL. */
  hostDetail(key: string): Observable<HostDetail> {
    return this.http.get<HostDetail>(`${API_BASE_URL}/integrations/hosts/${encodeURIComponent(key)}`);
  }
}
