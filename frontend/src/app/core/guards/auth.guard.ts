import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { PortalAdminAuthService } from '../services/portal-admin-auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const portalAdminAuthService = inject(PortalAdminAuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  if (portalAdminAuthService.isAuthenticated()) {
    return router.createUrlTree(['/portal-admin/dashboard']);
  }

  if (authService.user() && authService.hasRefreshToken()) {
    return authService.refreshToken().pipe(
      map(() => true),
      catchError(() => of(router.createUrlTree(['/login'])))
    );
  }

  return router.createUrlTree(['/login']);
};
