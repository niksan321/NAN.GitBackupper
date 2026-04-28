/** Mirrors NAN.GitBackupper.Core.Enums.GitProviderType */
export type GitProviderType = 0 | 1 | 2 | 3;

/** Mirrors NAN.GitBackupper.Core.Enums.TimePeriodType */
export type TimePeriodType = 0 | 1 | 2 | 3 | 4;

/** Mirrors NAN.GitBackupper.Api.Models.Enums.SchedulerStartupPreference */
export type SchedulerStartupPreference = 0 | 1 | 2;

export interface TimePeriod {
  value: number;
  type: TimePeriodType;
}

/** Mirrors NAN.Git.Models.GitRepositoryDescriptor */
export interface GitRepositoryDescriptor {
  displayKey: string;
  httpsCloneUrl: string;
  defaultBranch?: string | null;
  isOwnedByAuthenticatedUser?: boolean;
}

export interface TargetItemModel {
  id: string;
  provider: GitProviderType;
  apiKey: string;
  providerUserName: string;
  gitLabBaseUrl: string;
  backupRootPath: string;
  name: string;
  backupInterval: TimePeriod;
  /** Таймаут HTTP (секунды), 15…86400. */
  operationTimeoutSeconds: number;
  enabled: boolean;
  cleanBackupDirectory: boolean;
  zipAfterBackup: boolean;
  zipCompression: number;
  zipArchivesToKeep: number;
  keepArchivesUntilDiskLimit: boolean;
  diskUsageLimitPercent: number;
  repositoryBackupParallelism: number;
  repositoryBranches: Record<string, string>;
  useAllRepositories: boolean | null;
  repositoryBackupSelectionMode: number | null;
  backupRepositoryKeys: string[];
  repositoryBackupSizesBytes: Record<string, number>;
  /** Сумма размеров выбранных репозиториев после последнего успешного бэкапа (байты). */
  lastBackupSelectedTotalBytes?: number | null;
  /** Размер ZIP последнего успешного бэкапа (байты), если ZIP создавался. */
  lastBackupZipArchiveBytes?: number | null;
  /** Длительность последнего бэкапа (мс). */
  lastBackupDurationMs?: number | null;
  showOnlySelectedRepositories: boolean;
}

export interface SettingsModel {
  targetItems?: TargetItemModel[];
  /** Снимки статуса бекапа по id профиля (только ответ GET /api/settings). */
  profileBackupStatuses?: Record<string, ProfileBackupStatus>;
  runOnStartUp?: boolean;
  autoStartPing?: boolean;
  notify?: boolean;
  startMinimized?: boolean;
  showDeletePrompt?: boolean;
  playErrorSound?: boolean;
  language?: string;
  timeToShowPopupSec?: number;
  sortingColumn?: number;
  sortingDirection?: number;
  /** Автозапуск планировщика при старте API: 0 — всегда, 1 — никогда, 2 — последнее состояние. */
  schedulerStartupPreference?: SchedulerStartupPreference;
}

export interface SchedulerStatus {
  isRunning: boolean;
}

/** Ответ GET …/backup-archives */
export interface BackupArchiveFileInfo {
  fileName: string;
  sizeBytes: number;
  /** ISO 8601 от сервера */
  modifiedUtc: string;
  /** Длительность полного бэкапа, создавшего этот ZIP (мс), если рядом есть .gbmeta.json */
  durationMs?: number | null;
}

export interface ProfileBackupStatus {
  isRunning: boolean;
  lastMessage: string;
  lastUpdatedUtc: string;
  /** Счётчик шага (сервер, GitBackupService count progress). */
  progressValue?: number;
  /** Верхняя граница шагов; минимум 1. */
  progressMaximum?: number;
  /** Совпадает с полем в SQLite; строка лога скрыта пользователем. */
  backupProgressLogDismissed?: boolean;
}

export interface ProfileDiskUsageInfo {
  totalBytes: number;
  freeBytes: number;
  usedBytes: number;
  diskUsagePercent: number;
}
