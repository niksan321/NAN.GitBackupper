import type { TargetItemModel, TimePeriodType } from './api-types';

const minOperationTimeoutSec = 15;
const maxOperationTimeoutSec = 86400;

export function clampOperationTimeoutSeconds(sec: number, fallback = 120): number {
  if (!Number.isFinite(sec)) return fallback;
  return Math.min(maxOperationTimeoutSec, Math.max(minOperationTimeoutSec, Math.round(sec)));
}

export function createDefaultTargetItem(): TargetItemModel {
  return {
    id: crypto.randomUUID(),
    provider: 0,
    apiKey: '',
    providerUserName: '',
    gitLabBaseUrl: '',
    backupRootPath: '',
    name: '',
    backupInterval: { value: 24, type: 3 },
    operationTimeoutSeconds: 120,
    enabled: true,
    cleanBackupDirectory: true,
    zipAfterBackup: true,
    zipCompression: 2,
    zipArchivesToKeep: 10,
    keepArchivesUntilDiskLimit: false,
    diskUsageLimitPercent: 80,
    repositoryBackupParallelism: 2,
    repositoryBranches: {},
    useAllRepositories: null,
    repositoryBackupSelectionMode: null,
    backupRepositoryKeys: [],
    repositoryBackupSizesBytes: {},
    showOnlySelectedRepositories: false,
  };
}

export function cloneProfile(p: TargetItemModel): TargetItemModel {
  const c = JSON.parse(JSON.stringify(p)) as TargetItemModel;
  return normalizeProfile(c);
}

/** Ensures nested objects exist after JSON from API. */
/** 0 = UseAll, 1 = OwnRepositoriesOnly, 2 = Manual — mirrors API enum. */
export function getEffectiveRepositorySelectionMode(p: TargetItemModel): number {
  const m = p.repositoryBackupSelectionMode;
  if (m != null && m >= 0 && m <= 2) return m;
  if (p.useAllRepositories === false) return 2;
  return 0;
}

export function normalizeProfile(p: TargetItemModel): TargetItemModel {
  const d = createDefaultTargetItem();
  p.backupInterval = {
    value: p.backupInterval?.value ?? d.backupInterval.value,
    type: (p.backupInterval?.type ?? d.backupInterval.type) as TimePeriodType,
  };

  const anyP = p as TargetItemModel & {
    operationTimeout?: { value?: number; type?: TimePeriodType };
  };
  if (anyP.operationTimeoutSeconds != null && Number.isFinite(anyP.operationTimeoutSeconds)) {
    p.operationTimeoutSeconds = clampOperationTimeoutSeconds(anyP.operationTimeoutSeconds, d.operationTimeoutSeconds);
  } else if (anyP.operationTimeout && typeof anyP.operationTimeout === 'object') {
    const v = anyP.operationTimeout.value ?? 2;
    const t = anyP.operationTimeout.type ?? 2;
    const mult: Record<number, number> = { 0: 0.001, 1: 1, 2: 60, 3: 3600, 4: 86400 };
    const sec = Math.ceil(v * (mult[t] ?? 60));
    p.operationTimeoutSeconds = clampOperationTimeoutSeconds(sec, d.operationTimeoutSeconds);
  } else {
    p.operationTimeoutSeconds = d.operationTimeoutSeconds;
  }
  delete anyP.operationTimeout;

  p.repositoryBranches ??= {};
  p.backupRepositoryKeys ??= [];
  p.repositoryBackupSizesBytes ??= {};
  p.zipArchivesToKeep ??= d.zipArchivesToKeep;
  p.keepArchivesUntilDiskLimit ??= d.keepArchivesUntilDiskLimit;
  p.diskUsageLimitPercent ??= d.diskUsageLimitPercent;
  if (p.diskUsageLimitPercent < 1) p.diskUsageLimitPercent = 1;
  if (p.diskUsageLimitPercent > 99) p.diskUsageLimitPercent = 99;
  p.repositoryBackupParallelism ??= d.repositoryBackupParallelism;
  if (p.repositoryBackupParallelism < 1) p.repositoryBackupParallelism = 1;
  const zc = p.zipCompression;
  if (zc == null || !Number.isFinite(zc)) p.zipCompression = d.zipCompression;
  else p.zipCompression = Math.min(3, Math.max(0, Math.round(zc)));
  p.zipAfterBackup = true;
  return p;
}
