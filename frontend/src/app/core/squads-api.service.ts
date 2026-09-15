import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import { AppSummary, Squad, SquadQualityEvolution } from './models';

@Injectable({ providedIn: 'root' })
export class SquadsApiService {
  private readonly http = inject(HttpClient);

  list(): Observable<Squad[]> {
    return this.http.get<Squad[]>(`${API_BASE_URL}/squads`);
  }

  get(squadId: string): Observable<Squad> {
    return this.http.get<Squad>(`${API_BASE_URL}/squads/${squadId}`);
  }

  listApps(squadId: string): Observable<AppSummary[]> {
    return this.http.get<AppSummary[]>(`${API_BASE_URL}/squads/${squadId}/apps`);
  }

  getQualityEvolution(squadId: string, from?: string, to?: string): Observable<SquadQualityEvolution> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<SquadQualityEvolution>(`${API_BASE_URL}/squads/${squadId}/quality-evolution`, { params });
  }
}
