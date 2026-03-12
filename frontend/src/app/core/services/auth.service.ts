import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { AuthResponse, SignupRequestSubmitted, UserProfile } from '../models/app.models';
import { ToastService } from './toast.service';

const ACCESS_TOKEN_KEY = 'myDhathuru_access_token';
const REFRESH_TOKEN_KEY = 'myDhathuru_refresh_token';
const USER_KEY = 'myDhathuru_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly user = signal<UserProfile | null>(this.getStoredUser());
  readonly isAuthenticated = computed(() => !!this.user());

  login(payload: { email: string; password: string }): Observable<AuthResponse> {
    return this.api.post<AuthResponse>('auth/login', payload).pipe(
      tap((response) => this.storeAuth(response))
    );
  }

  signup(payload: {
    companyName: string;
    companyEmail: string;
    companyPhoneNumber: string;
    companyTinNumber: string;
    businessRegistrationNumber: string;
    adminFullName: string;
    adminUserEmail: string;
    password: string;
    confirmPassword: string;
  }): Observable<SignupRequestSubmitted> {
    return this.api.post<SignupRequestSubmitted>('auth/signup', payload);
  }

  forgotPassword(payload: { email: string }): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>('auth/forgot-password', payload);
  }

  resetPassword(payload: { email: string; token: string; newPassword: string; confirmPassword: string }): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>('auth/reset-password', payload);
  }

  refreshToken(): Observable<AuthResponse> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      this.logout();
      throw new Error('No refresh token available');
    }

    return this.api.post<AuthResponse>('auth/refresh', { refreshToken }).pipe(
      tap((response) => this.storeAuth(response))
    );
  }

  loadProfile(): Observable<UserProfile> {
    return this.api.get<UserProfile>('auth/me').pipe(
      tap((profile) => {
        this.user.set(profile);
        localStorage.setItem(USER_KEY, JSON.stringify(profile));
      })
    );
  }

  logout(message?: string): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.user.set(null);
    if (message) {
      this.toast.info(message);
    }
    this.router.navigate(['/login']);
  }

  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  private storeAuth(response: AuthResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
    this.user.set(response.user);
  }

  private getStoredUser(): UserProfile | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as UserProfile;
    } catch {
      return null;
    }
  }
}
