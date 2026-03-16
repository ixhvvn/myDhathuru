import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription, filter, finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { NAME_REGEX, PHONE_REGEX } from '../../../core/validators/input-patterns';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AppButtonComponent, AppCardComponent],
  template: `
    <div class="auth-shell">
      <span class="ambient blob-a"></span>
      <span class="ambient blob-b"></span>
      <span class="ambient blob-c"></span>
      <span class="noise"></span>

      <app-card class="auth-card">
        <div class="auth-head">
          <span class="eyebrow">{{ authEyebrow() }}</span>
          <h2>{{ authHeading() }}</h2>
          <p>{{ authSubheading() }}</p>
        </div>

        <div *ngIf="mode() !== 'forgot' && !signupSubmitted()" class="tab-strip" role="tablist" aria-label="Authentication tabs">
          <button [class.active]="mode() === 'login'" (click)="setMode('login')">Login</button>
          <button [class.active]="mode() === 'signup'" (click)="setMode('signup')">Signup</button>
        </div>

        <div class="form-stage">
          <form *ngIf="mode() === 'login'" [formGroup]="loginForm" (ngSubmit)="login()" class="auth-form in">
            <label>
              Admin or Company Email
              <input formControlName="email" type="email" autocomplete="email">
            </label>
            <small *ngIf="loginForm.controls.email.touched && loginForm.controls.email.hasError('email')">Enter a valid email address.</small>
            <label>
              Password
              <div class="input-shell">
                <input [type]="showLoginPassword() ? 'text' : 'password'" formControlName="password" autocomplete="current-password">
                <button type="button" class="eye-btn" (click)="showLoginPassword.set(!showLoginPassword())" [attr.aria-label]="showLoginPassword() ? 'Hide password' : 'Show password'">
                  {{ showLoginPassword() ? 'Hide' : 'Show' }}
                </button>
              </div>
            </label>
            <small>You can login using the admin user email or the company email.</small>
            <app-button type="submit" [loading]="loading()" [fullWidth]="true">Login</app-button>
            <button type="button" class="forgot-link" (click)="setMode('forgot')">Forgot password?</button>
          </form>

          <div *ngIf="mode() === 'signup' && signupSubmitted()" class="signup-success in">
            <div class="success-badge" aria-hidden="true">
              <span class="success-ring"></span>
              <span class="success-check">✓</span>
            </div>

            <div class="success-copy">
              <strong>Your request has been submitted for approval.</strong>
              <p>Portal admin will review your business details before access is activated. Once approved, you can sign in using your company email or the admin user email you submitted.</p>
            </div>

            <div class="success-actions">
              <app-button type="button" [fullWidth]="true" (clicked)="setMode('login')">Back to Login</app-button>
              <button type="button" class="forgot-back" (click)="openNewSignupRequest()">Submit another request</button>
            </div>
          </div>

          <form *ngIf="mode() === 'signup' && !signupSubmitted()" [formGroup]="signupForm" (ngSubmit)="signup()" class="auth-form in">
            <label>Company Name <input formControlName="companyName"></label>
            <small *ngIf="signupForm.controls.companyName.touched && signupForm.controls.companyName.hasError('pattern')">Company name must not contain numbers.</small>

            <label>Company Email <input formControlName="companyEmail" type="email"></label>
            <small *ngIf="signupForm.controls.companyEmail.touched && signupForm.controls.companyEmail.hasError('email')">Enter a valid company email address.</small>

            <label>Company Phone Number <input formControlName="companyPhoneNumber" type="tel"></label>
            <small *ngIf="signupForm.controls.companyPhoneNumber.touched && signupForm.controls.companyPhoneNumber.hasError('pattern')">Phone number must contain only digits (optional leading +).</small>

            <label>Company TIN Number <input formControlName="companyTinNumber"></label>
            <label>Business Registration Number <input formControlName="businessRegistrationNumber"></label>

            <label>Admin Full Name <input formControlName="adminFullName"></label>
            <small *ngIf="signupForm.controls.adminFullName.touched && signupForm.controls.adminFullName.hasError('pattern')">Admin full name must not contain numbers.</small>

            <label>Admin User Email <input formControlName="adminUserEmail" type="email"></label>
            <small *ngIf="signupForm.controls.adminUserEmail.touched && signupForm.controls.adminUserEmail.hasError('email')">Enter a valid admin email address.</small>
            <small>Admin user email is used for login and password reset.</small>

            <label>
              Password
              <div class="input-shell">
                <input [type]="showSignupPassword() ? 'text' : 'password'" formControlName="password" autocomplete="new-password">
                <button type="button" class="eye-btn" (click)="showSignupPassword.set(!showSignupPassword())" [attr.aria-label]="showSignupPassword() ? 'Hide password' : 'Show password'">
                  {{ showSignupPassword() ? 'Hide' : 'Show' }}
                </button>
              </div>
            </label>

            <label>
              Confirm Password
              <div class="input-shell">
                <input [type]="showSignupConfirmPassword() ? 'text' : 'password'" formControlName="confirmPassword" autocomplete="new-password">
                <button type="button" class="eye-btn" (click)="showSignupConfirmPassword.set(!showSignupConfirmPassword())" [attr.aria-label]="showSignupConfirmPassword() ? 'Hide password' : 'Show password'">
                  {{ showSignupConfirmPassword() ? 'Hide' : 'Show' }}
                </button>
              </div>
            </label>

            <app-button type="submit" [loading]="loading()" [fullWidth]="true">Submit Signup Request</app-button>
          </form>

          <form *ngIf="mode() === 'forgot'" [formGroup]="forgotForm" (ngSubmit)="forgotPassword()" class="auth-form in">
            <label>
              User Email
              <input formControlName="email" type="email" autocomplete="email">
            </label>
            <small *ngIf="forgotForm.controls.email.touched && forgotForm.controls.email.hasError('email')">Enter a valid email address.</small>
            <small>A secure reset link with expiry time will be sent to your email.</small>
            <app-button type="submit" [loading]="loading()" [fullWidth]="true">Send Reset Link</app-button>
            <button type="button" class="forgot-back" (click)="setMode('login')">Back to login</button>
          </form>
        </div>
      </app-card>
    </div>
  `
})
export class LoginPageComponent implements OnInit, OnDestroy {
  readonly mode = signal<'login' | 'signup' | 'forgot'>('login');
  readonly loading = signal(false);
  readonly signupSubmitted = signal(false);
  readonly showLoginPassword = signal(false);
  readonly showSignupPassword = signal(false);
  readonly showSignupConfirmPassword = signal(false);

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private routeSubscription: Subscription | null = null;

  readonly loginForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  readonly signupForm = this.fb.nonNullable.group({
    companyName: ['', [Validators.required, Validators.pattern(NAME_REGEX)]],
    companyEmail: ['', [Validators.required, Validators.email]],
    companyPhoneNumber: ['', [Validators.required, Validators.pattern(PHONE_REGEX)]],
    companyTinNumber: ['', Validators.required],
    businessRegistrationNumber: ['', Validators.required],
    adminFullName: ['', [Validators.required, Validators.pattern(NAME_REGEX)]],
    adminUserEmail: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
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

  authEyebrow(): string {
    return this.mode() === 'signup' && this.signupSubmitted() ? 'Request Submitted' : 'Secure Access';
  }

  authHeading(): string {
    if (this.mode() === 'forgot') {
      return 'Reset your password';
    }

    if (this.mode() === 'signup' && this.signupSubmitted()) {
      return 'Signup Request Submitted';
    }

    return 'Welcome Back';
  }

  authSubheading(): string {
    if (this.mode() === 'forgot') {
      return 'Enter your account email to receive a secure reset link.';
    }

    if (this.mode() === 'signup' && this.signupSubmitted()) {
      return 'Your request has been submitted for approval. Portal admin will review your details before activating access.';
    }

    return 'Login, create your account, or request a password reset link.';
  }

  setMode(mode: 'login' | 'signup' | 'forgot'): void {
    const targetUrl = mode === 'forgot'
      ? '/forgot-password'
      : mode === 'signup'
        ? '/login?mode=signup'
        : '/login';

    if (this.router.url !== targetUrl) {
      this.router.navigateByUrl(targetUrl);
      return;
    }

    this.mode.set(mode);
    this.signupSubmitted.set(false);
  }

  openNewSignupRequest(): void {
    this.signupForm.reset();
    this.signupSubmitted.set(false);
    this.router.navigateByUrl('/login?mode=signup');
  }

  login(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      this.toast.error('Please enter valid login credentials.');
      return;
    }

    this.loading.set(true);
    this.authService.login(this.loginForm.getRawValue())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Welcome back.');
          this.router.navigate(['/app/dashboard']);
        },
        error: (error) => this.toast.error(this.readError(error, 'Login failed.'))
      });
  }

  signup(): void {
    if (this.signupForm.invalid) {
      this.signupForm.markAllAsTouched();
      this.toast.error('Please complete all signup fields correctly.');
      return;
    }

    const value = this.signupForm.getRawValue();
    if (value.password !== value.confirmPassword) {
      this.toast.error('Password and confirm password must match.');
      return;
    }

    this.loading.set(true);
    this.authService.signup(value)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Signup request has been sent to portal admin.');
          this.signupForm.reset();
          this.router.navigateByUrl('/login?mode=signup&submitted=1');
        },
        error: (error) => this.toast.error(this.readError(error, 'Signup failed.'))
      });
  }

  forgotPassword(): void {
    if (this.forgotForm.invalid) {
      this.forgotForm.markAllAsTouched();
      this.toast.error('Enter a valid email for password reset.');
      return;
    }

    this.loading.set(true);
    this.authService.forgotPassword(this.forgotForm.getRawValue())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => this.toast.info('If the account exists, a reset link was sent to email.'),
        error: (error) => this.toast.error(this.readError(error, 'Could not send reset link.'))
      });
  }

  private readError(error: unknown, fallback: string): string {
    return extractApiError(error, fallback);
  }

  private syncModeFromUrl(url: string): void {
    const [pathname, query] = url.split('?');
    if (pathname.includes('/forgot-password')) {
      this.mode.set('forgot');
      this.signupSubmitted.set(false);
      return;
    }

    if (query) {
      const params = new URLSearchParams(query);
      const requestedMode = params.get('mode');
      const submitted = params.get('submitted') === '1';

      if (requestedMode === 'signup') {
        this.mode.set('signup');
        this.signupSubmitted.set(submitted);
        return;
      }

      if (requestedMode === 'forgot') {
        this.mode.set('forgot');
        this.signupSubmitted.set(false);
        return;
      }
    }

    this.mode.set('login');
    this.signupSubmitted.set(false);
  }
}
