import { Component, OnInit, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SelectModule } from 'primeng/select';
import type { SchedulerStartupPreference, SettingsModel } from '../../core/api-types';
import { AppToastService } from '../../core/app-toast.service';
import { SettingsApiService } from '../../core/settings-api.service';
import { AppI18nService } from '../../i18n/app-i18n.service';

@Component({
  selector: 'gb-settings',
  imports: [FormsModule, CardModule, ButtonModule, SelectModule],
  templateUrl: './gb-settings.component.html',
  styleUrl: './gb-settings.component.scss',
})
export class GbSettingsComponent implements OnInit {
  private readonly settingsApi = inject(SettingsApiService);
  private readonly toast = inject(AppToastService);
  readonly i18n = inject(AppI18nService);

  settings: SettingsModel | null = null;
  saving = false;

  readonly schedulerStartupOptions = computed(() => {
    this.i18n.lang();
    const t = (k: string) => this.i18n.t(k);
    return [
      { label: t('settings.schedulerStartupAlways'), value: 0 as SchedulerStartupPreference },
      { label: t('settings.schedulerStartupNever'), value: 1 as SchedulerStartupPreference },
      { label: t('settings.schedulerStartupResume'), value: 2 as SchedulerStartupPreference },
    ];
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.settingsApi.getSettings().subscribe({
      next: (s) => {
        this.settings = { ...s };
        if (this.settings.schedulerStartupPreference == null)
          this.settings.schedulerStartupPreference = 2;
      },
      error: (e) => this.toast.error(e),
    });
  }

  save(): void {
    if (!this.settings) return;
    this.saving = true;
    const payload: SettingsModel = {
      ...this.settings,
      profileBackupStatuses: undefined,
    };
    this.settingsApi.putSettings(payload).subscribe({
      next: (s) => {
        this.settings = { ...s };
        if (this.settings.schedulerStartupPreference == null)
          this.settings.schedulerStartupPreference = 2;
        this.saving = false;
        this.toast.success(this.i18n.t('settings.saved'));
      },
      error: (e) => {
        this.saving = false;
        this.toast.error(e);
      },
    });
  }
}
