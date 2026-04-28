import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environment';
import type { SettingsModel } from './api-types';

@Injectable({ providedIn: 'root' })
export class SettingsApiService {
  private readonly base = `${environment.apiUrl}/api`;

  constructor(private readonly http: HttpClient) {}

  getSettings(sort?: string, order?: 'asc' | 'desc'): Observable<SettingsModel> {
    let params = new HttpParams();
    if (sort) {
      params = params.set('sort', sort);
      if (order) params = params.set('order', order);
    }
    return this.http.get<SettingsModel>(`${this.base}/settings`, { params });
  }

  putSettings(body: SettingsModel): Observable<SettingsModel> {
    return this.http.put<SettingsModel>(`${this.base}/settings`, body);
  }
}
