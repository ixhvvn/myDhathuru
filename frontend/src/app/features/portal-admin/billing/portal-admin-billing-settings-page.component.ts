import { CommonModule } from '@angular/common';
import { Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { PortalAdminBillingSettings } from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';

@Component({
  selector: 'app-portal-admin-billing-settings-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppLoaderComponent],
  template: `
    <section class="page-head">
      <h1>Billing Settings</h1>
      <p>Configure global pricing, invoice numbering, payment details, and billing email defaults.</p>
    </section>

    <app-loader *ngIf="loading()"></app-loader>

    <app-card *ngIf="!loading()">
      <form [formGroup]="form" class="settings-grid" (ngSubmit)="save()">
        <h2>Default Pricing</h2>
        <label>Basic Software Fee <input type="number" step="0.01" formControlName="basicSoftwareFee"></label>
        <label>Vessel Fee <input type="number" step="0.01" formControlName="vesselFee"></label>
        <label>Staff Fee <input type="number" step="0.01" formControlName="staffFee"></label>
        <label>Default Currency
          <select formControlName="defaultCurrency">
            <option value="MVR">MVR</option>
            <option value="USD">USD</option>
          </select>
        </label>
        <label>Default Due Days <input type="number" formControlName="defaultDueDays"></label>

        <h2>Invoice Numbering</h2>
        <label>Invoice Prefix <input type="text" formControlName="invoicePrefix"></label>
        <label>Starting Sequence <input type="number" formControlName="startingSequenceNumber"></label>

        <h2>Payment Details</h2>
        <label>Account Name <input type="text" formControlName="accountName"></label>
        <label>Account Number <input type="text" formControlName="accountNumber"></label>
        <label>Bank Name <input type="text" formControlName="bankName"></label>
        <label>Branch <input type="text" formControlName="branch"></label>
        <label class="full">Payment Instructions <textarea rows="3" formControlName="paymentInstructions"></textarea></label>

        <h2>Invoice Notes</h2>
        <label class="full">Invoice Footer <textarea rows="2" formControlName="invoiceFooterNote"></textarea></label>
        <label class="full">Invoice Terms <textarea rows="3" formControlName="invoiceTerms"></textarea></label>
        <div class="full logo-upload-field">
          <span class="field-label">Logo Upload</span>
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
              <img [src]="preview" alt="Logo preview">
              <span>{{ logoUploading() ? 'Uploading logo...' : 'Drop another logo or click to replace' }}</span>
              <button type="button" class="logo-remove" (click)="removeLogo($event)">Remove logo</button>
            </ng-container>
            <ng-template #emptyLogoState>
              <span>{{ logoUploading() ? 'Uploading logo...' : 'Drag and drop logo here' }}</span>
              <small>or click to browse (PNG, JPG, WEBP up to 5MB)</small>
            </ng-template>
          </button>
        </div>
        <label>Logo URL <input type="url" formControlName="logoUrl" placeholder="/logo-name.svg"></label>

        <h2>Email Defaults</h2>
        <label>Email From Name <input type="text" formControlName="emailFromName"></label>
        <label>Reply-To Email <input type="email" formControlName="replyToEmail"></label>
        <label class="checkbox"><input type="checkbox" formControlName="autoGenerationEnabled"> Enable auto-generation toggle</label>
        <label class="checkbox"><input type="checkbox" formControlName="autoEmailEnabled"> Enable auto-email toggle</label>

        <div class="actions">
          <app-button type="submit" [loading]="saving()">Save Billing Settings</app-button>
        </div>
      </form>
    </app-card>
  `,
  styles: `
    .page-head h1 { margin: 0; font-family: var(--font-heading); font-size: 1.48rem; color: #2f4269; font-weight: 600; }
    .page-head p { margin: .3rem 0 0; color: #62749d; }
    .settings-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: .58rem .66rem;
      align-items: start;
    }
    .settings-grid > * { min-width: 0; }
    .settings-grid h2 {
      grid-column: 1 / -1;
      margin: .12rem 0 0;
      color: #33496f;
      font-family: var(--font-heading);
      font-size: 1rem;
      font-weight: 600;
    }
    label {
      display: grid;
      gap: .2rem;
      color: #5f739d;
      font-size: .78rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    label.full { grid-column: 1 / -1; }
    .logo-upload-field {
      display: grid;
      gap: .32rem;
    }
    .field-label {
      color: #5f739d;
      font-size: .78rem;
      font-family: var(--font-heading);
      font-weight: 600;
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
    .checkbox {
      display: flex;
      align-items: center;
      gap: .46rem;
      min-height: 40px;
      border: 1px solid #d8e2f9;
      border-radius: 11px;
      background: rgba(255,255,255,.88);
      padding: .5rem .58rem;
      color: #465d87;
      font-size: .82rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    input:not([type='checkbox']), select, textarea {
      border: 1px solid #ccdaf5;
      border-radius: 11px;
      background: #fff;
      color: #34496e;
      padding: .56rem .64rem;
      font-size: .86rem;
      min-height: 41px;
    }
    textarea { min-height: 76px; }
    input:not([type='checkbox']):focus, select:focus, textarea:focus {
      outline: none;
      border-color: #7f8df5;
      box-shadow: 0 0 0 3px rgba(126, 140, 245, .16);
    }
    .checkbox input[type='checkbox'] {
      width: 18px;
      height: 18px;
      min-height: 18px;
      margin: 0;
      padding: 0;
      flex: 0 0 auto;
      border-radius: 4px;
      border: 1px solid #b8c9ea;
      background: #fff;
      accent-color: #6f83f4;
      cursor: pointer;
      box-shadow: none;
    }
    .checkbox input[type='checkbox']:focus {
      outline: none;
      box-shadow: 0 0 0 3px rgba(126, 140, 245, .16);
    }
    .actions { grid-column: 1 / -1; margin-top: .2rem; display: flex; justify-content: flex-end; }
    @media (max-width: 1200px) {
      .settings-grid { grid-template-columns: 1fr; }
    }
  `
})
export class PortalAdminBillingSettingsPageComponent {
  private static readonly maxLogoSizeBytes = 5 * 1024 * 1024;
  private static readonly allowedLogoMimeTypes = new Set(['image/png', 'image/jpeg', 'image/webp']);
  private static readonly defaultInvoiceLogoUrl = '/logo-name.svg';

  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly logoUploading = signal(false);
  readonly logoDragActive = signal(false);

  @ViewChild('logoFileInput')
  private logoFileInput?: ElementRef<HTMLInputElement>;

  readonly form = this.fb.nonNullable.group({
    basicSoftwareFee: [2500, [Validators.required, Validators.min(0)]],
    vesselFee: [1000, [Validators.required, Validators.min(0)]],
    staffFee: [250, [Validators.required, Validators.min(0)]],
    invoicePrefix: ['ADM', [Validators.required, Validators.maxLength(20)]],
    startingSequenceNumber: [1, [Validators.required, Validators.min(1)]],
    defaultCurrency: ['MVR' as 'MVR' | 'USD', [Validators.required]],
    defaultDueDays: [14, [Validators.required, Validators.min(1), Validators.max(120)]],
    accountName: ['', [Validators.required, Validators.maxLength(200)]],
    accountNumber: ['', [Validators.required, Validators.maxLength(120)]],
    bankName: ['', [Validators.maxLength(120)]],
    branch: ['', [Validators.maxLength(120)]],
    paymentInstructions: ['', [Validators.maxLength(600)]],
    invoiceFooterNote: ['', [Validators.maxLength(600)]],
    invoiceTerms: ['', [Validators.maxLength(1200)]],
    logoUrl: [PortalAdminBillingSettingsPageComponent.defaultInvoiceLogoUrl, [Validators.maxLength(400)]],
    emailFromName: ['', [Validators.maxLength(120)]],
    replyToEmail: ['', [Validators.email, Validators.maxLength(200)]],
    autoGenerationEnabled: [false],
    autoEmailEnabled: [false]
  });

  constructor() {
    this.load();
  }

  logoPreviewUrl(): string | null {
    const value = this.form.controls.logoUrl.value.trim();
    if (!value) {
      return null;
    }

    if (!PortalAdminBillingSettingsPageComponent.isPreviewLogoUrl(value)) {
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

    this.form.controls.logoUrl.setValue(PortalAdminBillingSettingsPageComponent.defaultInvoiceLogoUrl);
    this.form.controls.logoUrl.markAsDirty();
    this.resetLogoInput();
    this.toast.success('Logo reset to default invoice branding.');
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error('Please correct the billing settings form.');
      return;
    }

    const logoUrl = this.form.controls.logoUrl.value.trim();
    if (logoUrl && !PortalAdminBillingSettingsPageComponent.isPersistableLogoUrl(logoUrl)) {
      this.form.controls.logoUrl.markAsTouched();
      this.toast.error('Logo URL must be an HTTP/HTTPS URL, /uploads path, or root static path like /logo-name.svg.');
      return;
    }

    const payload: PortalAdminBillingSettings = {
      ...this.form.getRawValue(),
      logoUrl
    };
    this.saving.set(true);
    this.api.updateBillingSettings(payload)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (result) => {
          this.patchForm(result);
          this.toast.success('Billing settings updated.');
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to update billing settings.'))
      });
  }

  private load(): void {
    this.loading.set(true);
    this.api.getBillingSettings()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => this.patchForm(result),
        error: (error) => this.toast.error(extractApiError(error, 'Unable to load billing settings.'))
      });
  }

  private patchForm(settings: PortalAdminBillingSettings): void {
    this.form.reset({
      basicSoftwareFee: settings.basicSoftwareFee,
      vesselFee: settings.vesselFee,
      staffFee: settings.staffFee,
      invoicePrefix: settings.invoicePrefix,
      startingSequenceNumber: settings.startingSequenceNumber,
      defaultCurrency: settings.defaultCurrency,
      defaultDueDays: settings.defaultDueDays,
      accountName: settings.accountName,
      accountNumber: settings.accountNumber,
      bankName: settings.bankName ?? '',
      branch: settings.branch ?? '',
      paymentInstructions: settings.paymentInstructions ?? '',
      invoiceFooterNote: settings.invoiceFooterNote ?? '',
      invoiceTerms: settings.invoiceTerms ?? '',
      logoUrl: this.normalizePersistedLogoUrl(settings.logoUrl),
      emailFromName: settings.emailFromName ?? '',
      replyToEmail: settings.replyToEmail ?? '',
      autoGenerationEnabled: settings.autoGenerationEnabled,
      autoEmailEnabled: settings.autoEmailEnabled
    });
  }

  private uploadLogoFile(file: File): void {
    if (file.size === 0) {
      this.toast.error('Selected logo file is empty.');
      this.resetLogoInput();
      return;
    }

    if (file.size > PortalAdminBillingSettingsPageComponent.maxLogoSizeBytes) {
      this.toast.error('Logo file must be 5 MB or smaller.');
      this.resetLogoInput();
      return;
    }

    const mimeType = file.type.toLowerCase();
    if (mimeType && !PortalAdminBillingSettingsPageComponent.allowedLogoMimeTypes.has(mimeType)) {
      this.toast.error('Supported logo formats are PNG, JPG, and WEBP.');
      this.resetLogoInput();
      return;
    }

    this.logoUploading.set(true);
    this.api.uploadBillingLogo(file)
      .pipe(finalize(() => this.logoUploading.set(false)))
      .subscribe({
        next: (result) => {
          this.form.controls.logoUrl.setValue(result.url);
          this.form.controls.logoUrl.markAsDirty();
          this.toast.success('Logo uploaded.');
          this.resetLogoInput();
        },
        error: (error) => {
          this.toast.error(extractApiError(error, 'Unable to upload logo.'));
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

      this.form.controls.logoUrl.setValue(parsed.toString());
      this.form.controls.logoUrl.markAsDirty();
      this.toast.success('Logo URL added.');
      return true;
    } catch {
      return false;
    }
  }

  private normalizePersistedLogoUrl(raw: string | null | undefined): string {
    const value = (raw ?? '').trim();
    if (!value) {
      return PortalAdminBillingSettingsPageComponent.defaultInvoiceLogoUrl;
    }

    return PortalAdminBillingSettingsPageComponent.isPersistableLogoUrl(value)
      ? value
      : PortalAdminBillingSettingsPageComponent.defaultInvoiceLogoUrl;
  }

  private static isPreviewLogoUrl(raw: string): boolean {
    const value = raw.trim().toLowerCase();
    if (!value) {
      return false;
    }

    if (value.startsWith('data:image/') || value.startsWith('blob:')) {
      return true;
    }

    return PortalAdminBillingSettingsPageComponent.isPersistableLogoUrl(raw);
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
}



