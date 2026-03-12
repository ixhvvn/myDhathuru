import { Routes } from '@angular/router';
import { publicEntryHostGuard } from '../../core/guards/host-routing.guard';
import { PublicLayoutComponent } from '../../layout/public-layout/public-layout.component';
import { LandingPageComponent } from './landing-page/landing-page.component';

export const publicRoutes: Routes = [
  {
    path: '',
    component: PublicLayoutComponent,
    children: [
      { path: '', component: LandingPageComponent, pathMatch: 'full', canActivate: [publicEntryHostGuard] }
    ]
  },
  { path: 'features', redirectTo: '', pathMatch: 'full' },
  { path: 'pricing', redirectTo: '', pathMatch: 'full' },
  { path: 'contact', redirectTo: '', pathMatch: 'full' }
];
