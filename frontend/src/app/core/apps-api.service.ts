import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import { AppDetail, AppSummary, QualityLevel, QualitySnapshot, RepositoryInfo, SonarProjectInfo } from './models';

@Injectable({ providedIn: 'root' })
export class AppsApiService {
  private readonly http = inject(HttpClient);

  get(appId: string): Observable<AppDetail> {
    return this.http.get<AppDetail>(`${API_BASE_URL}/apps/${appId}`);
  }

  listByQualityLevel(level: QualityLevel): Observable<AppSummary[]> {
    return this.http.get<AppSummary[]>(`${API_BASE_URL}/apps`, { params: { qualityLevel: level } });
  }

  listRepositories(appId: string): Observable<RepositoryInfo[]> {
    return this.http.get<RepositoryInfo[]>(`${API_BASE_URL}/apps/${appId}/repositories`);
  }

  listSonarProjects(appId: string): Observable<SonarProjectInfo[]> {
    return this.http.get<SonarProjectInfo[]>(`${API_BASE_URL}/apps/${appId}/sonar-projects`);
  }

  getQualityHistory(appId: string): Observable<QualitySnapshot[]> {
    return this.http.get<QualitySnapshot[]>(`${API_BASE_URL}/apps/${appId}/quality/history`);
  }
}
