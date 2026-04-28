import { Injectable, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
} from '@microsoft/signalr';
import { environment } from '../environment';
import type { ProfileBackupStatus } from './api-types';

/** Снимок с сервера (SignalR + при необходимости GET /status). */
export interface ProfileBackupRealtime extends ProfileBackupStatus {
  profileId: string;
}

@Injectable({ providedIn: 'root' })
export class ProfileBackupHubService {
  private readonly connection: HubConnection;
  readonly states = signal<Record<string, ProfileBackupRealtime>>({});
  constructor() {
    const base = (environment.apiUrl || (typeof location !== 'undefined' ? location.origin : '')).replace(
      /\/$/,
      '',
    );
    const hubUrl = `${base}/hubs/backup`;
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        withCredentials: false,
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .build();

    this.connection.on('backupProgress', (payload: ProfileBackupRealtime & { message?: string }) => {
      const id = String(payload.profileId);
      const lastMessage = payload.lastMessage ?? payload.message ?? '';
      const normalized: ProfileBackupRealtime = {
        ...payload,
        profileId: id,
        lastMessage,
        backupProgressLogDismissed: payload.backupProgressLogDismissed ?? false,
      };
      this.states.update((m) => ({
        ...m,
        [id]: normalized,
      }));
    });
  }

  /** Идempotent: безопасно вызывать из страницы профилей. */
  async ensureConnected(): Promise<void> {
    if (this.connection.state === HubConnectionState.Connected) return;
    await this.connection.start();
  }

  /** Подтянуть состояние через REST (например после загрузки списка или при обрыве SignalR). */
  applyHttpSnapshot(profileId: string, s: ProfileBackupStatus): void {
    const id = String(profileId);
    this.states.update((m) => ({
      ...m,
      [id]: {
        profileId: id,
        isRunning: s.isRunning,
        lastMessage: s.lastMessage ?? '',
        lastUpdatedUtc: s.lastUpdatedUtc,
        progressValue: s.progressValue ?? 0,
        progressMaximum: s.progressMaximum ?? 1,
        backupProgressLogDismissed: s.backupProgressLogDismissed ?? false,
      },
    }));
  }
}
