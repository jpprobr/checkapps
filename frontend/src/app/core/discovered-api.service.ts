import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import {
  AppDetail,
  CreateAppFromDiscoveryRequest,
  DiscoveredRepository,
  LinkDiscoveryRequest,
  ScanReport,
  ScanStatus,
  TriageStatus,
} from './models';

@Injectable({ providedIn: 'root' })
export class DiscoveredApiService {
  private readonly http = inject(HttpClient);

  /** `catalog`: só os que já estão no catálogo; `all`: todos; os demais: só os que aguardam decisão. */
  list(status: TriageStatus | 'catalog' | 'all'): Observable<DiscoveredRepository[]> {
    return this.http.get<DiscoveredRepository[]>(`${API_BASE_URL}/discovered-repositories`, { params: { status } });
  }

  createApp(id: string, request: CreateAppFromDiscoveryRequest): Observable<AppDetail> {
    return this.http.post<AppDetail>(`${API_BASE_URL}/discovered-repositories/${id}/create-app`, request);
  }

  link(id: string, request: LinkDiscoveryRequest): Observable<{ warnings: string[] }> {
    return this.http.post<{ warnings: string[] }>(`${API_BASE_URL}/discovered-repositories/${id}/link`, request);
  }

  markOtherSquad(id: string): Observable<void> {
    return this.http.post<void>(`${API_BASE_URL}/discovered-repositories/${id}/other-squad`, {});
  }

  ignore(id: string): Observable<void> {
    return this.http.post<void>(`${API_BASE_URL}/discovered-repositories/${id}/ignore`, {});
  }

  scanStatus(): Observable<ScanStatus> {
    return this.http.get<ScanStatus>(`${API_BASE_URL}/discovered-repositories/scan/status`);
  }

  scan(siglas: string[], dryRun: boolean): Observable<ScanReport> {
    return this.http.post<ScanReport>(`${API_BASE_URL}/discovered-repositories/scan`, { siglas, dryRun });
  }

  restore(id: string): Observable<void> {
    return this.http.post<void>(`${API_BASE_URL}/discovered-repositories/${id}/restore`, {});
  }
}
