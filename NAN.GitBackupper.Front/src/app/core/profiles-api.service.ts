import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../environment';
import type { BackupArchiveFileInfo, GitRepositoryDescriptor, ProfileDiskUsageInfo } from './api-types';

@Injectable({ providedIn: 'root' })
export class ProfilesApiService {
  private readonly base = `${environment.apiUrl}/api/profiles`;

  constructor(private readonly http: HttpClient) {}

  listRepositories(profileId: string): Observable<GitRepositoryDescriptor[]> {
    return this.http.get<GitRepositoryDescriptor[]>(`${this.base}/${profileId}/repositories`);
  }

  listRepositoriesCached(
    profileId: string,
    sort?: string,
    order?: 'asc' | 'desc',
  ): Observable<GitRepositoryDescriptor[]> {
    const url = `${this.base}/${profileId}/repositories/cached`;
    let params = new HttpParams();
    if (sort) {
      params = params.set('sort', sort);
      if (order) params = params.set('order', order);
    }
    return this.http.get(url, { params, responseType: 'text' }).pipe(
      map((body) => this.parseCachedReposBody(body)),
    );
  }

  /** Без автоматического json-парса HttpClient: пустой/HTML/проблемное тело не даёт «Http failure during parsing». */
  private parseCachedReposBody(body: string | null): GitRepositoryDescriptor[] {
    const s = (body ?? '').trim().replace(/^\uFEFF/, '');
    if (!s) return [];
    if (!environment.production && (s.startsWith('<!DOCTYPE') || s.startsWith('<html'))) {
      console.warn(
        '[GitBackupper] GET .../repositories/cached вернул HTML (часто wwwroot/index.html или старый API без маршрута). Проверьте сборку и логи NAN.GitBackupper.Api.SpaFallback.',
      );
    }
    try {
      const v = JSON.parse(s) as unknown;
      return Array.isArray(v) ? (v as GitRepositoryDescriptor[]) : [];
    } catch {
      return [];
    }
  }

  refreshRepositories(profileId: string): Observable<GitRepositoryDescriptor[]> {
    return this.http.post<GitRepositoryDescriptor[]>(`${this.base}/${profileId}/repositories/refresh`, {});
  }

  listBranches(profileId: string, repository: GitRepositoryDescriptor): Observable<string[]> {
    return this.http.post<string[]>(`${this.base}/${profileId}/repositories/branches`, repository);
  }

  runBackup(profileId: string): Observable<unknown> {
    return this.http.post(`${this.base}/${profileId}/backup/run`, {}, {});
  }

  cancelBackup(profileId: string): Observable<unknown> {
    return this.http.post(`${this.base}/${profileId}/backup/cancel`, {}, {});
  }

  dismissBackupProgressLog(profileId: string): Observable<unknown> {
    return this.http.post(`${this.base}/${profileId}/backup/progress-log/dismiss`, {}, {});
  }

  getDiskUsage(profileId: string): Observable<ProfileDiskUsageInfo> {
    return this.http.get<ProfileDiskUsageInfo>(`${this.base}/${profileId}/disk-usage`);
  }

  listBackupArchives(
    profileId: string,
    sort?: string,
    order?: 'asc' | 'desc',
  ): Observable<BackupArchiveFileInfo[]> {
    let params = new HttpParams();
    if (sort) {
      params = params.set('sort', sort);
      if (order) params = params.set('order', order);
    }
    return this.http.get<BackupArchiveFileInfo[]>(`${this.base}/${profileId}/backup-archives`, {
      params,
    });
  }

  /** Скачать zip; вызывающий сохраняет blob (например через createObjectURL). */
  getBackupArchiveBlob(profileId: string, fileName: string): Observable<Blob> {
    return this.http.get(`${this.base}/${profileId}/backup-archives/download`, {
      params: { fileName },
      responseType: 'blob',
    });
  }
}
