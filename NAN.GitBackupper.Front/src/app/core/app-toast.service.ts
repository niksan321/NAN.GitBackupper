import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { MessageService } from 'primeng/api';
import { AppI18nService } from '../i18n/app-i18n.service';
import { formatApiError } from './format-api-error';

@Injectable({ providedIn: 'root' })
export class AppToastService {
  private readonly messages = inject(MessageService);
  private readonly i18n = inject(AppI18nService);

  private readonly lifeMs = 4000;

  success(detail: string, summary?: string): void {
    this.messages.add({
      severity: 'success',
      summary: summary ?? this.i18n.t('toast.successSummary'),
      detail,
      life: this.lifeMs,
    });
  }

  error(err: unknown, summary?: string): void {
    const detail =
      err instanceof HttpErrorResponse && err.status === 0
        ? this.i18n.t('errors.serverUnavailable')
        : formatApiError(err);
    this.messages.add({
      severity: 'error',
      summary: summary ?? this.i18n.t('toast.errorSummary'),
      detail,
      life: this.lifeMs,
    });
  }

  errorMessage(detail: string, summary?: string): void {
    this.messages.add({
      severity: 'error',
      summary: summary ?? this.i18n.t('toast.errorSummary'),
      detail,
      life: this.lifeMs,
    });
  }
}
