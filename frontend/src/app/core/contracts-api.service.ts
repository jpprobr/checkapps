import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import { AppContractChanges, AppContracts } from './models';

/** A análise roda pela ferramenta CheckApps.Analyze, nunca por esta API; aqui só se leem os contratos e o que mudou neles. */
@Injectable({ providedIn: 'root' })
export class ContractsApiService {
  private readonly http = inject(HttpClient);

  forApp(appId: string): Observable<AppContracts> {
    return this.http.get<AppContracts>(`${API_BASE_URL}/apps/${appId}/contracts`);
  }

  changesForApp(appId: string): Observable<AppContractChanges> {
    return this.http.get<AppContractChanges>(`${API_BASE_URL}/apps/${appId}/contracts/changes`);
  }
}
