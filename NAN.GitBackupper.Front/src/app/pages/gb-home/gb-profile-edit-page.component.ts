import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SliderModule } from 'primeng/slider';
import { DatePicker } from 'primeng/datepicker';
import type {
  GitProviderType,
  SettingsModel,
  TargetItemModel,
} from '../../core/api-types';
import { AppToastService } from '../../core/app-toast.service';
import {
  cloneProfile,
  createDefaultTargetItem,
  normalizeProfile,
} from '../../core/profile-defaults';
import { SettingsApiService } from '../../core/settings-api.service';
import { AppI18nService } from '../../i18n/app-i18n.service';

interface ApiKeyHelpStep {
  headingKey: string;
  bodyKey: string;
  url?: string;
  linkLabelKey?: string;
}

@Component({
  selector: 'gb-profile-edit-page',
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    CardModule,
    CheckboxModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    SliderModule,
    DialogModule,
    DatePicker,
  ],
  templateUrl: './gb-profile-edit-page.component.html',
  styleUrl: './gb-profile-edit-page.component.scss',
})
export class GbProfileEditPageComponent implements OnInit {
  private readonly settingsApi = inject(SettingsApiService);
  private readonly toast = inject(AppToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly i18n = inject(AppI18nService);

  settings: SettingsModel | null = null;
  draftProfile: TargetItemModel | null = null;
  isNew = false;
  loading = false;
  savingProfile = false;
  formError: string | null = null;
  apiKeyHelpDialogVisible = false;
  operationTimeoutPickDate: Date = new Date(1970, 0, 1, 0, 0, 0);

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
    this.loading = true;
    const id = this.route.snapshot.paramMap.get('id');
    this.isNew = !id;
    this.settingsApi.getSettings().subscribe({
      next: (settings) => {
        this.settings = settings;
        settings.targetItems?.forEach((p) => normalizeProfile(p));
        if (this.isNew) {
          this.draftProfile = normalizeProfile(createDefaultTargetItem());
        } else {
          const profile = (settings.targetItems ?? []).find((p) => p.id === id);
          if (!profile) {
            this.toast.error('Profile not found.');
            this.back();
            return;
          }
          this.draftProfile = cloneProfile(profile);
        }
        this.formError = null;
        this.clampDraftParallelism();
        this.refreshOperationTimeoutPickDate();
        this.loading = false;
      },
      error: (e) => {
        this.loading = false;
        this.toast.error(e);
      },
    });
  }

  providerNeedsUsername(provider: GitProviderType): boolean {
    return provider === 2;
  }

  providerNeedsGitLabBaseUrl(provider: GitProviderType): boolean {
    return provider === 3;
  }

  apiKeyHelpSteps(provider: GitProviderType): ApiKeyHelpStep[] {
    if (provider === 2) {
      return [
        {
          headingKey: 'profiles.apiKeyHelp.bitbucket.atlassianHeading',
          bodyKey: 'profiles.apiKeyHelp.bitbucket.atlassianBody',
          url: 'https://id.atlassian.com/manage-profile/security/api-tokens',
          linkLabelKey: 'profiles.apiKeyHelp.bitbucket.atlassianLink',
        },
        {
          headingKey: 'profiles.apiKeyHelp.bitbucket.appPasswordHeading',
          bodyKey: 'profiles.apiKeyHelp.bitbucket.appPasswordBody',
          url: 'https://bitbucket.org/account/settings/app-passwords/',
          linkLabelKey: 'profiles.apiKeyHelp.bitbucket.appPasswordLink',
        },
      ];
    }
    if (provider === 1 || provider === 3) {
      return [
        {
          headingKey: 'profiles.apiKeyHelp.gitlab.cloudHeading',
          bodyKey: 'profiles.apiKeyHelp.gitlab.cloudBody',
          url: 'https://gitlab.com/-/user_settings/personal_access_tokens/legacy/new',
          linkLabelKey: 'profiles.apiKeyHelp.gitlab.cloudLink',
        },
        {
          headingKey: 'profiles.apiKeyHelp.gitlab.selfHostedHeading',
          bodyKey: 'profiles.apiKeyHelp.gitlab.selfHostedBody',
        },
      ];
    }
    return [
      {
        headingKey: 'profiles.apiKeyHelp.github.classicHeading',
        bodyKey: 'profiles.apiKeyHelp.github.classicBody',
        url: 'https://github.com/settings/tokens/new',
        linkLabelKey: 'profiles.apiKeyHelp.github.classicLink',
      },
      {
        headingKey: 'profiles.apiKeyHelp.github.fineHeading',
        bodyKey: 'profiles.apiKeyHelp.github.fineBody',
        url: 'https://github.com/settings/personal-access-tokens/new',
        linkLabelKey: 'profiles.apiKeyHelp.github.fineLink',
      },
    ];
  }

  onOperationTimeoutPickChange(d: Date | null): void {
    if (!this.draftProfile || !d) return;
    const sec = d.getHours() * 3600 + d.getMinutes() * 60 + d.getSeconds();
    this.draftProfile.operationTimeoutSeconds = Math.min(86400, sec);
  }

  back(): void {
    if (this.savingProfile) return;
    this.router.navigate(['/profiles']);
  }

  saveProfile(): void {
    if (!this.settings || !this.draftProfile) return;
    const name = (this.draftProfile.name ?? '').trim();
    if (!name) {
      this.formError = this.i18n.t('profiles.validationName');
      return;
    }

    const nameKey = name.toLowerCase();
    const duplicate = (this.settings.targetItems ?? []).some(
      (p) => p.id !== this.draftProfile!.id && (p.name ?? '').trim().toLowerCase() === nameKey,
    );
    if (duplicate) {
      this.formError = this.i18n.t('profiles.validationDuplicateName');
      return;
    }

    if (this.providerNeedsUsername(this.draftProfile.provider)) {
      const login = (this.draftProfile.providerUserName ?? '').trim();
      if (!login) {
        this.formError = this.i18n.t('profiles.validationBitbucketUser');
        return;
      }
      this.draftProfile.providerUserName = login;
    }

    const timeoutSec = this.draftProfile.operationTimeoutSeconds;
    if (timeoutSec < 15) {
      this.formError = this.i18n.t('profiles.validationOperationTimeoutMin');
      return;
    }
    if (timeoutSec > 86400) {
      this.formError = this.i18n.t('profiles.validationOperationTimeoutMax');
      return;
    }
    if (!this.draftProfile.keepArchivesUntilDiskLimit && this.draftProfile.zipArchivesToKeep < 1) {
      this.formError = this.i18n.t('profiles.validationZipArchivesToKeepMin');
      return;
    }

    this.draftProfile.name = name;
    this.clampDraftParallelism();
    this.draftProfile.zipAfterBackup = true;
    this.formError = null;
    this.savingProfile = true;

    const rawItems = [...(this.settings.targetItems ?? [])];
    const idx = rawItems.findIndex((p) => p.id === this.draftProfile!.id);
    const next = { ...this.draftProfile };
    if (idx >= 0) rawItems[idx] = next;
    else rawItems.push(next);

    const items = rawItems.map((p) => normalizeProfile(JSON.parse(JSON.stringify(p)) as TargetItemModel));
    const payload: SettingsModel = { ...this.settings, targetItems: items };
    this.settingsApi.putSettings(payload).subscribe({
      next: () => {
        this.savingProfile = false;
        this.router.navigate(['/profiles']);
      },
      error: (e) => {
        this.savingProfile = false;
        this.toast.error(e);
      },
    });
  }

  private clampDraftParallelism(): void {
    if (!this.draftProfile) return;
    const max = this.maxRepoParallelism();
    let v = this.draftProfile.repositoryBackupParallelism;
    if (v == null || !Number.isFinite(v)) v = 2;
    this.draftProfile.repositoryBackupParallelism = Math.min(max, Math.max(1, Math.round(v)));
  }

  private refreshOperationTimeoutPickDate(): void {
    if (!this.draftProfile) return;
    const cap = Math.min(86400, Math.max(0, this.draftProfile.operationTimeoutSeconds));
    const d = new Date(1970, 0, 1, 0, 0, 0);
    d.setSeconds(cap);
    this.operationTimeoutPickDate = d;
  }
}
