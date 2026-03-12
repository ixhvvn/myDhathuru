import { Routes } from '@angular/router';
import { authRoutes } from './features/auth/auth.routes';
import { businessRoutes } from './features/business/business.routes';
import { portalAdminRoutes } from './features/portal-admin/portal-admin.routes';
import { publicRoutes } from './features/public/public.routes';

export const routes: Routes = [
  ...publicRoutes,
  ...authRoutes,
  ...businessRoutes,
  ...portalAdminRoutes,
  { path: '**', redirectTo: '' }
];
