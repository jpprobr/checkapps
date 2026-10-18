import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-config';
import {
  AppDetail,
  AppLink,
  AppSummary,
  CreateAppRequest,
  QualityLevel,
  QualitySnapshot,
  RepositoryInfo,
  SonarProjectInfo,
  UpdateAppRequest,
  UpsertAppLinkRequest,
  UpsertRepositoryRequest,
} from './models';

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

  create(request: CreateAppRequest): Observable<AppDetail> {
    return this.http.post<AppDetail>(`${API_BASE_URL}/apps`, request);
  }

  update(appId: string, request: UpdateAppRequest): Observable<AppDetail> {
    return this.http.put<AppDetail>(`${API_BASE_URL}/apps/${appId}`, request);
  }

  delete(appId: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE_URL}/apps/${appId}`);
  }

  addRepository(appId: string, request: UpsertRepositoryRequest): Observable<RepositoryInfo> {
    return this.http.post<RepositoryInfo>(`${API_BASE_URL}/apps/${appId}/repositories`, request);
  }

  updateRepository(appId: string, repositoryId: string, request: UpsertRepositoryRequest): Observable<RepositoryInfo> {
    return this.http.put<RepositoryInfo>(`${API_BASE_URL}/apps/${appId}/repositories/${repositoryId}`, request);
  }

  deleteRepository(appId: string, repositoryId: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE_URL}/apps/${appId}/repositories/${repositoryId}`);
  }

  listLinks(appId: string): Observable<AppLink[]> {
    return this.http.get<AppLink[]>(`${API_BASE_URL}/apps/${appId}/links`);
  }

  addLink(appId: string, request: UpsertAppLinkRequest): Observable<AppLink> {
    return this.http.post<AppLink>(`${API_BASE_URL}/apps/${appId}/links`, request);
  }

  updateLink(appId: string, linkId: string, request: UpsertAppLinkRequest): Observable<AppLink> {
    return this.http.put<AppLink>(`${API_BASE_URL}/apps/${appId}/links/${linkId}`, request);
  }

  deleteLink(appId: string, linkId: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE_URL}/apps/${appId}/links/${linkId}`);
  }
}
