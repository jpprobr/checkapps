import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import { Journey, JourneyDetail, JourneyImpact, JourneyRequest, JourneyStepRequest, JourneySummary } from './models';

/** A resolução dos passos e o impacto das mudanças de contrato são calculados pela API a cada leitura; a análise roda pela ferramenta CheckApps.Analyze. */
@Injectable({ providedIn: 'root' })
export class JourneysApiService {
  private readonly http = inject(HttpClient);

  list(): Observable<JourneySummary[]> {
    return this.http.get<JourneySummary[]>(`${API_BASE_URL}/journeys`);
  }

  get(journeyId: string): Observable<JourneyDetail> {
    return this.http.get<JourneyDetail>(`${API_BASE_URL}/journeys/${journeyId}`);
  }

  create(request: JourneyRequest): Observable<Journey> {
    return this.http.post<Journey>(`${API_BASE_URL}/journeys`, request);
  }

  update(journeyId: string, request: JourneyRequest): Observable<Journey> {
    return this.http.put<Journey>(`${API_BASE_URL}/journeys/${journeyId}`, request);
  }

  delete(journeyId: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE_URL}/journeys/${journeyId}`);
  }

  /** Substitui a lista inteira de passos e devolve a jornada com os passos já resolvidos. */
  replaceSteps(journeyId: string, steps: JourneyStepRequest[]): Observable<JourneyDetail> {
    return this.http.put<JourneyDetail>(`${API_BASE_URL}/journeys/${journeyId}/steps`, { steps });
  }

  impact(journeyId: string): Observable<JourneyImpact> {
    return this.http.get<JourneyImpact>(`${API_BASE_URL}/journeys/${journeyId}/impact`);
  }
}
