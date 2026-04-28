import { Component, inject } from '@angular/core';
import { CardModule } from 'primeng/card';
import { AppI18nService } from '../../i18n/app-i18n.service';

@Component({
  selector: 'gb-about',
  imports: [CardModule],
  templateUrl: './gb-about.component.html',
  styleUrl: './gb-about.component.scss',
})
export class GbAboutComponent {
  readonly i18n = inject(AppI18nService);
}
