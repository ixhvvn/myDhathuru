import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { PortalAdminAuthService } from '../../../core/services/portal-admin-auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { extractApiError } from '../../../core/utils/api-error.util';

@Component({
  selector: 'app-portal-admin-reset-password-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AppButtonComponent, AppCardComponent],
  template: `
    <div class="reset-shell">
      <app-card class="reset-card">
        <div class="header">
          <img src="/logo.svg" alt="myDhathuru logo">
          <div>
            <h1>Portal Admin Password Reset</h1>
            <p>Set a new password to continue accessing the portal admin workspace.</p>
          </div>
        </div>

        <div class="hint" *ngIf="isLinkValid()">
          Reset requested for <strong>{{ email() }}</strong>. This link expires in 30 minutes.
        </div>
        <div class="error" *ngIf="!isLinkValid()">
          Invalid or expired reset link. Please request a new reset link from portal admin login.
        </div>

        <form *ngIf="isLinkValid()" [formGroup]="form" (ngSubmit)="submit()" class="form-grid">
          <label>
            New Password
            <input type="password" formControlName="newPassword" autocomplete="new-password">
          </label>
          <label>
            Confirm Password
            <input type="password" formControlName="confirmPassword" autocomplete="new-password">
          </label>
          <div class="actions">
            <a routerLink="/portal-admin/login">Back to login</a>
            <app-button type="submit" [loading]="loading()">Reset Password</app-button>
          </div>
        </form>

        <div class="actions" *ngIf="!isLinkValid()">
          <a routerLink="/portal-admin/login">Back to login</a>
        </div>
      </app-card>
    </div>
  `,
  styles: `
    .reset-shell {
      min-height: 100dvh;
      display: grid;
      place-items: center;
      padding: 1rem;
      background:
        radial-gradient(circle at 16% 14%, rgba(122, 136, 252, .2), transparent 42%),
        radial-gradient(circle at 86% 14%, rgba(110, 220, 226, .18), transparent 44%),
        linear-gradient(160deg, #f6f8ff 0%, #eef4ff 100%);
    }
    .reset-card {
      width: min(640px, 100%);
      --card-padding: 1.05rem;
    }
    .header {
      display: flex;
      align-items: center;
      gap: .72rem;
      margin-bottom: .62rem;
    }
    .header img {
      width: 50px;
      height: 50px;
      border-radius: 12px;
      border: 1px solid #d8e2f8;
      background: #fff;
      object-fit: cover;
      box-shadow: 0 10px 20px rgba(89, 109, 173, .2);
    }
    h1 {
      margin: 0;
      color: #2d3e66;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: 1.35rem;
    }
    .header p {
      margin: .12rem 0 0;
      color: #60729a;
      font-size: .88rem;
      line-height: 1.4;
    }
    .hint,
    .error {
      border-radius: 12px;
      padding: .58rem .66rem;
      font-size: .83rem;
      margin-bottom: .66rem;
    }
    .hint {
      border: 1px solid #d8e4ff;
      background: #f3f7ff;
      color: #4d6088;
    }
    .error {
      border: 1px solid #f1cbda;
      background: #fff1f6;
      color: #a54767;
    }
    .form-grid {
      display: grid;
      gap: .56rem;
    }
    label {
      display: grid;
      gap: .26rem;
      color: #576a90;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .82rem;
    }
    input {
      border-radius: 11px;
      border: 1px solid #ccdaf5;
      padding: .62rem .7rem;
      background: #fff;
      color: #304361;
      font-size: .92rem;
    }
    input:focus {
      outline: none;
      border-color: #7e8df7;
      box-shadow: 0 0 0 3px rgba(126, 141, 247, .16);
    }
    .actions {
      margin-top: .16rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: .6rem;
    }
    .actions a {
      color: #4f64b0;
      text-decoration: none;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .84rem;
    }
    .actions a:hover {
      text-decoration: underline;
    }
    @media (max-width: 640px) {
      .actions {
        flex-direction: column-reverse;
        align-items: stretch;
      }
      .actions a {
        text-align: center;
      }
    }
  `
})
export class PortalAdminResetPasswordPageComponent implements OnInit {
  readonly loading = signal(false);
  readonly email = signal('');
  readonly token = signal('');
  readonly isLinkValid = computed(() => !!this.email() && !!this.token());

  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(PortalAdminAuthService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
  });

  ngOnInit(): void {
    this.route.queryParamMap.subscribe((params) => {
      this.email.set((params.get('email') ?? '').trim());
      this.token.set((params.get('token') ?? '').trim());
    });
  }

  submit(): void {
    if (!this.isLinkValid()) {
      this.toast.error('Reset link is invalid.');
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error('Please enter valid passwords.');
      return;
    }

    const payload = this.form.getRawValue();
    if (payload.newPassword !== payload.confirmPassword) {
      this.toast.error('Password and confirm password must match.');
      return;
    }

    this.loading.set(true);
    this.authService.resetPassword({
      email: this.email(),
      token: this.token(),
      newPassword: payload.newPassword,
      confirmPassword: payload.confirmPassword
    })
    .pipe(finalize(() => this.loading.set(false)))
    .subscribe({
      next: () => {
        this.toast.success('Password reset successful. Please login.');
        this.router.navigate(['/portal-admin/login']);
      },
      error: (error) => this.toast.error(extractApiError(error, 'Password reset failed.'))
    });
  }
}


