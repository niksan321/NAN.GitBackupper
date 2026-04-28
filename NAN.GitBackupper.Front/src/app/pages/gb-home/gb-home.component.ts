import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ProgressBarModule } from 'primeng/progressbar';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService } from 'primeng/api';
import { finalize } from 'rxjs/operators';
import type {
  GitProviderType,
  SettingsModel,
  TargetItemModel,
} from '../../core/api-types';
import { ProfileBackupHubService, type ProfileBackupRealtime } from '../../core/profile-backup-hub.service';
import { AppI18nService } from '../../i18n/app-i18n.service';
import { normalizeProfile } from '../../core/profile-defaults';
import { AppToastService } from '../../core/app-toast.service';
import { ProfilesApiService } from '../../core/profiles-api.service';
import { SettingsApiService } from '../../core/settings-api.service';
import { formatBytes } from '../../core/format-bytes';
import {
  isRedundantTableSort,
  parseOnSortEvent,
  tableSortToApiParam,
} from '../../core/table-sort-helpers';

@Component({
    selector: 'gb-home',
    imports: [
        CommonModule,
        ButtonModule,
        TooltipModule,
        CardModule,
        TableModule,
        ProgressBarModule,
    ],
    templateUrl: './gb-home.component.html',
    styleUrl: './gb-home.component.scss'
})
export class GbHomeComponent implements OnInit {
  private readonly settingsApi = inject(SettingsApiService);
  private readonly profilesApi = inject(ProfilesApiService);
  private readonly toast = inject(AppToastService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly router = inject(Router);
  readonly backupHub = inject(ProfileBackupHubService);
  readonly i18n = inject(AppI18nService);

  /** Для вызова refresh() когда SignalR сообщает об окончании бэкапа (isRunning: true → false). */
  private readonly backupWasRunningByProfileId = new Map<string, boolean>();

  constructor() {
    effect(() => {
      const states = this.backupHub.states();
      for (const id of Object.keys(states)) {
        const running = states[id]?.isRunning ?? false;
        const wasRunning = this.backupWasRunningByProfileId.get(id) ?? false;
        if (wasRunning && !running) {
          queueMicrotask(() => this.refresh());
        }
        this.backupWasRunningByProfileId.set(id, running);
      }
    });
  }

  settings: SettingsModel | null = null;
  /** Загрузка настроек/списка профилей (getSettings), в т.ч. по кнопке «Обновить». */
  profilesLoading = false;
  private settingsRefreshInflight = 0;
  /** Сортировка с сервера: поле совпадает с query sort, order — PrimeNG 1 / -1. */
  profilesSortField: string | null = null;
  profilesSortOrder: 1 | -1 = 1;
  profilesTableFirst = 0;
  /** Профили, для которых сейчас выполняется запрос «бекап сейчас» (несколько могут идти параллельно). */
  private readonly backingUpProfileIds = signal(new Set<string>());

  isProfileBackingUp(id: string): boolean {
    return this.backingUpProfileIds().has(id);
  }

  /** Кнопка «Отмена»: HTTP ещё не завершился или сервер сообщает, что бекап идёт. */
  showCancelBackup(profileId: string): boolean {
    return (
      this.isProfileBackingUp(profileId) || !!this.backupLine(profileId)?.isRunning
    );
  }

  backupLine(profileId: string): ProfileBackupRealtime | undefined {
    return this.backupHub.states()[profileId];
  }

  backupStatusLineVisible(profileId: string): boolean {
    const s = this.backupLine(profileId);
    if (!s) return false;
    if (s.isRunning) return true;
    if (s.backupProgressLogDismissed) return false;
    return !!(s.lastMessage && s.lastMessage.trim());
  }

  showDismissBackupLogButton(profileId: string): boolean {
    const s = this.backupLine(profileId);
    if (!s || s.isRunning) return false;
    return !!(s.lastMessage?.trim());
  }

  dismissBackupLogRow(profileId: string): void {
    this.profilesApi.dismissBackupProgressLog(profileId).subscribe({
      next: () => {},
      error: (e) => this.toast.error(e),
    });
  }

  backupProgressPercent(profileId: string): number {
    const s = this.backupLine(profileId);
    const max = s?.progressMaximum ?? 1;
    const v = s?.progressValue ?? 0;
    if (max <= 0) return 0;
    return Math.min(100, Math.round((100 * v) / max));
  }

  readonly providerSelectOptions = computed(() => {
    this.i18n.lang();
    const t = (k: string) => this.i18n.t(k);
    return [
      { label: t('provider.github'), value: 0 },
      { label: t('provider.gitlab'), value: 1 },
      { label: t('provider.bitbucket'), value: 2 },
      { label: t('provider.gitlabLocal'), value: 3 },
    ];
  });

  /** Только Bitbucket использует логин для Basic auth (см. EditTargetViewModel, BitbucketProviderClient). */
  providerNeedsUsername(provider: GitProviderType): boolean {
    return provider === 2;
  }

  /** Базовый URL — только для GitLab Local (self-hosted). */
  providerNeedsGitLabBaseUrl(provider: GitProviderType): boolean {
    return provider === 3;
  }

  providerLabel(provider: GitProviderType | null | undefined): string {
    if (provider == null) return '—';
    const opt = this.providerSelectOptions().find((o) => o.value === provider);
    return opt?.label ?? String(provider);
  }

  formatBackupInterval(row: TargetItemModel): string {
    const tp = row.backupInterval;
    if (tp == null || tp.value == null) return '—';
    const periodOpt = this.periodSelectOptions().find((o) => o.value === tp.type);
    const unit = periodOpt?.label ?? '';
    return `${tp.value}\u00a0${unit}`.trim();
  }

  formatLastBackupSizes(row: TargetItemModel): string {
    const total = row.lastBackupSelectedTotalBytes;
    const zip = row.lastBackupZipArchiveBytes;
    const em = '\u2014';
    const left =
      total != null && Number.isFinite(total) ? this.formatBytes(total) : em;
    const right =
      zip != null && Number.isFinite(zip) ? this.formatBytes(zip) : em;
    return `${left}/${right}`;
  }

  formatLastBackupDuration(row: TargetItemModel): string {
    const ms = row.lastBackupDurationMs;
    if (ms == null || !Number.isFinite(ms) || ms < 0) return '\u2014';
    const s = Math.floor(ms / 1000);
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    const sec = s % 60;
    if (h > 0)
      return `${h}:${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`;
    return `${m}:${String(sec).padStart(2, '0')}`;
  }

  readonly periodSelectOptions = computed(() => {
    this.i18n.lang();
    const t = (k: string) => this.i18n.t(k);
    return [
      { label: t('period.ms'), value: 0 },
      { label: t('period.sec'), value: 1 },
      { label: t('period.min'), value: 2 },
      { label: t('period.hours'), value: 3 },
      { label: t('period.days'), value: 4 },
    ];
  });

  /** Макс. параллелизм: логические ядра × 2 (как на сервере бэкапа). */
  readonly maxRepoParallelism = computed(() => {
    const cores =
      typeof navigator !== 'undefined' && navigator.hardwareConcurrency
        ? navigator.hardwareConcurrency
        : 4;
    return Math.max(2, cores * 2);
  });

  readonly zipCompressionOptions = computed(() => {
    this.i18n.lang();
    const t = (k: string) => this.i18n.t(k);
    return [
      { label: t('profiles.zipCompression.none'), value: 0 },
      { label: t('profiles.zipCompression.fastest'), value: 1 },
      { label: t('profiles.zipCompression.optimal'), value: 2 },
      { label: t('profiles.zipCompression.smallest'), value: 3 },
    ];
  });

  async ngOnInit(): Promise<void> {
    try {
      await this.backupHub.ensureConnected();
    } catch {
      // без SignalR остаётся опрос GET …/status после загрузки списка
    }
    this.refresh();
  }

  refresh(): void {
    this.settingsRefreshInflight += 1;
    this.profilesLoading = true;
    const sort = this.profilesSortField?.trim() || undefined;
    const order = sort ? tableSortToApiParam(this.profilesSortOrder) : undefined;
    this.settingsApi
      .getSettings(sort, order)
      .pipe(
        finalize(() => {
          this.settingsRefreshInflight -= 1;
          if (this.settingsRefreshInflight <= 0) {
            this.settingsRefreshInflight = 0;
            this.profilesLoading = false;
          }
        }),
      )
      .subscribe({
        next: (s) => {
          this.settings = s;
          s.targetItems?.forEach((p) => normalizeProfile(p));
          const map = s.profileBackupStatuses;
          if (map)
            for (const id of Object.keys(map)) {
              const st = map[id];
              if (st) this.backupHub.applyHttpSnapshot(id, st);
            }
        },
        error: (e) => this.toast.error(e),
      });
  }

  onTargetItemsSort(event: unknown): void {
    const meta = parseOnSortEvent(event);
    if (!meta) return;
    if (isRedundantTableSort(meta, this.profilesSortField, this.profilesSortOrder)) return;
    this.profilesSortField = meta.field;
    this.profilesSortOrder = meta.order as 1 | -1;
    this.profilesTableFirst = 0;
    this.refresh();
  }

  runBackup(row: TargetItemModel): void {
    const id = row.id;
    this.backingUpProfileIds.update((s) => new Set(s).add(id));
    this.profilesApi.runBackup(id).subscribe({
      next: () => {
        this.removeBackingUp(id);
      },
      error: (e) => {
        this.removeBackingUp(id);
        this.toast.error(e);
      },
    });
  }

  requestCancelBackup(row: TargetItemModel): void {
    const name = (row.name ?? '').trim() || row.id;
    this.confirmation.confirm({
      header: this.i18n.t('profiles.cancelBackupHeader'),
      message: this.i18n.t('profiles.cancelBackupMessage').replace('{name}', name),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.i18n.t('profiles.cancelBackupConfirm'),
      rejectLabel: this.i18n.t('profiles.cancelBackupReject'),
      acceptButtonProps: { severity: 'danger' },
      accept: () => this.executeCancelBackup(row),
    });
  }

  private executeCancelBackup(row: TargetItemModel): void {
    const id = row.id;
    this.profilesApi.cancelBackup(id).subscribe({
      next: () => this.removeBackingUp(id),
      error: (e) => this.toast.error(e),
    });
  }

  private removeBackingUp(id: string): void {
    this.backingUpProfileIds.update((s) => {
      const next = new Set(s);
      next.delete(id);
      return next;
    });
  }

  openAddProfile(): void {
    this.router.navigate(['/profiles/new']);
  }

  openEditProfile(row: TargetItemModel): void {
    this.router.navigate(['/profiles', row.id, 'edit']);
  }

  openRepositories(row: TargetItemModel): void {
    this.router.navigate(['/profiles', row.id, 'repositories']);
  }

  private formatBytes(bytes: number): string {
    return formatBytes(bytes);
  }
}
