import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import { AppMap } from './models';

/** O mapa é derivado das integrações e das decisões da tela Integrações; aqui só se lê. */
@Injectable({ providedIn: 'root' })
export class MapApiService {
  private readonly http = inject(HttpClient);

  map(squadId: string | null): Observable<AppMap> {
    let params = new HttpParams();
    if (squadId) {
      params = params.set('squadId', squadId);
    }
    return this.http.get<AppMap>(`${API_BASE_URL}/map`, { params });
  }
}
