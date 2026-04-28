import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./layout/main-layout.component').then((m) => m.MainLayoutComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'profiles' },
      {
        path: 'profiles',
        loadComponent: () =>
          import('./pages/gb-home/gb-home.component').then((m) => m.GbHomeComponent),
        data: { titleKey: 'pageProfiles' },
      },
      {
        path: 'profiles/new',
        loadComponent: () =>
          import('./pages/gb-home/gb-profile-edit-page.component').then((m) => m.GbProfileEditPageComponent),
        data: { titleKey: 'pageProfiles' },
      },
      {
        path: 'profiles/:id/edit',
        loadComponent: () =>
          import('./pages/gb-home/gb-profile-edit-page.component').then((m) => m.GbProfileEditPageComponent),
        data: { titleKey: 'pageProfiles' },
      },
      {
        path: 'profiles/:id/repositories',
        loadComponent: () =>
          import('./pages/gb-home/gb-profile-repositories-dialog.component').then((m) => m.GbProfileRepositoriesDialogComponent),
        data: { titleKey: 'pageRepositories' },
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./pages/gb-settings/gb-settings.component').then((m) => m.GbSettingsComponent),
        data: { titleKey: 'pageSettings' },
      },
      {
        path: 'about',
        loadComponent: () =>
          import('./pages/gb-about/gb-about.component').then((m) => m.GbAboutComponent),
        data: { titleKey: 'pageAbout' },
      },
      {
        path: 'backups',
        loadComponent: () =>
          import('./pages/gb-backups/gb-backups.component').then((m) => m.GbBackupsComponent),
        data: { titleKey: 'pageBackups' },
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
