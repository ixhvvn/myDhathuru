import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { PortalAdminAuthService } from '../services/portal-admin-auth.service';

export const portalAdminAuthGuard: CanActivateFn = () => {
  const authService = inject(PortalAdminAuthService);
  const tenantAuthService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  if (tenantAuthService.isAuthenticated()) {
    return router.createUrlTree(['/app/dashboard']);
  }

  return router.createUrlTree(['/portal-admin/login']);
};

