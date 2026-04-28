import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import type { BackupArchiveFileInfo, ProfileDiskUsageInfo, SettingsModel, TargetItemModel } from '../../core/api-types';
import { formatBytes } from '../../core/format-bytes';
import { AppToastService } from '../../core/app-toast.service';
import { ProfilesApiService } from '../../core/profiles-api.service';
import { SettingsApiService } from '../../core/settings-api.service';
import { AppI18nService } from '../../i18n/app-i18n.service';
import {
  isRedundantTableSort,
  parseOnSortEvent,
  tableSortToApiParam,
} from '../../core/table-sort-helpers';

interface ProfileSelectOption {
  label: string;
  value: string;
}

const storageBackupsProfile = 'gb-backups-profile';

@Component({
  selector: 'gb-backups',
  imports: [CommonModule, FormsModule, CardModule, TableModule, SelectModule, ButtonModule, TooltipModule],
  templateUrl: './gb-backups.component.html',
  styleUrl: './gb-backups.component.scss',
})
export class GbBackupsComponent implements OnInit {
  private readonly settingsApi = inject(SettingsApiService);
  private readonly profilesApi = inject(ProfilesApiService);
  private readonly toast = inject(AppToastService);
  readonly i18n = inject(AppI18nService);

  settings: SettingsModel | null = null;
  profileOptions: ProfileSelectOption[] = [];
  selectedProfileId: string | null = null;

  archives: BackupArchiveFileInfo[] = [];
  archivesLoading = false;
  downloadingFileName: string | null = null;
  listError: string | null = null;
  diskUsageInfo: ProfileDiskUsageInfo | null = null;
  diskUsageLoading = false;
  /** Совпадает с дефолтом API: modifiedUtc по убыванию. */
  archivesSortField: string | null = 'modifiedUtc';
  archivesSortOrder: 1 | -1 = -1;
  archivesTableFirst = 0;

  ngOnInit(): void {
    this.settingsApi.getSettings().subscribe({
      next: (s) => {
        this.settings = s;
        this.profileOptions = (s.targetItems ?? []).map((p) => ({ label: p.name?.trim() || p.id, value: p.id }));
        const targetIds = new Set((s.targetItems ?? []).map((p) => p.id));
        const saved = this.readStoredBackupsProfileId();
        let initial: string | null = null;
        if (saved && targetIds.has(saved)) {
          initial = saved;
        } else if (saved) {
          this.writeStoredBackupsProfileId(null);
        }
        this.selectedProfileId = initial;
        if (initial) {
          this.loadArchives(initial);
          this.loadDiskUsage(initial);
        }
      },
      error: (e) => this.toast.error(e),
    });
  }

  onProfileChange(value: string | null): void {
    this.writeStoredBackupsProfileId(value);
    this.selectedProfileId = value;
    this.archives = [];
    this.diskUsageInfo = null;
    this.listError = null;
    if (!value) return;
    this.loadArchives(value);
    this.loadDiskUsage(value);
  }

  private loadArchives(profileId: string): void {
    this.archivesLoading = true;
    const field = this.archivesSortField?.trim();
    const order = field ? tableSortToApiParam(this.archivesSortOrder) : undefined;
    this.profilesApi.listBackupArchives(profileId, field || undefined, order).subscribe({
      next: (list) => {
        this.archives = list ?? [];
        this.archivesLoading = false;
      },
      error: (e) => {
        this.archivesLoading = false;
        this.toast.error(e);
      },
    });
  }

  private loadDiskUsage(profileId: string): void {
    this.diskUsageLoading = true;
    this.profilesApi.getDiskUsage(profileId).subscribe({
      next: (v) => {
        this.diskUsageInfo = v;
        this.diskUsageLoading = false;
      },
      error: () => {
        this.diskUsageInfo = null;
        this.diskUsageLoading = false;
      },
    });
  }

  onArchivesSort(event: unknown): void {
    const meta = parseOnSortEvent(event);
    if (!meta) return;
    if (isRedundantTableSort(meta, this.archivesSortField, this.archivesSortOrder)) return;
    this.archivesSortField = meta.field;
    this.archivesSortOrder = meta.order as 1 | -1;
    this.archivesTableFirst = 0;
    if (this.selectedProfileId) this.loadArchives(this.selectedProfileId);
  }

  formatSize(bytes: number): string {
    return formatBytes(bytes);
  }

  formatArchiveDate(iso: string): string {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '\u2014';
    return new Intl.DateTimeFormat(this.i18n.lang() === 'ru' ? 'ru-RU' : 'en-GB', {
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(d);
  }

  formatDurationMs(ms: number | null | undefined): string {
    if (ms == null || !Number.isFinite(ms) || ms < 0) return '\u2014';
    const s = Math.floor(ms / 1000);
    const m = Math.floor(s / 60);
    const h = Math.floor(m / 60);
    if (h > 0) return `${h}:${String(m % 60).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
    if (m > 0) return `${m}:${String(s % 60).padStart(2, '0')}`;
    return s === 0 && ms < 1000 ? '<1s' : `${s}s`;
  }

  download(row: BackupArchiveFileInfo): void {
    if (!this.selectedProfileId) return;
    this.downloadingFileName = row.fileName;
    this.profilesApi.getBackupArchiveBlob(this.selectedProfileId, row.fileName).subscribe({
      next: (blob) => {
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = row.fileName;
        a.click();
        URL.revokeObjectURL(a.href);
        this.downloadingFileName = null;
      },
      error: (e) => {
        this.downloadingFileName = null;
        this.toast.error(e);
      },
    });
  }

  hasProfiles(): boolean {
    return (this.settings?.targetItems?.length ?? 0) > 0;
  }

  selectedProfile(): TargetItemModel | null {
    if (!this.selectedProfileId) return null;
    return (this.settings?.targetItems ?? []).find((p) => p.id === this.selectedProfileId) ?? null;
  }

  retentionSummaryText(): string {
    const profile = this.selectedProfile();
    if (!profile) return '';
    if (!profile.keepArchivesUntilDiskLimit)
      return this.i18n.t('backups.retention.fixed').replace('{count}', String(Math.max(1, profile.zipArchivesToKeep)));

    const limitText = this.i18n.t('backups.retention.useAllSpace');
    const allocated = this.allocatedBackupBytes();
    const allocatedText = allocated == null
      ? this.i18n.t('backups.retention.allocatedUnknown')
      : this.i18n.t('backups.retention.allocated').replace('{size}', this.formatSize(allocated));

    const estimateText = this.estimatedBackupsText(profile, allocated);
    return `${limitText} ${allocatedText} ${estimateText}`.trim();
  }

  private allocatedBackupBytes(): number | null {
    const free = this.diskUsageInfo?.freeBytes;
    if (!free || !Number.isFinite(free) || free <= 0) return null;
    return Math.floor(free);
  }

  private estimatedBackupsText(profile: TargetItemModel, allocatedBytes: number | null): string {
    if (allocatedBytes == null) return this.i18n.t('backups.retention.estimateUnknownDisk');
    const lastZip = profile.lastBackupZipArchiveBytes;
    if (!lastZip || !Number.isFinite(lastZip) || lastZip <= 0)
      return this.i18n.t('backups.retention.estimateUnknownZip');

    const estimated = Math.max(1, Math.floor(allocatedBytes / lastZip));
    return this.i18n.t('backups.retention.estimate').replace('{count}', String(estimated));
  }

  private readStoredBackupsProfileId(): string | null {
    try {
      const v = localStorage.getItem(storageBackupsProfile);
      if (v == null || v === '') return null;
      return v;
    } catch {
      return null;
    }
  }

  private writeStoredBackupsProfileId(id: string | null): void {
    try {
      if (id == null || id === '') {
        localStorage.removeItem(storageBackupsProfile);
      } else {
        localStorage.setItem(storageBackupsProfile, id);
      }
    } catch {
      // ignore
    }
  }
}
