import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { extractApiError } from '../../../core/utils/api-error.util';
import { NAME_REGEX, PHONE_REGEX } from '../../../core/validators/input-patterns';
import { PortalApiService } from '../../services/portal-api.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppPageHeaderComponent],
  template: `
    <app-page-header title="Settings" subtitle="Tenant-specific business settings and password management"></app-page-header>

    <div class="grid">
      <app-card>
        <h3>Business Settings</h3>
        <form [formGroup]="settingsForm" class="form-grid" (ngSubmit)="saveSettings()">
          <label>
            Username
            <input formControlName="username" placeholder="Display name in sidebar profile">
            <small class="field-error" *ngIf="settingsForm.controls.username.touched && settingsForm.controls.username.hasError('pattern')">Username must not contain numbers.</small>
          </label>

          <div class="two-col">
            <label>
              Company Name
              <input formControlName="companyName">
              <small class="field-error" *ngIf="settingsForm.controls.companyName.touched && settingsForm.controls.companyName.hasError('pattern')">Company name must not contain numbers.</small>
            </label>
            <label>
              Company Email
              <input formControlName="companyEmail" type="email">
              <small class="field-error" *ngIf="settingsForm.controls.companyEmail.touched && settingsForm.controls.companyEmail.hasError('email')">Enter a valid company email address.</small>
            </label>
          </div>
          <div class="two-col">
            <label>
              Company Phone
              <input formControlName="companyPhone" type="tel">
              <small class="field-error" *ngIf="settingsForm.controls.companyPhone.touched && settingsForm.controls.companyPhone.hasError('pattern')">Company phone must contain only digits (optional leading +).</small>
            </label>
            <label>TIN Number <input formControlName="tinNumber"></label>
          </div>
          <label>Business Registration Number <input formControlName="businessRegistrationNumber"></label>

          <div class="logo-upload-field">
            <span class="field-label">Company Logo</span>
            <input
              #logoFileInput
              class="file-input"
              type="file"
              accept="image/png,image/jpeg,image/webp"
              (change)="onLogoFilePicked($event)">
            <button
              type="button"
              class="logo-dropzone"
              [class.dragging]="logoDragActive()"
              [class.uploading]="logoUploading()"
              (click)="openLogoPicker()"
              (dragover)="onLogoDragOver($event)"
              (dragleave)="onLogoDragLeave($event)"
              (drop)="onLogoDrop($event)">
              <ng-container *ngIf="logoPreviewUrl() as preview; else emptyLogoState">
                <img [src]="preview" alt="Company logo preview">
                <span>{{ logoUploading() ? 'Uploading logo...' : 'Drop another logo or click to replace' }}</span>
                <button type="button" class="logo-remove" (click)="removeLogo($event)">Remove logo</button>
              </ng-container>
              <ng-template #emptyLogoState>
                <span>{{ logoUploading() ? 'Uploading logo...' : 'Drag and drop company logo here' }}</span>
                <small>or click to browse (PNG, JPG, WEBP up to 5MB)</small>
              </ng-template>
            </button>
          </div>
          <label>
            Logo URL
            <input formControlName="logoUrl" type="url" placeholder="/logo-name.svg">
          </label>

          <div class="two-col">
            <label>Invoice Prefix <input formControlName="invoicePrefix"></label>
            <label>Delivery Note Prefix <input formControlName="deliveryNotePrefix"></label>
          </div>
          <div class="two-col">
            <label>Default Tax Rate <input type="number" step="0.01" formControlName="defaultTaxRate"></label>
            <label>Default Due Days <input type="number" formControlName="defaultDueDays"></label>
          </div>
          <label>Default Currency
            <select formControlName="defaultCurrency">
              <option value="MVR">MVR</option>
              <option value="USD">USD</option>
            </select>
          </label>

          <div class="bank-section">
            <h4>BML Accounts</h4>
            <div class="two-col">
              <label>BML MVR Account Name <input formControlName="bmlMvrAccountName"></label>
              <label>BML MVR Account Number <input formControlName="bmlMvrAccountNumber"></label>
            </div>
            <div class="two-col">
              <label>BML USD Account Name <input formControlName="bmlUsdAccountName"></label>
              <label>BML USD Account Number <input formControlName="bmlUsdAccountNumber"></label>
            </div>
          </div>

          <div class="bank-section">
            <h4>MIB Accounts</h4>
            <div class="two-col">
              <label>MIB MVR Account Name <input formControlName="mibMvrAccountName"></label>
              <label>MIB MVR Account Number <input formControlName="mibMvrAccountNumber"></label>
            </div>
            <div class="two-col">
              <label>MIB USD Account Name <input formControlName="mibUsdAccountName"></label>
              <label>MIB USD Account Number <input formControlName="mibUsdAccountNumber"></label>
            </div>
          </div>

          <div class="bank-section">
            <h4>Invoice Owner Signature</h4>
            <div class="two-col">
              <label>
                Invoice Owner Name
                <input formControlName="invoiceOwnerName">
                <small class="field-error" *ngIf="settingsForm.controls.invoiceOwnerName.touched && settingsForm.controls.invoiceOwnerName.hasError('pattern')">Owner name must not contain numbers.</small>
              </label>
              <label>Invoice Owner ID Card <input formControlName="invoiceOwnerIdCard"></label>
            </div>
          </div>

          <div class="actions">
            <app-button type="submit">Save Settings</app-button>
          </div>
        </form>
      </app-card>

      <app-card>
        <h3>Change Password</h3>
        <form [formGroup]="passwordForm" class="form-grid" (ngSubmit)="changePassword()">
          <label>Current Password <input type="password" formControlName="currentPassword"></label>
          <label>New Password <input type="password" formControlName="newPassword"></label>
          <label>Confirm Password <input type="password" formControlName="confirmPassword"></label>
          <div class="actions">
            <app-button type="submit">Change Password</app-button>
          </div>
        </form>
      </app-card>
    </div>
  `,
  styles: `
    .grid { display: grid; grid-template-columns: 1.2fr 1fr; gap: .9rem; }
    .form-grid { display: grid; gap: .78rem; }
    .two-col { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .75rem; }
    label { display: grid; gap: .2rem; font-size: .82rem; color: var(--text-muted); align-content: start; }
    .field-label {
      color: var(--text-muted);
      font-size: .82rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .logo-upload-field {
      display: grid;
      gap: .34rem;
    }
    .file-input {
      display: none;
    }
    .logo-dropzone {
      border: 1px dashed #b6c8ee;
      border-radius: 13px;
      background: linear-gradient(150deg, rgba(245, 248, 255, .95), rgba(235, 242, 255, .9));
      min-height: 132px;
      padding: .7rem;
      display: grid;
      place-items: center;
      gap: .4rem;
      text-align: center;
      color: #4f6490;
      cursor: pointer;
      transition: border-color .2s ease, background .2s ease, transform .2s ease;
    }
    .logo-dropzone:hover {
      border-color: #90a6e7;
      transform: translateY(-1px);
    }
    .logo-dropzone.dragging {
      border-color: #6f84f5;
      background: linear-gradient(150deg, rgba(228, 236, 255, .96), rgba(214, 228, 255, .9));
    }
    .logo-dropzone.uploading {
      cursor: progress;
      opacity: .78;
    }
    .logo-dropzone img {
      max-height: 72px;
      max-width: 100%;
      object-fit: contain;
      border-radius: 8px;
      border: 1px solid #d3ddf5;
      background: #fff;
      padding: .2rem;
    }
    .logo-dropzone span {
      font-size: .85rem;
      font-family: var(--font-heading);
      font-weight: 600;
      line-height: 1.35;
    }
    .logo-dropzone small {
      color: #667aa4;
      font-size: .74rem;
      line-height: 1.35;
    }
    .logo-remove {
      margin-top: .15rem;
      border: 1px solid #d9bfd0;
      border-radius: 9px;
      background: #fff;
      color: #a44a70;
      padding: .28rem .55rem;
      font-size: .76rem;
      font-family: var(--font-heading);
      font-weight: 600;
      cursor: pointer;
    }
    .logo-remove:hover {
      border-color: #c99ab5;
      background: #fff5fa;
    }
    .field-error { margin-top: .08rem; display: block; font-size: .75rem; line-height: 1.25; color: #bf2f46; }
    input,
    select {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .52rem .6rem;
      background: rgba(255,255,255,.92);
    }
    .bank-section {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .7rem;
      background: rgba(246, 250, 255, .6);
      display: grid;
      gap: .6rem;
    }
    .bank-section h4 {
      margin: 0;
      font-size: .9rem;
      color: var(--text-main);
    }
    .actions { display: flex; justify-content: flex-end; }
    @media (max-width: 980px) {
      .grid {
        grid-template-columns: 1fr;
      }
    }
    @media (max-width: 700px) {
      .two-col {
        grid-template-columns: 1fr;
      }
      .actions {
        justify-content: stretch;
      }
      .actions app-button {
        width: 100%;
      }
    }
  `
})
export class SettingsPageComponent implements OnInit {
  private static readonly maxLogoSizeBytes = 5 * 1024 * 1024;
  private static readonly allowedLogoMimeTypes = new Set(['image/png', 'image/jpeg', 'image/webp']);
  private static readonly defaultInvoiceLogoUrl = '/logo-name.svg';

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);

  readonly logoUploading = signal(false);
  readonly logoDragActive = signal(false);

  @ViewChild('logoFileInput')
  private logoFileInput?: ElementRef<HTMLInputElement>;

  readonly settingsForm = this.fb.nonNullable.group({
    username: ['', Validators.pattern(NAME_REGEX)],
    companyName: ['', [Validators.required, Validators.pattern(NAME_REGEX)]],
    companyEmail: ['', [Validators.required, Validators.email]],
    companyPhone: ['', [Validators.required, Validators.pattern(PHONE_REGEX)]],
    tinNumber: ['', Validators.required],
    businessRegistrationNumber: ['', Validators.required],
    invoicePrefix: ['', Validators.required],
    deliveryNotePrefix: ['', Validators.required],
    defaultTaxRate: [0.08, Validators.required],
    defaultDueDays: [7, Validators.required],
    defaultCurrency: ['MVR', Validators.required],
    bmlMvrAccountName: [''],
    bmlMvrAccountNumber: [''],
    bmlUsdAccountName: [''],
    bmlUsdAccountNumber: [''],
    mibMvrAccountName: [''],
    mibMvrAccountNumber: [''],
    mibUsdAccountName: [''],
    mibUsdAccountNumber: [''],
    invoiceOwnerName: ['', Validators.pattern(NAME_REGEX)],
    invoiceOwnerIdCard: [''],
    logoUrl: [SettingsPageComponent.defaultInvoiceLogoUrl, Validators.maxLength(400)]
  });

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required]
  });

  ngOnInit(): void {
    this.api.getSettings().subscribe({
      next: (settings) => {
        this.settingsForm.reset({
          ...settings,
          logoUrl: this.normalizePersistedLogoUrl(settings.logoUrl)
        });
        this.persistUsername(settings.username ?? '');
      },
      error: () => this.toast.error('Failed to load settings.')
    });
  }

  saveSettings(): void {
    if (this.settingsForm.invalid) {
      this.settingsForm.markAllAsTouched();
      this.toast.error('Please complete required settings fields.');
      return;
    }

    const logoUrl = this.settingsForm.controls.logoUrl.value.trim();
    if (logoUrl && !SettingsPageComponent.isPersistableLogoUrl(logoUrl)) {
      this.settingsForm.controls.logoUrl.markAsTouched();
      this.toast.error('Logo URL must be an HTTP/HTTPS URL, /uploads path, or root static path like /logo-name.svg.');
      return;
    }

    const payload = {
      ...this.settingsForm.getRawValue(),
      logoUrl,
      defaultTaxRate: Number(this.settingsForm.controls.defaultTaxRate.value),
      defaultDueDays: Number(this.settingsForm.controls.defaultDueDays.value)
    };

    this.api.updateSettings(payload).subscribe({
      next: (settings) => {
        this.settingsForm.reset({
          ...settings,
          logoUrl: this.normalizePersistedLogoUrl(settings.logoUrl)
        });
        this.persistUsername(settings.username ?? '');
        this.toast.success('Settings updated successfully.');
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to update settings.'))
    });
  }

  logoPreviewUrl(): string | null {
    const value = this.settingsForm.controls.logoUrl.value.trim();
    if (!value) {
      return null;
    }

    if (!SettingsPageComponent.isPreviewLogoUrl(value)) {
      return null;
    }

    if (value.toLowerCase().startsWith('data:image/') || value.toLowerCase().startsWith('blob:')) {
      return value;
    }

    const origin = typeof window !== 'undefined' ? window.location.origin : '';

    try {
      const parsed = new URL(value);
      if (parsed.pathname.startsWith('/uploads/') && origin && parsed.origin === origin) {
        return `${origin}${parsed.pathname}${parsed.search}`;
      }

      return parsed.toString();
    } catch {
      if (value.startsWith('/uploads/') && origin) {
        return `${origin}${value}`;
      }

      if (value.startsWith('/') && origin) {
        return `${origin}${value}`;
      }

      if (value.startsWith('uploads/') && origin) {
        return `${origin}/${value}`;
      }

      return null;
    }
  }

  openLogoPicker(): void {
    if (this.logoUploading()) {
      return;
    }

    this.logoFileInput?.nativeElement.click();
  }

  onLogoFilePicked(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.item(0);
    if (!file) {
      return;
    }

    this.uploadLogoFile(file);
  }

  onLogoDragOver(event: DragEvent): void {
    event.preventDefault();
    if (this.logoUploading()) {
      return;
    }

    this.logoDragActive.set(true);
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'copy';
    }
  }

  onLogoDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.logoDragActive.set(false);
  }

  onLogoDrop(event: DragEvent): void {
    event.preventDefault();
    this.logoDragActive.set(false);

    if (this.logoUploading()) {
      return;
    }

    const transfer = event.dataTransfer;
    if (!transfer) {
      return;
    }

    const file = transfer.files?.item(0);
    if (file) {
      this.uploadLogoFile(file);
      return;
    }

    const droppedText = transfer.getData('text/uri-list') || transfer.getData('text/plain');
    if (this.tryUseDroppedUrl(droppedText)) {
      return;
    }

    this.toast.error('Drop an image file or image URL.');
  }

  removeLogo(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (this.logoUploading()) {
      return;
    }

    this.settingsForm.controls.logoUrl.setValue(SettingsPageComponent.defaultInvoiceLogoUrl);
    this.settingsForm.controls.logoUrl.markAsDirty();
    this.resetLogoInput();
    this.toast.success('Logo reset to default invoice branding.');
  }

  changePassword(): void {
    if (this.passwordForm.invalid) {
      this.toast.error('Please complete password fields.');
      return;
    }

    const value = this.passwordForm.getRawValue();
    if (value.newPassword !== value.confirmPassword) {
      this.toast.error('New password and confirm password must match.');
      return;
    }

    this.api.changePassword(value).subscribe({
      next: () => {
        this.passwordForm.reset({ currentPassword: '', newPassword: '', confirmPassword: '' });
        this.toast.success('Password changed successfully.');
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to change password.'))
    });
  }

  private readError(error: unknown, fallback: string): string {
    return extractApiError(error, fallback);
  }

  private uploadLogoFile(file: File): void {
    if (file.size === 0) {
      this.toast.error('Selected logo file is empty.');
      this.resetLogoInput();
      return;
    }

    if (file.size > SettingsPageComponent.maxLogoSizeBytes) {
      this.toast.error('Logo file must be 5 MB or smaller.');
      this.resetLogoInput();
      return;
    }

    const mimeType = file.type.toLowerCase();
    if (mimeType && !SettingsPageComponent.allowedLogoMimeTypes.has(mimeType)) {
      this.toast.error('Supported logo formats are PNG, JPG, and WEBP.');
      this.resetLogoInput();
      return;
    }

    this.logoUploading.set(true);
    this.api.uploadSettingsLogo(file)
      .pipe(finalize(() => this.logoUploading.set(false)))
      .subscribe({
        next: (result) => {
          this.settingsForm.controls.logoUrl.setValue(result.url);
          this.settingsForm.controls.logoUrl.markAsDirty();
          this.toast.success('Logo uploaded.');
          this.resetLogoInput();
        },
        error: (error) => {
          this.toast.error(this.readError(error, 'Unable to upload logo.'));
          this.resetLogoInput();
        }
      });
  }

  private tryUseDroppedUrl(value: string): boolean {
    if (!value) {
      return false;
    }

    const candidate = value
      .split(/\r?\n/)
      .map((line) => line.trim())
      .find((line) => !!line && !line.startsWith('#'));

    if (!candidate) {
      return false;
    }

    try {
      const parsed = new URL(candidate);
      if (parsed.protocol !== 'https:' && parsed.protocol !== 'http:') {
        return false;
      }

      this.settingsForm.controls.logoUrl.setValue(parsed.toString());
      this.settingsForm.controls.logoUrl.markAsDirty();
      this.toast.success('Logo URL added.');
      return true;
    } catch {
      return false;
    }
  }

  private normalizePersistedLogoUrl(raw: string | null | undefined): string {
    const value = (raw ?? '').trim();
    if (!value) {
      return SettingsPageComponent.defaultInvoiceLogoUrl;
    }

    return SettingsPageComponent.isPersistableLogoUrl(value)
      ? value
      : SettingsPageComponent.defaultInvoiceLogoUrl;
  }

  private static isPreviewLogoUrl(raw: string): boolean {
    const value = raw.trim().toLowerCase();
    if (!value) {
      return false;
    }

    if (value.startsWith('data:image/') || value.startsWith('blob:')) {
      return true;
    }

    return SettingsPageComponent.isPersistableLogoUrl(raw);
  }

  private static isPersistableLogoUrl(raw: string): boolean {
    const value = raw.trim();
    if (!value) {
      return true;
    }

    if (value.startsWith('/uploads/') || value.startsWith('uploads/')) {
      return true;
    }

    if (value.startsWith('/')) {
      const normalizedPath = value.split('?', 2)[0].split('#', 2)[0];
      if (!/^\/[A-Za-z0-9/_\-.]+$/.test(normalizedPath)) {
        return false;
      }

      return !normalizedPath.includes('..');
    }

    try {
      const parsed = new URL(value);
      return parsed.protocol === 'http:' || parsed.protocol === 'https:';
    } catch {
      return false;
    }
  }

  private resetLogoInput(): void {
    if (this.logoFileInput) {
      this.logoFileInput.nativeElement.value = '';
    }
  }

  private persistUsername(raw: string): void {
    if (typeof window === 'undefined') {
      return;
    }

    const username = raw.trim();
    if (username) {
      window.localStorage.setItem('mydhathuru-username', username);
    } else {
      window.localStorage.removeItem('mydhathuru-username');
    }

    window.dispatchEvent(new CustomEvent<string>('mydhathuru-username-updated', { detail: username }));
  }
}



