import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { filter } from 'rxjs';
import { AppToastService } from '../core/app-toast.service';
import { SchedulerApiService } from '../core/scheduler-api.service';
import { AppI18nService } from '../i18n/app-i18n.service';

@Component({
    selector: 'gb-main-layout',
    imports: [
        CommonModule,
        RouterOutlet,
        RouterLink,
        RouterLinkActive,
        ButtonModule,
        ToastModule,
        TooltipModule,
        ConfirmDialogModule,
    ],
    templateUrl: './main-layout.component.html',
    styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent implements OnInit {
  private static readonly sidebarCollapsedStorageKey = 'gb.sidebarCollapsed';
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly schedulerApi = inject(SchedulerApiService);
  private readonly toast = inject(AppToastService);
  readonly i18n = inject(AppI18nService);

  schedulerRunning = false;
  isSidebarCollapsed = false;

  readonly navItems = [
    { link: '/profiles', labelKey: 'navProfiles', icon: 'pi pi-folder', exact: false },
    { link: '/backups', labelKey: 'navBackups', icon: 'pi pi-download', exact: true },
    { link: '/settings', labelKey: 'navSettings', icon: 'pi pi-cog', exact: true },
    { link: '/about', labelKey: 'navAbout', icon: 'pi pi-info-circle', exact: true },
  ] as const;

  private readonly pageTitleKey = signal<string | null>(null);

  readonly pageTitleText = computed(() => {
    this.i18n.lang();
    const k = this.pageTitleKey();
    if (!k) return '';
    return this.i18n.t(k);
  });

  ngOnInit(): void {
    this.restoreSidebarState();
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe(() => this.syncTitle());
    this.syncTitle();
    this.schedulerApi.status().subscribe({
      next: (x) => (this.schedulerRunning = x.isRunning),
      error: (e) => this.toast.error(e),
    });
  }

  toggleScheduler(): void {
    const req = this.schedulerRunning ? this.schedulerApi.stop() : this.schedulerApi.start();
    req.subscribe({
      next: (x) => (this.schedulerRunning = x.isRunning),
      error: (e) => this.toast.error(e),
    });
  }

  toggleSidebar(): void {
    this.isSidebarCollapsed = !this.isSidebarCollapsed;
    this.persistSidebarState();
  }

  private restoreSidebarState(): void {
    try {
      const raw = localStorage.getItem(MainLayoutComponent.sidebarCollapsedStorageKey);
      this.isSidebarCollapsed = raw === '1';
    } catch {
      this.isSidebarCollapsed = false;
    }
  }

  private persistSidebarState(): void {
    try {
      localStorage.setItem(MainLayoutComponent.sidebarCollapsedStorageKey, this.isSidebarCollapsed ? '1' : '0');
    } catch {
      // localStorage может быть недоступен в ограниченных окружениях
    }
  }

  private syncTitle(): void {
    let r: ActivatedRoute | null = this.route;
    while (r?.firstChild) r = r.firstChild;
    const key = r?.snapshot.data['titleKey'] as string | undefined;
    this.pageTitleKey.set(key ?? null);
  }
}
