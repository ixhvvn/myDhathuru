import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { PortalAdminAuthService } from '../services/portal-admin-auth.service';

let isTenantRefreshing = false;
const tenantRefreshTokenSubject = new BehaviorSubject<string | null>(null);

let isAdminRefreshing = false;
const adminRefreshTokenSubject = new BehaviorSubject<string | null>(null);

const TENANT_AUTH_ENDPOINTS = ['auth/login', 'auth/signup', 'auth/refresh', 'auth/forgot-password', 'auth/reset-password'];
const ADMIN_AUTH_ENDPOINTS = ['portal-admin/auth/login', 'portal-admin/auth/refresh', 'portal-admin/auth/forgot-password', 'portal-admin/auth/reset-password'];

const isPortalAdminRequest = (url: string): boolean => url.includes('/portal-admin/') || url.includes('portal-admin/');

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const tenantAuthService = inject(AuthService);
  const adminAuthService = inject(PortalAdminAuthService);
  const router = inject(Router);
  const adminRequest = isPortalAdminRequest(req.url);
  const authEndpoints = adminRequest ? ADMIN_AUTH_ENDPOINTS : TENANT_AUTH_ENDPOINTS;
  const shouldSkipAuth = authEndpoints.some((path) => req.url.includes(path));

  let authReq = req;
  const accessToken = adminRequest
    ? adminAuthService.getAccessToken()
    : tenantAuthService.getAccessToken();

  if (accessToken && !shouldSkipAuth) {
    authReq = req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } });
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || shouldSkipAuth) {
        return throwError(() => error);
      }

      if (adminRequest) {
        if (!isAdminRefreshing) {
          isAdminRefreshing = true;
          adminRefreshTokenSubject.next(null);

          return adminAuthService.refreshToken().pipe(
            switchMap((response) => {
              isAdminRefreshing = false;
              adminRefreshTokenSubject.next(response.accessToken);
              return next(req.clone({
                setHeaders: {
                  Authorization: `Bearer ${response.accessToken}`
                }
              }));
            }),
            catchError((refreshError) => {
              isAdminRefreshing = false;
              adminAuthService.logout('Session expired. Please login again.');
              router.navigate(['/portal-admin/login']);
              return throwError(() => refreshError);
            })
          );
        }

        return adminRefreshTokenSubject.pipe(
          filter((token): token is string => token !== null),
          take(1),
          switchMap((token) => next(req.clone({
            setHeaders: {
              Authorization: `Bearer ${token}`
            }
          })))
        );
      }

      if (!isTenantRefreshing) {
        isTenantRefreshing = true;
        tenantRefreshTokenSubject.next(null);

        return tenantAuthService.refreshToken().pipe(
          switchMap((response) => {
            isTenantRefreshing = false;
            tenantRefreshTokenSubject.next(response.accessToken);
            return next(req.clone({
              setHeaders: {
                Authorization: `Bearer ${response.accessToken}`
              }
            }));
          }),
          catchError((refreshError) => {
            isTenantRefreshing = false;
            tenantAuthService.logout('Session expired. Please login again.');
            router.navigate(['/login']);
            return throwError(() => refreshError);
          })
        );
      }

      return tenantRefreshTokenSubject.pipe(
        filter((token): token is string => token !== null),
        take(1),
        switchMap((token) => next(req.clone({
          setHeaders: {
            Authorization: `Bearer ${token}`
          }
        })))
      );
    })
  );
};
