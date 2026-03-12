import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { PortalAdminAuthService } from '../../../core/services/portal-admin-auth.service';

@Component({
  selector: 'app-portal-admin-settings-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AppButtonComponent, AppCardComponent],
  template: `
    <section class="page-head">
      <h1>Portal Admin Settings</h1>
      <p>Manage super admin account security settings.</p>
    </section>

    <app-card class="settings-card">
      <h2>Change Password</h2>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>
          Current Password
          <input type="password" formControlName="currentPassword" autocomplete="current-password">
        </label>
        <label>
          New Password
          <input type="password" formControlName="newPassword" autocomplete="new-password">
        </label>
        <small class="error" *ngIf="form.controls.newPassword.touched && form.controls.newPassword.hasError('minlength')">
          New password must be at least 8 characters.
        </small>
        <label>
          Confirm Password
          <input type="password" formControlName="confirmPassword" autocomplete="new-password">
        </label>
        <small class="error" *ngIf="form.controls.confirmPassword.touched && form.controls.confirmPassword.value !== form.controls.newPassword.value">
          Password and confirm password must match.
        </small>
        <div class="actions">
          <app-button type="submit" [loading]="loading()">Update Password</app-button>
        </div>
      </form>
    </app-card>
  `,
  styles: `
    .page-head h1 { margin: 0; font-family: var(--font-heading); color: #2f4269; font-size: 1.45rem; font-weight: 600; }
    .page-head p { margin: .32rem 0 0; color: #61739a; }
    .settings-card {
      margin-top: .78rem;
      max-width: 620px;
      --card-padding: .9rem;
    }
    h2 {
      margin: 0 0 .65rem;
      color: #30466f;
      font-family: var(--font-heading);
      font-size: 1.08rem;
      font-weight: 600;
    }
    form {
      display: grid;
      gap: .56rem;
    }
    label {
      display: grid;
      gap: .22rem;
      color: #5f739d;
      font-size: .78rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    input {
      border: 1px solid #ccdaf5;
      border-radius: 10px;
      background: #fff;
      padding: .56rem .65rem;
      font-size: .87rem;
      color: #34486d;
    }
    input:focus {
      outline: none;
      border-color: #7e8df7;
      box-shadow: 0 0 0 3px rgba(126,141,247,.16);
    }
    .error {
      margin-top: -.3rem;
      color: #c04e72;
      font-size: .76rem;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      margin-top: .14rem;
    }
  `
})
export class PortalAdminSettingsPageComponent {
  private readonly authService = inject(PortalAdminAuthService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly form = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload = this.form.getRawValue();
    if (payload.newPassword !== payload.confirmPassword) {
      this.toast.error('Password and confirm password must match.');
      return;
    }

    this.loading.set(true);
    this.authService.changePassword(payload)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Portal admin password updated.');
          this.form.reset({
            currentPassword: '',
            newPassword: '',
            confirmPassword: ''
          });
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to update password.'))
      });
  }
}

