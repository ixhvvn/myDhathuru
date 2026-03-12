import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { PortalAdminAuthService } from '../services/portal-admin-auth.service';

export const businessAuthPageGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const portalAdminAuthService = inject(PortalAdminAuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return router.createUrlTree(['/app/dashboard']);
  }

  if (portalAdminAuthService.isAuthenticated()) {
    return router.createUrlTree(['/portal-admin/dashboard']);
  }

  return true;
};
