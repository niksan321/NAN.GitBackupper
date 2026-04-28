import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environment';
import type { SchedulerStatus } from './api-types';

@Injectable({ providedIn: 'root' })
export class SchedulerApiService {
  private readonly base = `${environment.apiUrl}/api/scheduler`;

  constructor(private readonly http: HttpClient) {}

  status(): Observable<SchedulerStatus> {
    return this.http.get<SchedulerStatus>(`${this.base}/status`);
  }

  start(): Observable<SchedulerStatus> {
    return this.http.post<SchedulerStatus>(`${this.base}/start`, {});
  }

  stop(): Observable<SchedulerStatus> {
    return this.http.post<SchedulerStatus>(`${this.base}/stop`, {});
  }
}
