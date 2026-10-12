import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import {
  AppCheck,
  CheckCandidate,
  CheckDetail,
  CheckEvent,
  CheckItemStatus,
  CheckSummary,
  UpsertCheckRequest,
} from './models';

@Injectable({ providedIn: 'root' })
export class ChecksApiService {
  private readonly http = inject(HttpClient);

  list(includeClosed: boolean): Observable<CheckSummary[]> {
    return this.http.get<CheckSummary[]>(`${API_BASE_URL}/checks`, { params: { includeClosed } });
  }

  get(checkId: string): Observable<CheckDetail> {
    return this.http.get<CheckDetail>(`${API_BASE_URL}/checks/${checkId}`);
  }

  create(request: UpsertCheckRequest): Observable<CheckDetail> {
    return this.http.post<CheckDetail>(`${API_BASE_URL}/checks`, request);
  }

  update(checkId: string, request: UpsertCheckRequest): Observable<CheckDetail> {
    return this.http.put<CheckDetail>(`${API_BASE_URL}/checks/${checkId}`, request);
  }

  close(checkId: string): Observable<CheckDetail> {
    return this.http.post<CheckDetail>(`${API_BASE_URL}/checks/${checkId}/close`, {});
  }

  reopen(checkId: string): Observable<CheckDetail> {
    return this.http.post<CheckDetail>(`${API_BASE_URL}/checks/${checkId}/reopen`, {});
  }

  delete(checkId: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE_URL}/checks/${checkId}`);
  }

  listCandidates(checkId: string): Observable<CheckCandidate[]> {
    return this.http.get<CheckCandidate[]>(`${API_BASE_URL}/checks/${checkId}/candidates`);
  }

  addApps(checkId: string, appIds: string[]): Observable<{ added: number }> {
    return this.http.post<{ added: number }>(`${API_BASE_URL}/checks/${checkId}/items`, { appIds });
  }

  updateItem(checkId: string, appId: string, status: CheckItemStatus, notes: string | null): Observable<CheckDetail> {
    return this.http.put<CheckDetail>(`${API_BASE_URL}/checks/${checkId}/items/${appId}`, { status, notes });
  }

  removeApp(checkId: string, appId: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE_URL}/checks/${checkId}/items/${appId}`);
  }

  getHistory(checkId: string): Observable<CheckEvent[]> {
    return this.http.get<CheckEvent[]>(`${API_BASE_URL}/checks/${checkId}/history`);
  }

  listByApp(appId: string): Observable<AppCheck[]> {
    return this.http.get<AppCheck[]>(`${API_BASE_URL}/apps/${appId}/checks`);
  }
}
