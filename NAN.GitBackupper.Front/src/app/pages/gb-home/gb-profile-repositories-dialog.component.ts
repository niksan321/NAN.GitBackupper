import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import type { GitRepositoryDescriptor, SettingsModel, TargetItemModel } from '../../core/api-types';
import {
  cloneProfile,
  getEffectiveRepositorySelectionMode,
  normalizeProfile,
} from '../../core/profile-defaults';
import { ProfilesApiService } from '../../core/profiles-api.service';
import { SettingsApiService } from '../../core/settings-api.service';
import { AppToastService } from '../../core/app-toast.service';
import { AppI18nService } from '../../i18n/app-i18n.service';
import {
  isRedundantTableSort,
  parseOnSortEvent,
  tableSortToApiParam,
} from '../../core/table-sort-helpers';

interface RepoDialogSnapshot {
  mode: number | null;
  useAll: boolean | null;
  keys: string[];
  branches: Record<string, string>;
  showOnlySelected: boolean;
}

@Component({
  selector: 'gb-profile-repositories-dialog',
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    CardModule,
    CheckboxModule,
    InputTextModule,
    ProgressSpinnerModule,
    SelectModule,
    TableModule,
  ],
  templateUrl: './gb-profile-repositories-dialog.component.html',
  styleUrl: './gb-profile-repositories-dialog.component.scss',
})
export class GbProfileRepositoriesDialogComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly profilesApi = inject(ProfilesApiService);
  private readonly settingsApi = inject(SettingsApiService);
  private readonly toast = inject(AppToastService);
  readonly i18n = inject(AppI18nService);

  settings: SettingsModel | null = null;
  working: TargetItemModel | null = null;
  repos: GitRepositoryDescriptor[] = [];
  listLoading = false;
  /** Фоновая синхронизация списка с Git API после показа кэша. */
  syncInProgress = false;
  saving = false;
  private finished = false;
  private refreshRepositoriesCompleted = false;

  searchText = '';
  appliedSearch = '';
  branchChoices: Record<string, string[]> = {};
  branchLoading: Record<string, boolean> = {};
  private branchLoadStarted = new Set<string>();

  private snapshot: RepoDialogSnapshot | null = null;
  private manualSelectionSnapshot: string[] = [];
  private manualSelectionSnapshotValid = false;
  reposSortField: string | null = null;
  reposSortOrder: 1 | -1 = 1;
  reposTableFirst = 0;
  /** Режим до последнего выбора в UI (для корректного снимка ключей при смене Manual). */
  private modeUiPrev = 0;

  selectionModeOptions(): { label: string; value: number }[] {
    return [
      { label: this.i18n.t('repos.modeUseAll'), value: 0 },
      { label: this.i18n.t('repos.modeOwnOnly'), value: 1 },
      { label: this.i18n.t('repos.modeManual'), value: 2 },
    ];
  }

  ngOnInit(): void {
    const profileId = this.route.snapshot.paramMap.get('id')?.trim();
    if (!profileId) {
      this.router.navigate(['/profiles']);
      return;
    }

    this.settingsApi.getSettings().subscribe({
      next: (settings) => {
        const profile = settings.targetItems?.find((item) => item.id === profileId);
        if (!profile) {
          this.toast.error(`Profile "${profileId}" not found.`);
          this.router.navigate(['/profiles']);
          return;
        }

        this.settings = settings;
        this.working = cloneProfile(profile);
        if (this.working.repositoryBackupSelectionMode == null)
          this.working.repositoryBackupSelectionMode = getEffectiveRepositorySelectionMode(this.working);
        this.takeSnapshot();
        this.modeUiPrev = getEffectiveRepositorySelectionMode(this.working);
        this.manualSelectionSnapshot = [];
        this.manualSelectionSnapshotValid = false;
        this.searchText = '';
        this.appliedSearch = '';
        this.branchChoices = {};
        this.branchLoading = {};
        this.branchLoadStarted.clear();
        this.refreshRepositoriesCompleted = false;
        this.loadRepositories();
      },
      error: (e) => {
        this.toast.error(e);
        this.router.navigate(['/profiles']);
      },
    });
  }

  private takeSnapshot(): void {
    if (!this.working) return;
    this.snapshot = {
      mode: this.working.repositoryBackupSelectionMode,
      useAll: this.working.useAllRepositories,
      keys: [...this.working.backupRepositoryKeys],
      branches: { ...this.working.repositoryBranches },
      showOnlySelected: this.working.showOnlySelectedRepositories,
    };
  }

  private loadRepositories(): void {
    if (!this.working) return;
    this.listLoading = true;
    this.syncInProgress = true;
    const id = this.working.id;
    this.profilesApi
      .listRepositoriesCached(
        id,
        this.reposSortField?.trim() || undefined,
        this.reposSortField?.trim() ? tableSortToApiParam(this.reposSortOrder) : undefined,
      )
      .subscribe({
        next: (list) => {
          this.listLoading = false;
          if (!this.refreshRepositoriesCompleted) this.repos = list ?? [];
        },
        error: (e) => {
          this.listLoading = false;
          this.toast.error(e);
          if (!this.refreshRepositoriesCompleted) this.repos = [];
        },
      });

    this.profilesApi.refreshRepositories(id).subscribe({
      next: (list) => {
        this.refreshRepositoriesCompleted = true;
        this.pruneWorkingToValidRepoKeys();
        this.syncInProgress = false;
        const sortF = this.reposSortField?.trim() || undefined;
        const sortO = sortF ? tableSortToApiParam(this.reposSortOrder) : undefined;
        if (sortF) {
          this.profilesApi.listRepositoriesCached(id, sortF, sortO).subscribe({
            next: (sorted) => {
              this.repos = sorted ?? [];
            },
            error: () => {
              this.repos = list ?? [];
            },
          });
        } else {
          this.repos = list ?? [];
        }
      },
      error: (e) => {
        this.syncInProgress = false;
        if (this.repos.length === 0) this.toast.error(e);
      },
    });
  }

  /** Согласовать выбор репозиториев с актуальным списком (как на бэкенде после refresh). */
  private pruneWorkingToValidRepoKeys(): void {
    const w = this.working;
    if (!w) return;
    const valid = new Set(this.repos.map((r) => r.displayKey));
    w.backupRepositoryKeys = w.backupRepositoryKeys.filter((k) => valid.has(k));
    const branches = { ...w.repositoryBranches };
    for (const k of Object.keys(branches)) {
      if (!valid.has(k)) delete branches[k];
    }
    w.repositoryBranches = branches;
    const sizes = { ...w.repositoryBackupSizesBytes };
    for (const k of Object.keys(sizes)) {
      if (!valid.has(k)) delete sizes[k];
    }
    w.repositoryBackupSizesBytes = sizes;
  }

  effectiveMode(): number {
    if (!this.working) return 0;
    return getEffectiveRepositorySelectionMode(this.working);
  }

  filteredRepos(): GitRepositoryDescriptor[] {
    const w = this.working;
    if (!w) return [];
    const q = this.appliedSearch.trim();
    return this.repos.filter((r) => {
      if (w.showOnlySelectedRepositories && !this.isRowIncluded(r)) return false;
      if (!q) return true;
      const ql = q.toLowerCase();
      return (
        r.displayKey.toLowerCase().includes(ql) || r.httpsCloneUrl.toLowerCase().includes(ql)
      );
    });
  }

  tableRows(): GitRepositoryDescriptor[] {
    return this.filteredRepos();
  }

  onReposTableSort(event: unknown): void {
    const meta = parseOnSortEvent(event);
    if (!meta) return;
    if (isRedundantTableSort(meta, this.reposSortField, this.reposSortOrder)) return;
    this.reposSortField = meta.field;
    this.reposSortOrder = meta.order as 1 | -1;
    this.reposTableFirst = 0;
    this.reloadCachedSorted();
  }

  private reloadCachedSorted(): void {
    if (!this.working) return;
    const id = this.working.id;
    const field = this.reposSortField?.trim() || undefined;
    const order = field ? tableSortToApiParam(this.reposSortOrder) : undefined;
    this.listLoading = true;
    this.profilesApi.listRepositoriesCached(id, field, order).subscribe({
      next: (list) => {
        this.repos = list ?? [];
        this.listLoading = false;
      },
      error: (e) => {
        this.listLoading = false;
        this.toast.error(e);
      },
    });
  }

  isRowIncluded(r: GitRepositoryDescriptor): boolean {
    const w = this.working;
    if (!w) return false;
    const mode = getEffectiveRepositorySelectionMode(w);
    if (mode === 0) return true;
    if (mode === 1) return !!r.isOwnedByAuthenticatedUser;
    return w.backupRepositoryKeys.some((k) => k === r.displayKey);
  }

  rowCheckboxEnabled(r: GitRepositoryDescriptor): boolean {
    void r;
    return this.effectiveMode() === 2;
  }

  setRowIncluded(r: GitRepositoryDescriptor, checked: boolean): void {
    const w = this.working;
    if (!w || getEffectiveRepositorySelectionMode(w) !== 2) return;
    const key = r.displayKey;
    const list = w.backupRepositoryKeys;
    const has = list.some((k) => k === key);
    if (checked && !has) list.push(key);
    else if (!checked && has) {
      w.backupRepositoryKeys = list.filter((k) => k !== key);
    }
  }

  get selectAllCheckboxEnabled(): boolean {
    return this.effectiveMode() === 2 && this.repos.length > 0;
  }

  selectAllVisibleState(): boolean | null {
    const w = this.working;
    if (!w || getEffectiveRepositorySelectionMode(w) !== 2) return true;
    const visible = this.filteredRepos();
    const n = visible.length;
    if (n === 0) return false;
    const selected = visible.filter((row) =>
      w.backupRepositoryKeys.some((k) => k === row.displayKey),
    ).length;
    if (selected === 0) return false;
    if (selected === n) return true;
    return null;
  }

  onSelectAllHeaderClick(): void {
    const w = this.working;
    if (!w || !this.selectAllCheckboxEnabled) return;
    const visible = this.filteredRepos();
    if (visible.length === 0) return;
    const state = this.selectAllVisibleState();
    const selectAll = state !== true;
    if (selectAll) {
      for (const row of visible) {
        const key = row.displayKey;
        if (!w.backupRepositoryKeys.some((k) => k === key)) w.backupRepositoryKeys.push(key);
      }
    } else {
      for (const row of visible) {
        const key = row.displayKey;
        w.backupRepositoryKeys = w.backupRepositoryKeys.filter((k) => k !== key);
      }
    }
  }

  onSelectionModeChange(value: number): void {
    const w = this.working;
    if (!w) return;
    const prev = this.modeUiPrev;
    if (prev === 2) {
      this.manualSelectionSnapshot = [...w.backupRepositoryKeys];
      this.manualSelectionSnapshotValid = true;
    }
    w.repositoryBackupSelectionMode = value;
    this.modeUiPrev = value;
    if (value === 2) {
      if (this.manualSelectionSnapshotValid) {
        this.applyManualKeysFiltered(this.manualSelectionSnapshot);
      } else if (w.backupRepositoryKeys.length > 0) {
        this.applyManualKeysFiltered(w.backupRepositoryKeys);
      } else {
        for (const row of this.repos) {
          const key = row.displayKey;
          if (!w.backupRepositoryKeys.some((k) => k === key)) w.backupRepositoryKeys.push(key);
        }
      }
    }
  }

  private applyManualKeysFiltered(keys: readonly string[]): void {
    const w = this.working;
    if (!w) return;
    let restored = [...keys];
    if (this.repos.length > 0) {
      const valid = new Set(this.repos.map((r) => r.displayKey));
      restored = restored.filter((k) => valid.has(k));
    }
    w.backupRepositoryKeys = restored;
  }

  applySearch(): void {
    this.appliedSearch = this.searchText;
  }

  clearSearch(): void {
    this.searchText = '';
    this.appliedSearch = '';
  }

  get hasSearchText(): boolean {
    return this.searchText.trim().length > 0;
  }

  getBranchOptions(r: GitRepositoryDescriptor): string[] {
    const cached = this.branchChoices[r.displayKey];
    if (cached?.length) return cached;
    const b = this.getBranchValue(r);
    return b ? [b] : [];
  }

  getBranchValue(r: GitRepositoryDescriptor): string {
    const w = this.working;
    if (!w) return '';
    const saved = w.repositoryBranches[r.displayKey];
    if (saved != null && saved.trim()) return saved.trim();
    return (r.defaultBranch ?? '').trim();
  }

  setBranchValue(r: GitRepositoryDescriptor, value: string | null): void {
    const w = this.working;
    if (!w) return;
    const v = (value ?? '').trim();
    if (!v) delete w.repositoryBranches[r.displayKey];
    else w.repositoryBranches[r.displayKey] = v;
  }

  onBranchPanelShow(r: GitRepositoryDescriptor): void {
    const key = r.displayKey;
    if (this.branchLoadStarted.has(key)) return;
    this.branchLoadStarted.add(key);
    const w = this.working;
    if (!w) return;
    this.branchLoading[key] = true;
    this.profilesApi.listBranches(w.id, r).subscribe({
      next: (list) => {
        let names = [...list].sort((a, b) => a.localeCompare(b));
        const cur = this.getBranchValue(r);
        if (cur && !names.includes(cur)) names.unshift(cur);
        if (names.length === 0 && cur) names = [cur];
        else if (names.length > 0 && !this.getBranchValue(r)) {
          this.setBranchValue(r, names[0]);
        }
        this.branchChoices[key] = names;
        this.branchLoading[key] = false;
      },
      error: (e) => {
        this.branchLoading[key] = false;
        this.branchLoadStarted.delete(key);
        this.toast.error(e);
      },
    });
  }

  sizeDisplay(r: GitRepositoryDescriptor): string {
    const w = this.working;
    if (!w) return this.i18n.t('repos.sizePending');
    const bytes = w.repositoryBackupSizesBytes[r.displayKey];
    if (bytes == null || !Number.isFinite(bytes)) return this.i18n.t('repos.sizePending');
    return this.formatBytes(bytes);
  }

  private formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    const kb = bytes / 1024;
    if (kb < 1024) return `${kb >= 10 ? kb.toFixed(0) : kb.toFixed(1)} KB`;
    const mb = kb / 1024;
    if (mb < 1024) return `${mb >= 10 ? mb.toFixed(0) : mb.toFixed(1)} MB`;
    const gb = mb / 1024;
    return `${gb >= 10 ? gb.toFixed(0) : gb.toFixed(1)} GB`;
  }

  back(): void {
    this.cancel();
  }

  cancel(): void {
    if (this.finished) return;
    this.finished = true;
    const w = this.working;
    const s = this.snapshot;
    if (w && s) {
      w.repositoryBackupSelectionMode = s.mode;
      w.useAllRepositories = s.useAll;
      w.backupRepositoryKeys = [...s.keys];
      w.repositoryBranches = { ...s.branches };
      w.showOnlySelectedRepositories = s.showOnlySelected;
    }
    this.router.navigate(['/profiles']);
  }

  confirm(): void {
    const w = this.working;
    if (!w || !this.settings?.targetItems) return;
    this.saving = true;
    const rawItems = [...this.settings.targetItems];
    const idx = rawItems.findIndex((p) => p.id === w.id);
    if (idx < 0) {
      this.saving = false;
      return;
    }
    rawItems[idx] = normalizeProfile(JSON.parse(JSON.stringify(w)) as TargetItemModel);
    const items = rawItems.map((p) =>
      normalizeProfile(JSON.parse(JSON.stringify(p)) as TargetItemModel),
    );
    const payload: SettingsModel = { ...this.settings, targetItems: items };
    this.settingsApi.putSettings(payload).subscribe({
      next: (s) => {
        this.saving = false;
        this.finished = true;
        this.settings = s;
        this.router.navigate(['/profiles']);
      },
      error: (e) => {
        this.saving = false;
        this.toast.error(e);
      },
    });
  }

}
