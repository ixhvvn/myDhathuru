import { Routes } from '@angular/router';
import { publicEntryHostGuard } from '../../core/guards/host-routing.guard';
import { PublicLayoutComponent } from '../../layout/public-layout/public-layout.component';

export const publicRoutes: Routes = [
  {
    path: '',
    component: PublicLayoutComponent,
    children: [
      {
        path: '',
        loadComponent: () => import('./landing-page/landing-page.component').then((m) => m.LandingPageComponent),
        pathMatch: 'full',
        canActivate: [publicEntryHostGuard]
      }
    ]
  },
  { path: 'features', redirectTo: '', pathMatch: 'full' },
  { path: 'pricing', redirectTo: '', pathMatch: 'full' },
  { path: 'contact', redirectTo: '', pathMatch: 'full' }
];
