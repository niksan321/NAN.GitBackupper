import { Injectable, signal } from '@angular/core';
import type { AppLang } from './messages';
import { messages } from './messages';

export type AppTheme = 'light' | 'dark';

const storageLang = 'gb-lang';
const storageTheme = 'gb-theme';

@Injectable({ providedIn: 'root' })
export class AppI18nService {
  readonly lang = signal<AppLang>(this.readStoredLang());
  readonly theme = signal<AppTheme>(this.readStoredTheme());

  constructor() {
    document.documentElement.lang = this.lang();
    this.applyTheme(this.theme());
  }

  t(key: string): string {
    this.lang();
    const table = messages[this.lang()];
    return table[key] ?? key;
  }

  setLang(next: AppLang): void {
    this.lang.set(next);
    document.documentElement.lang = next;
    try {
      localStorage.setItem(storageLang, next);
    } catch {
      // ignore
    }
  }

  toggleLang(): void {
    this.setLang(this.lang() === 'en' ? 'ru' : 'en');
  }

  setTheme(next: AppTheme): void {
    this.theme.set(next);
    this.applyTheme(next);
    try {
      localStorage.setItem(storageTheme, next);
    } catch {
      // ignore
    }
  }

  toggleTheme(): void {
    this.setTheme(this.theme() === 'dark' ? 'light' : 'dark');
  }

  private applyTheme(mode: AppTheme): void {
    document.documentElement.classList.toggle('dark', mode === 'dark');
  }

  private readStoredLang(): AppLang {
    try {
      const v = localStorage.getItem(storageLang);
      if (v === 'en' || v === 'ru') return v;
    } catch {
      // ignore
    }
    return 'en';
  }

  private readStoredTheme(): AppTheme {
    try {
      const v = localStorage.getItem(storageTheme);
      if (v === 'light' || v === 'dark') return v;
    } catch {
      // ignore
    }
    return 'dark';
  }

}
