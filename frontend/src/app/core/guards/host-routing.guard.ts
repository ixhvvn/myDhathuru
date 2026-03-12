import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { HostContextService } from '../services/host-context.service';

export const publicEntryHostGuard: CanActivateFn = () => {
  const hostContext = inject(HostContextService);
  const router = inject(Router);

  if (hostContext.area() === 'business') {
    return router.createUrlTree(['/login']);
  }

  if (hostContext.area() === 'admin') {
    return router.createUrlTree(['/portal-admin/login']);
  }

  return true;
};

export const businessAuthHostGuard: CanActivateFn = () => {
  const hostContext = inject(HostContextService);
  const router = inject(Router);

  if (hostContext.area() === 'admin') {
    return router.createUrlTree(['/portal-admin/login']);
  }

  return true;
};

export const adminAuthHostGuard: CanActivateFn = () => {
  const hostContext = inject(HostContextService);
  const router = inject(Router);

  if (hostContext.area() === 'business') {
    return router.createUrlTree(['/login']);
  }

  return true;
};
