import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import {
  AppDetail,
  CreateAppFromDiscoveryRequest,
  DiscoveredRepository,
  LinkDiscoveryRequest,
  TriageStatus,
} from './models';

@Injectable({ providedIn: 'root' })
export class DiscoveredApiService {
  private readonly http = inject(HttpClient);

  list(status: TriageStatus | 'all'): Observable<DiscoveredRepository[]> {
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

  restore(id: string): Observable<void> {
    return this.http.post<void>(`${API_BASE_URL}/discovered-repositories/${id}/restore`, {});
  }
}
