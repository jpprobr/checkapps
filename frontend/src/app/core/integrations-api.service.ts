import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import { AppIntegrations, HostDetail, HostLinkMatch, HostLinkStatus, HostList, SetHostLinkRequest } from './models';

/** A análise roda pela ferramenta CheckApps.Analyze, nunca por esta API; aqui só se leem os fatos e se gravam os vínculos host→app (decisão humana). */
@Injectable({ providedIn: 'root' })
export class IntegrationsApiService {
  private readonly http = inject(HttpClient);

  forApp(appId: string): Observable<AppIntegrations> {
    return this.http.get<AppIntegrations>(`${API_BASE_URL}/apps/${appId}/integrations`);
  }

  hosts(host: string, squadId: string | null, linkStatus: HostLinkStatus | '' = ''): Observable<HostList> {
    let params = new HttpParams();
    if (host.trim()) {
      params = params.set('host', host.trim());
    }
    if (squadId) {
      params = params.set('squadId', squadId);
    }
    if (linkStatus) {
      params = params.set('linkStatus', linkStatus);
    }
    return this.http.get<HostList>(`${API_BASE_URL}/integrations/hosts`, { params });
  }

  /** A chave tem ":" e "{env}": vai codificada na URL. */
  hostDetail(key: string): Observable<HostDetail> {
    return this.http.get<HostDetail>(`${API_BASE_URL}/integrations/hosts/${encodeURIComponent(key)}`);
  }

  /** Cria ou troca o vínculo do par host+app. 409 se o host já está confirmado para outro app (texto no corpo). */
  setLink(hostKey: string, request: SetHostLinkRequest): Observable<HostLinkMatch> {
    return this.http.put<HostLinkMatch>(`${API_BASE_URL}/integrations/hosts/${encodeURIComponent(hostKey)}/link`, request);
  }

  removeLink(hostKey: string, appId: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE_URL}/integrations/hosts/${encodeURIComponent(hostKey)}/link/${appId}`);
  }
}
