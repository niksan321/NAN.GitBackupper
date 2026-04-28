import { HttpErrorResponse } from '@angular/common/http';

export function formatApiError(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error as { detail?: string; title?: string } | null | undefined;
    return body?.detail ?? body?.title ?? err.message;
  }
  return String((err as { message?: string })?.message ?? err);
}
