import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { Subscription, filter, finalize } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { PortalAdminAuthService } from '../../../core/services/portal-admin-auth.service';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { extractApiError } from '../../../core/utils/api-error.util';

type AuthMode = 'login' | 'forgot';

@Component({
  selector: 'app-portal-admin-login-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AppButtonComponent, AppCardComponent],
  template: `
    <div class="admin-auth-shell">
      <span class="ambient blob-a"></span>
      <span class="ambient blob-b"></span>
      <span class="ambient blob-c"></span>
      <span class="noise"></span>

      <app-card class="auth-card">
        <div class="brand-strip">
          <img src="/newlogo.png" alt="myDhathuru logo">
          <div>
            <strong>myDhathuru Portal Admin</strong>
            <small>Platform control center</small>
          </div>
        </div>

        <div class="card-head">
          <span class="eyebrow">Portal Admin Access</span>
          <h2>Welcome Back</h2>
          <p>Sign in to manage businesses, approvals, and platform controls.</p>
        </div>

        <div class="tabs">
          <button type="button" [class.active]="mode() === 'login'" (click)="setMode('login')">Login</button>
          <button type="button" [class.active]="mode() === 'forgot'" (click)="setMode('forgot')">Forgot Password</button>
        </div>

        <form *ngIf="mode() === 'login'" [formGroup]="loginForm" (ngSubmit)="login()" class="auth-form">
          <label>
            Email
            <input type="email" formControlName="email" autocomplete="email" placeholder="mydhathuru@gmail.com">
          </label>

          <label>
            Password
            <div class="input-shell">
              <input [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="current-password">
              <button type="button" class="toggle-btn" (click)="showPassword.set(!showPassword())">
                {{ showPassword() ? 'Hide' : 'Show' }}
              </button>
            </div>
          </label>

          <app-button type="submit" [fullWidth]="true" [loading]="loading()">Login</app-button>
        </form>

        <form *ngIf="mode() === 'forgot'" [formGroup]="forgotForm" (ngSubmit)="forgotPassword()" class="auth-form">
          <label>
            Portal Admin Email
            <input type="email" formControlName="email" autocomplete="email" placeholder="mydhathuru@gmail.com">
          </label>

          <p class="hint">A secure reset link with 30-minute expiry will be sent to your email.</p>
          <app-button type="submit" [fullWidth]="true" [loading]="loading()">Send Reset Link</app-button>
        </form>

        <a class="switch-link" routerLink="/login">Business portal login</a>
      </app-card>
    </div>
  `,
  styles: `
    .admin-auth-shell {
      min-height: 100dvh;
      padding: 1rem;
      display: flex;
      align-items: center;
      justify-content: center;
      isolation: isolate;
      background:
        radial-gradient(circle at 12% 10%, rgba(124, 139, 253, .28), transparent 40%),
        radial-gradient(circle at 84% 16%, rgba(114, 223, 225, .24), transparent 44%),
        linear-gradient(160deg, #f6f8ff 0%, #eef4ff 100%);
      position: relative;
      overflow: hidden;
    }

    .admin-auth-shell::before {
      content: '';
      position: absolute;
      z-index: 0;
      width: 440px;
      height: 440px;
      top: -170px;
      left: -130px;
      border-radius: 50%;
      background: radial-gradient(circle, rgba(124, 142, 246, .34), rgba(124, 142, 246, 0) 70%);
      filter: blur(6px);
      animation: drift 15s ease-in-out infinite;
      pointer-events: none;
    }

    .admin-auth-shell::after {
      content: '';
      position: absolute;
      z-index: 0;
      width: 400px;
      height: 400px;
      right: -130px;
      bottom: -150px;
      border-radius: 50%;
      background: radial-gradient(circle, rgba(97, 212, 224, .3), rgba(97, 212, 224, 0) 70%);
      filter: blur(5px);
      animation: drift 18s ease-in-out infinite reverse;
      pointer-events: none;
    }

    .ambient {
      position: absolute;
      border-radius: 999px;
      filter: blur(12px);
      pointer-events: none;
      z-index: 0;
      opacity: .54;
      animation: drift 14s ease-in-out infinite;
    }

    .blob-a {
      width: 300px;
      height: 300px;
      right: -120px;
      top: -110px;
      background: radial-gradient(circle, rgba(111, 126, 247, .34), rgba(111, 126, 247, 0) 72%);
    }

    .blob-b {
      width: 280px;
      height: 280px;
      left: -90px;
      bottom: -120px;
      background: radial-gradient(circle, rgba(99, 211, 225, .3), rgba(99, 211, 225, 0) 72%);
      animation-delay: 2s;
    }

    .blob-c {
      width: 220px;
      height: 220px;
      top: 56%;
      left: 62%;
      background: radial-gradient(circle, rgba(176, 161, 255, .34), rgba(176, 161, 255, 0) 72%);
      animation-delay: 4s;
    }

    .noise {
      position: absolute;
      inset: 0;
      z-index: 0;
      pointer-events: none;
      opacity: .04;
      background-image: radial-gradient(#5f7fb6 0.6px, transparent 0.6px);
      background-size: 11px 11px;
      animation: grainShift 20s linear infinite;
    }

    .auth-card {
      position: relative;
      z-index: 1;
      align-self: center;
      width: min(460px, calc(100vw - 1.5rem));
      max-width: 460px;
      margin-inline: auto;
      --card-padding: 1.2rem;
      --card-radius: 28px;
      --card-border: rgba(255, 255, 255, .86);
      --card-bg: linear-gradient(155deg, rgba(255, 255, 255, .96), rgba(247, 250, 255, .9));
      --shadow-soft: 0 28px 62px rgba(68, 90, 148, .22), inset 0 1px 0 rgba(255, 255, 255, .7);
      --shadow-hover: 0 32px 70px rgba(63, 88, 154, .26), inset 0 1px 0 rgba(255, 255, 255, .78);
      box-shadow: none !important;
    }

    .brand-strip {
      display: inline-flex;
      align-items: center;
      gap: .55rem;
      width: fit-content;
      margin-bottom: .65rem;
      padding: .44rem .6rem;
      border-radius: 14px;
      border: 1px solid #d8e2f8;
      background: linear-gradient(140deg, rgba(244, 249, 255, .95), rgba(234, 242, 255, .9));
    }

    .brand-strip img {
      width: 32px;
      height: 32px;
      border-radius: 10px;
      border: 1px solid #d7e1f5;
      background: #fff;
      object-fit: cover;
    }

    .brand-strip strong {
      display: block;
      font-family: var(--font-heading);
      font-size: .92rem;
      color: #34456e;
      font-weight: 600;
      line-height: 1.1;
    }

    .brand-strip small {
      display: block;
      margin-top: .08rem;
      color: #6478a4;
      font-size: .72rem;
      line-height: 1;
    }

    .card-head .eyebrow {
      display: inline-flex;
      align-items: center;
      padding: .2rem .52rem;
      border-radius: 999px;
      background: rgba(115, 132, 247, .12);
      border: 1px solid rgba(115, 132, 247, .28);
      color: #5c6fba;
      font-size: .74rem;
      font-family: var(--font-heading);
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: .06em;
    }
    .card-head h2 {
      margin: .48rem 0 .2rem;
      color: #2f4068;
      font-size: 1.82rem;
      font-family: var(--font-heading);
      font-weight: 600;
      line-height: 1.05;
    }
    .card-head p {
      margin: 0;
      color: #60729a;
      font-size: .88rem;
      line-height: 1.4;
    }

    .tabs {
      margin-top: .9rem;
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      padding: .24rem;
      border-radius: 12px;
      border: 1px solid #d9e2f9;
      background: linear-gradient(140deg, #f8fbff, #eef4ff);
      gap: .24rem;
    }
    .tabs button {
      border: none;
      border-radius: 10px;
      padding: .52rem .68rem;
      font-size: .88rem;
      font-family: var(--font-heading);
      font-weight: 600;
      color: #5a6b92;
      background: transparent;
      cursor: pointer;
      transition: background .18s ease, color .18s ease, box-shadow .18s ease;
    }
    .tabs button.active {
      color: #fff;
      background: linear-gradient(135deg, #7283f7, #67a9ee);
      box-shadow: 0 10px 20px rgba(87, 110, 203, .28);
      transform: translateY(-1px);
    }
    .auth-form {
      margin-top: .84rem;
      display: grid;
      gap: .55rem;
    }
    label {
      display: grid;
      gap: .28rem;
      color: #56698f;
      font-size: .82rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    input {
      width: 100%;
      border-radius: 11px;
      border: 1px solid #cfdcf5;
      background: rgba(255,255,255,.95);
      color: #304261;
      padding: .62rem .7rem;
      font-size: .93rem;
      transition: border-color .18s ease, box-shadow .18s ease;
    }
    input:focus {
      outline: none;
      border-color: #7d8cf7;
      box-shadow: 0 0 0 3px rgba(125, 140, 247, .16);
    }
    .input-shell {
      position: relative;
    }
    .input-shell input {
      padding-right: 4.6rem;
    }
    .toggle-btn {
      position: absolute;
      right: .34rem;
      top: 50%;
      transform: translateY(-50%);
      border: 1px solid #d7e2fa;
      border-radius: 9px;
      background: linear-gradient(135deg, #f4f8ff, #e9f2ff);
      color: #566a93;
      font-family: var(--font-heading);
      font-size: .74rem;
      font-weight: 600;
      padding: .32rem .55rem;
      cursor: pointer;
    }
    .hint {
      margin: 0;
      color: #60729a;
      font-size: .84rem;
      line-height: 1.45;
    }
    .switch-link {
      display: inline-flex;
      margin-top: .78rem;
      color: #4e64b4;
      text-decoration: none;
      font-family: var(--font-heading);
      font-size: .82rem;
      font-weight: 600;
    }
    .switch-link:hover {
      color: #394d92;
      text-decoration: underline;
    }

    @media (max-width: 680px) {
      .admin-auth-shell {
        padding: .7rem;
        align-items: flex-start;
      }
      .auth-card {
        width: min(460px, calc(100vw - 1rem));
        max-width: 460px;
        --card-padding: .95rem;
      }

      .tabs {
        grid-template-columns: 1fr;
      }

      .card-head h2 {
        font-size: 1.5rem;
      }
    }

    @keyframes drift {
      0%, 100% {
        transform: translate3d(0, 0, 0) scale(1);
      }
      50% {
        transform: translate3d(0, -12px, 0) scale(1.04);
      }
    }

    @keyframes grainShift {
      0% {
        transform: translate3d(0, 0, 0);
      }
      100% {
        transform: translate3d(11px, 11px, 0);
      }
    }
  `
})
export class PortalAdminLoginPageComponent implements OnInit, OnDestroy {
  readonly mode = signal<AuthMode>('login');
  readonly loading = signal(false);
  readonly showPassword = signal(false);

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(PortalAdminAuthService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private routeSubscription: Subscription | null = null;

  readonly loginForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  readonly forgotForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]]
  });

  ngOnInit(): void {
    this.syncModeFromUrl(this.router.url);
    this.routeSubscription = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => this.syncModeFromUrl(event.urlAfterRedirects));
  }

  ngOnDestroy(): void {
    this.routeSubscription?.unsubscribe();
  }

  setMode(mode: AuthMode): void {
    this.mode.set(mode);
    const path = mode === 'login' ? '/portal-admin/login' : '/portal-admin/forgot-password';
    if (this.router.url !== path) {
      this.router.navigateByUrl(path);
    }
  }

  login(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      this.toast.error('Enter valid admin credentials.');
      return;
    }

    this.loading.set(true);
    this.authService.login(this.loginForm.getRawValue())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Portal admin login successful.');
          this.router.navigate(['/portal-admin/dashboard']);
        },
        error: (error) => this.toast.error(extractApiError(error, 'Portal admin login failed.'))
      });
  }

  forgotPassword(): void {
    if (this.forgotForm.invalid) {
      this.forgotForm.markAllAsTouched();
      this.toast.error('Enter a valid email address.');
      return;
    }

    this.loading.set(true);
    this.authService.forgotPassword(this.forgotForm.getRawValue())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => this.toast.info('If the account exists, a reset link was sent to email.'),
        error: (error) => this.toast.error(extractApiError(error, 'Unable to send reset link.'))
      });
  }

  private syncModeFromUrl(url: string): void {
    this.mode.set(url.includes('/portal-admin/forgot-password') ? 'forgot' : 'login');
  }
}

