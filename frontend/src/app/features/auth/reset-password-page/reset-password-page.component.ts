import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';

@Component({
  selector: 'app-reset-password-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AppButtonComponent, AppCardComponent],
  template: `
    <div class="reset-page">
      <span class="blob blob-a"></span>
      <span class="blob blob-b"></span>
      <span class="blob blob-c"></span>

      <app-card class="reset-card">
        <div class="brand">
          <img src="/newlogo.png" alt="myDhathuru logo">
          <div>
            <h1>Reset Password</h1>
            <p>Create a new password for your account.</p>
          </div>
        </div>

        <div class="hint" *ngIf="isLinkValid()">
          Reset requested for <strong>{{ email() }}</strong>. This link expires in 30 minutes.
        </div>

        <div class="error" *ngIf="!isLinkValid()">
          This reset link is invalid or incomplete. Please request a new one from the login page.
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
            <a routerLink="/login" class="back-link">Back to login</a>
            <app-button type="submit" [loading]="loading()">Reset Password</app-button>
          </div>
        </form>

        <div class="actions" *ngIf="!isLinkValid()">
          <a routerLink="/login" class="back-link">Back to login</a>
        </div>
      </app-card>
    </div>
  `,
  styles: `
    .reset-page {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 1.1rem;
      position: relative;
      overflow: hidden;
      background:
        radial-gradient(circle at 14% 16%, #efe8ff 0%, rgba(239, 232, 255, .35) 34%, transparent 58%),
        radial-gradient(circle at 88% 15%, #dbf5ff 0%, rgba(219, 245, 255, .34) 32%, transparent 56%),
        linear-gradient(180deg, #f7f9ff 0%, #f4f7ff 100%);
    }
    .blob {
      position: absolute;
      border-radius: 50%;
      filter: blur(3px);
      opacity: .5;
      pointer-events: none;
      animation: drift 16s ease-in-out infinite;
    }
    .blob-a {
      width: 280px;
      height: 280px;
      background: radial-gradient(circle, rgba(140, 154, 253, .35), rgba(140, 154, 253, 0) 70%);
      top: -80px;
      right: -70px;
    }
    .blob-b {
      width: 260px;
      height: 260px;
      background: radial-gradient(circle, rgba(120, 220, 228, .3), rgba(120, 220, 228, 0) 70%);
      bottom: -90px;
      left: -70px;
      animation-delay: 3s;
    }
    .blob-c {
      width: 200px;
      height: 200px;
      background: radial-gradient(circle, rgba(255, 195, 222, .32), rgba(255, 195, 222, 0) 70%);
      top: 42%;
      left: 64%;
      animation-delay: 6s;
    }
    .reset-card {
      width: min(560px, 100%);
      position: relative;
      z-index: 2;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: .75rem;
      margin-bottom: .7rem;
    }
    .brand img {
      width: 52px;
      height: 52px;
      border-radius: 14px;
      object-fit: cover;
      border: 1px solid #dbe4f8;
      box-shadow: 0 8px 16px rgba(86, 108, 178, .18);
      background: #fff;
    }
    h1 {
      margin: 0;
      font-family: var(--font-heading);
      font-size: 1.7rem;
      color: #2f426e;
    }
    .brand p {
      margin: .1rem 0 0;
      color: var(--text-muted);
      font-size: .9rem;
    }
    .hint,
    .error {
      margin-bottom: .8rem;
      border-radius: 12px;
      padding: .58rem .68rem;
      font-size: .84rem;
      line-height: 1.4;
    }
    .hint {
      border: 1px solid #d8e4ff;
      background: rgba(241, 246, 255, .9);
      color: #4a5d87;
    }
    .error {
      border: 1px solid #f1cad5;
      background: rgba(255, 237, 242, .94);
      color: #9f4560;
    }
    .form-grid {
      display: grid;
      gap: .7rem;
    }
    label {
      display: grid;
      gap: .26rem;
      color: var(--text-muted);
      font-size: .84rem;
    }
    input {
      border: 1px solid var(--border-soft);
      border-radius: 11px;
      padding: .6rem .68rem;
      background: rgba(255,255,255,.95);
    }
    .actions {
      margin-top: .1rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: .6rem;
    }
    .back-link {
      color: #5b6d97;
      text-decoration: none;
      font-weight: 600;
      font-size: .85rem;
    }
    .back-link:hover {
      color: #3f5283;
      text-decoration: underline;
    }
    @media (max-width: 640px) {
      .reset-page {
        padding: .7rem;
      }
      .brand {
        align-items: flex-start;
      }
      .brand img {
        width: 46px;
        height: 46px;
      }
      h1 {
        font-size: 1.4rem;
      }
      .actions {
        flex-wrap: wrap;
        justify-content: stretch;
      }
      .actions app-button {
        display: block;
        width: 100%;
      }
      .back-link {
        width: 100%;
      }
    }
    @keyframes drift {
      0%, 100% {
        transform: translate3d(0, 0, 0) scale(1);
      }
      50% {
        transform: translate3d(0, -10px, 0) scale(1.05);
      }
    }
  `
})
export class ResetPasswordPageComponent implements OnInit {
  readonly loading = signal(false);
  readonly email = signal('');
  readonly token = signal('');

  readonly isLinkValid = computed(() => !!this.email() && !!this.token());

  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
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
      this.toast.error('Please enter valid password values.');
      return;
    }

    const raw = this.form.getRawValue();
    if (raw.newPassword !== raw.confirmPassword) {
      this.toast.error('Password and confirm password must match.');
      return;
    }

    this.loading.set(true);
    this.authService.resetPassword({
      email: this.email(),
      token: this.token(),
      newPassword: raw.newPassword,
      confirmPassword: raw.confirmPassword
    })
    .pipe(finalize(() => this.loading.set(false)))
    .subscribe({
      next: () => {
        this.toast.success('Password reset successful. Please login.');
        this.router.navigate(['/login']);
      },
      error: (error) => this.toast.error(this.readError(error, 'Reset password failed.'))
    });
  }

  private readError(error: unknown, fallback: string): string {
    const apiError = error as { error?: { message?: string; errors?: string[] } };
    return apiError?.error?.errors?.[0] ?? apiError?.error?.message ?? fallback;
  }
}

