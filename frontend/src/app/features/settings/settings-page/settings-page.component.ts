import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AuthService } from '../../../core/services/auth.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { NAME_REGEX, PHONE_REGEX } from '../../../core/validators/input-patterns';
import { PortalApiService } from '../../services/portal-api.service';
import { ToastService } from '../../../core/services/toast.service';

type SettingsImageKind = 'logo' | 'stamp' | 'signature';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppPageHeaderComponent],
  template: `
    <app-page-header title="Settings" subtitle="Tenant-specific business settings and password management"></app-page-header>

    <div class="grid">
      <app-card>
        <h3>Business Settings</h3>
        <p class="access-note" *ngIf="!isAdmin()">Only admin users can update business settings.</p>
        <form [formGroup]="settingsForm" class="form-grid" (ngSubmit)="saveSettings()" novalidate>
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

          <div class="asset-upload-field">
            <span class="field-label">Company Logo</span>
            <input
              #logoFileInput
              class="file-input"
              type="file"
              accept="image/png,image/jpeg,image/webp"
              (change)="onImageFilePicked('logo', $event)">
            <button
              type="button"
              class="asset-dropzone"
              [class.dragging]="imageState.logo.dragging()"
              [class.uploading]="imageState.logo.uploading()"
              (click)="openImagePicker('logo')"
              (dragover)="onImageDragOver('logo', $event)"
              (dragleave)="onImageDragLeave('logo', $event)"
              (drop)="onImageDrop('logo', $event)">
              <ng-container *ngIf="imagePreviewUrl('logo') as preview; else emptyLogoState">
                <img [src]="preview" alt="Company logo preview">
                <span>{{ imageState.logo.uploading() ? 'Uploading logo...' : 'Drop another logo or click to replace' }}</span>
                <small>Used on invoices, reports, and document exports.</small>
                <button type="button" class="asset-remove" (click)="removeImage('logo', $event)">Remove logo</button>
              </ng-container>
              <ng-template #emptyLogoState>
                <span>{{ imageState.logo.uploading() ? 'Uploading logo...' : 'Drag and drop company logo here' }}</span>
                <small>or click to browse (PNG, JPG, WEBP up to 5MB)</small>
              </ng-template>
            </button>
          </div>
          <label>
            Logo URL
            <input formControlName="logoUrl" type="text" placeholder="/logo-name.svg">
          </label>

          <div class="asset-panel">
            <div class="asset-panel-head">
              <h4>BPT Stamp & Signature</h4>
              <p>Upload the company stamp and authorized signature to place them automatically on BPT statement PDFs.</p>
            </div>
            <div class="two-col asset-grid">
              <div class="asset-upload-field">
                <span class="field-label">Company Stamp</span>
                <input
                  #stampFileInput
                  class="file-input"
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  (change)="onImageFilePicked('stamp', $event)">
                <button
                  type="button"
                  class="asset-dropzone"
                  [class.dragging]="imageState.stamp.dragging()"
                  [class.uploading]="imageState.stamp.uploading()"
                  (click)="openImagePicker('stamp')"
                  (dragover)="onImageDragOver('stamp', $event)"
                  (dragleave)="onImageDragLeave('stamp', $event)"
                  (drop)="onImageDrop('stamp', $event)">
                  <ng-container *ngIf="imagePreviewUrl('stamp') as preview; else emptyStampState">
                    <img [src]="preview" alt="Company stamp preview">
                    <span>{{ imageState.stamp.uploading() ? 'Uploading company stamp...' : 'Drop another stamp or click to replace' }}</span>
                    <small>Shown in the BPT statement approval section.</small>
                    <button type="button" class="asset-remove" (click)="removeImage('stamp', $event)">Remove stamp</button>
                  </ng-container>
                  <ng-template #emptyStampState>
                    <span>{{ imageState.stamp.uploading() ? 'Uploading company stamp...' : 'Drag and drop company stamp here' }}</span>
                    <small>or click to browse (PNG, JPG, WEBP up to 5MB)</small>
                  </ng-template>
                </button>
              </div>

              <div class="asset-upload-field">
                <span class="field-label">Authorized Signature</span>
                <input
                  #signatureFileInput
                  class="file-input"
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  (change)="onImageFilePicked('signature', $event)">
                <button
                  type="button"
                  class="asset-dropzone"
                  [class.dragging]="imageState.signature.dragging()"
                  [class.uploading]="imageState.signature.uploading()"
                  (click)="openImagePicker('signature')"
                  (dragover)="onImageDragOver('signature', $event)"
                  (dragleave)="onImageDragLeave('signature', $event)"
                  (drop)="onImageDrop('signature', $event)">
                  <ng-container *ngIf="imagePreviewUrl('signature') as preview; else emptySignatureState">
                    <img [src]="preview" alt="Authorized signature preview">
                    <span>{{ imageState.signature.uploading() ? 'Uploading signature...' : 'Drop another signature or click to replace' }}</span>
                    <small>Shown next to the company stamp in the BPT statement.</small>
                    <button type="button" class="asset-remove" (click)="removeImage('signature', $event)">Remove signature</button>
                  </ng-container>
                  <ng-template #emptySignatureState>
                    <span>{{ imageState.signature.uploading() ? 'Uploading signature...' : 'Drag and drop authorized signature here' }}</span>
                    <small>or click to browse (PNG, JPG, WEBP up to 5MB)</small>
                  </ng-template>
                </button>
              </div>
            </div>
          </div>

          <div class="prefix-grid">
            <label>Invoice Prefix <input formControlName="invoicePrefix"></label>
            <label>Delivery Note Prefix <input formControlName="deliveryNotePrefix"></label>
            <label>Quote Prefix <input formControlName="quotePrefix"></label>
            <label>PO Prefix <input formControlName="purchaseOrderPrefix"></label>
            <label>Received Invoice Prefix <input formControlName="receivedInvoicePrefix"></label>
            <label>Payment Voucher Prefix <input formControlName="paymentVoucherPrefix"></label>
            <label>Rent Prefix <input formControlName="rentEntryPrefix"></label>
            <label>Warning Form Prefix <input formControlName="warningFormPrefix"></label>
            <label>Statement Prefix <input formControlName="statementPrefix"></label>
            <label>Salary Slip Prefix <input formControlName="salarySlipPrefix"></label>
          </div>
          <div class="two-col">
            <label>
              Tax Applicable
              <select formControlName="isTaxApplicable">
                <option [ngValue]="true">Yes</option>
                <option [ngValue]="false">No</option>
              </select>
            </label>
            <label>Default Due Days <input type="number" formControlName="defaultDueDays"></label>
          </div>
          <div class="two-col">
            <ng-container *ngIf="settingsForm.controls.isTaxApplicable.value; else taxDisabledState">
              <label>Default Tax Rate <input type="number" step="0.01" formControlName="defaultTaxRate"></label>
            </ng-container>
            <ng-template #taxDisabledState>
              <div class="setting-callout">
                <strong>Tax is disabled</strong>
                <span>Invoices, reports, exports, and stored invoice tax values will stay tax-free.</span>
              </div>
            </ng-template>
            <label>Default Currency
              <select formControlName="defaultCurrency">
                <option value="MVR">MVR</option>
                <option value="USD">USD</option>
              </select>
            </label>
          </div>
          <div class="two-col">
            <label>Taxable Activity Number <input formControlName="taxableActivityNumber"></label>
            <label class="checkbox-inline">
              <span>Input Tax Claim Enabled</span>
              <select formControlName="isInputTaxClaimEnabled">
                <option [ngValue]="true">Yes</option>
                <option [ngValue]="false">No</option>
              </select>
            </label>
          </div>

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
            <app-button type="submit" [disabled]="!isAdmin()">Save Settings</app-button>
          </div>
        </form>
      </app-card>

      <app-card>
        <h3>Change Password</h3>
        <form [formGroup]="passwordForm" class="form-grid" (ngSubmit)="changePassword()" novalidate>
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
    .three-col { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .75rem; }
    .prefix-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .75rem; }
    label { display: grid; gap: .2rem; font-size: .82rem; color: var(--text-muted); align-content: start; }
    .checkbox-inline {
      display: grid;
      gap: .2rem;
    }
    .field-label {
      color: var(--text-muted);
      font-size: .82rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .asset-upload-field {
      display: grid;
      gap: .34rem;
    }
    .file-input {
      display: none;
    }
    .asset-panel {
      border: 1px solid var(--border-soft);
      border-radius: 14px;
      padding: .8rem;
      background: rgba(246, 250, 255, .55);
      display: grid;
      gap: .75rem;
    }
    .asset-panel-head {
      display: grid;
      gap: .22rem;
    }
    .asset-panel-head h4 {
      margin: 0;
      font-size: .92rem;
      color: var(--text-main);
    }
    .asset-panel-head p {
      margin: 0;
      font-size: .77rem;
      line-height: 1.4;
      color: var(--text-muted);
    }
    .asset-grid {
      align-items: start;
    }
    .asset-dropzone {
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
    .asset-dropzone:hover {
      border-color: #90a6e7;
      transform: translateY(-1px);
    }
    .asset-dropzone.dragging {
      border-color: #6f84f5;
      background: linear-gradient(150deg, rgba(228, 236, 255, .96), rgba(214, 228, 255, .9));
    }
    .asset-dropzone.uploading {
      cursor: progress;
      opacity: .78;
    }
    .asset-dropzone img {
      max-height: 72px;
      max-width: 100%;
      object-fit: contain;
      border-radius: 8px;
      border: 1px solid #d3ddf5;
      background: #fff;
      padding: .2rem;
    }
    .asset-dropzone span {
      font-size: .85rem;
      font-family: var(--font-heading);
      font-weight: 600;
      line-height: 1.35;
    }
    .asset-dropzone small {
      color: #667aa4;
      font-size: .74rem;
      line-height: 1.35;
    }
    .asset-remove {
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
    .asset-remove:hover {
      border-color: #c99ab5;
      background: #fff5fa;
    }
    .field-error { margin-top: .08rem; display: block; font-size: .75rem; line-height: 1.25; color: #bf2f46; }
    .access-note {
      margin: 0 0 .8rem;
      padding: .6rem .7rem;
      border-radius: 12px;
      border: 1px solid rgba(214, 169, 82, .35);
      background: rgba(255, 247, 226, .78);
      color: #775112;
      font-size: .8rem;
      line-height: 1.4;
    }
    .setting-callout {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .68rem .74rem;
      background: rgba(246, 250, 255, .7);
      display: grid;
      gap: .2rem;
      align-content: start;
    }
    .setting-callout strong {
      color: var(--text-main);
      font-size: .84rem;
      font-family: var(--font-heading);
    }
    .setting-callout span {
      color: var(--text-muted);
      font-size: .77rem;
      line-height: 1.4;
    }
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
      .three-col {
        grid-template-columns: 1fr;
      }
      .prefix-grid {
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
  private static readonly maxImageSizeBytes = 5 * 1024 * 1024;
  private static readonly allowedImageMimeTypes = new Set(['image/png', 'image/jpeg', 'image/webp']);
  private static readonly defaultInvoiceLogoUrl = '/logo-name.svg';

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly imageState = {
    logo: { uploading: signal(false), dragging: signal(false) },
    stamp: { uploading: signal(false), dragging: signal(false) },
    signature: { uploading: signal(false), dragging: signal(false) }
  };

  @ViewChild('logoFileInput')
  private logoFileInput?: ElementRef<HTMLInputElement>;

  @ViewChild('stampFileInput')
  private stampFileInput?: ElementRef<HTMLInputElement>;

  @ViewChild('signatureFileInput')
  private signatureFileInput?: ElementRef<HTMLInputElement>;

  readonly settingsForm = this.fb.nonNullable.group({
    username: ['', Validators.pattern(NAME_REGEX)],
    companyName: ['', [Validators.required, Validators.pattern(NAME_REGEX)]],
    companyEmail: ['', [Validators.required, Validators.email]],
    companyPhone: ['', [Validators.required, Validators.pattern(PHONE_REGEX)]],
    tinNumber: ['', Validators.required],
    businessRegistrationNumber: ['', Validators.required],
    invoicePrefix: ['', Validators.required],
    deliveryNotePrefix: ['', Validators.required],
    quotePrefix: ['', Validators.required],
    purchaseOrderPrefix: ['', Validators.required],
    receivedInvoicePrefix: ['', Validators.required],
    paymentVoucherPrefix: ['', Validators.required],
    rentEntryPrefix: ['', Validators.required],
    warningFormPrefix: ['', Validators.required],
    statementPrefix: ['', Validators.required],
    salarySlipPrefix: ['', Validators.required],
    isTaxApplicable: [true, Validators.required],
    defaultTaxRate: [0.08, Validators.required],
    defaultDueDays: [7, Validators.required],
    defaultCurrency: ['MVR', Validators.required],
    taxableActivityNumber: [''],
    isInputTaxClaimEnabled: [true, Validators.required],
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
    logoUrl: [SettingsPageComponent.defaultInvoiceLogoUrl, Validators.maxLength(400)],
    companyStampUrl: ['', Validators.maxLength(400)],
    companySignatureUrl: ['', Validators.maxLength(400)]
  });

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required]
  });

  ngOnInit(): void {
    this.settingsForm.controls.isTaxApplicable.valueChanges.subscribe((isTaxApplicable) => {
      this.syncTaxConfiguration(isTaxApplicable);
    });

    this.api.getSettings().subscribe({
      next: (settings) => {
        this.settingsForm.reset({
          ...settings,
          logoUrl: this.normalizePersistedLogoUrl(settings.logoUrl),
          companyStampUrl: this.normalizePersistedOptionalImageUrl(settings.companyStampUrl),
          companySignatureUrl: this.normalizePersistedOptionalImageUrl(settings.companySignatureUrl)
        });
        this.syncTaxConfiguration(settings.isTaxApplicable);
        this.persistUsername(settings.username ?? '');
      },
      error: () => this.toast.error('Failed to load settings.')
    });
  }

  isAdmin(): boolean {
    return this.auth.user()?.role === 'Admin';
  }

  saveSettings(): void {
    if (!this.isAdmin()) {
      this.toast.error('Only admin users can update settings.');
      return;
    }

    if (this.settingsForm.invalid) {
      this.settingsForm.markAllAsTouched();
      this.toast.error('Please complete required settings fields.');
      return;
    }

    const logoUrl = this.settingsForm.controls.logoUrl.value.trim();
    const companyStampUrl = this.settingsForm.controls.companyStampUrl.value.trim();
    const companySignatureUrl = this.settingsForm.controls.companySignatureUrl.value.trim();
    if (!this.validateImageUrl('logo', logoUrl)
      || !this.validateImageUrl('stamp', companyStampUrl)
      || !this.validateImageUrl('signature', companySignatureUrl)) {
      return;
    }

    const payload = {
      ...this.settingsForm.getRawValue(),
      logoUrl,
      companyStampUrl,
      companySignatureUrl,
      defaultTaxRate: this.settingsForm.controls.isTaxApplicable.value
        ? Number(this.settingsForm.controls.defaultTaxRate.value)
        : 0,
      defaultDueDays: Number(this.settingsForm.controls.defaultDueDays.value)
    };

    this.api.updateSettings(payload).subscribe({
      next: (settings) => {
        this.settingsForm.reset({
          ...settings,
          logoUrl: this.normalizePersistedLogoUrl(settings.logoUrl),
          companyStampUrl: this.normalizePersistedOptionalImageUrl(settings.companyStampUrl),
          companySignatureUrl: this.normalizePersistedOptionalImageUrl(settings.companySignatureUrl)
        });
        this.syncTaxConfiguration(settings.isTaxApplicable);
        this.persistUsername(settings.username ?? '');
        this.toast.success('Settings updated successfully.');
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to update settings.'))
    });
  }

  imagePreviewUrl(kind: SettingsImageKind): string | null {
    const value = this.getImageControl(kind).value.trim();
    return this.resolvePreviewUrl(value);
  }

  openImagePicker(kind: SettingsImageKind): void {
    if (this.imageState[kind].uploading()) {
      return;
    }

    this.getImageInput(kind)?.nativeElement.click();
  }

  onImageFilePicked(kind: SettingsImageKind, event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.item(0);
    if (!file) {
      return;
    }

    this.uploadImageFile(kind, file);
  }

  onImageDragOver(kind: SettingsImageKind, event: DragEvent): void {
    event.preventDefault();
    if (this.imageState[kind].uploading()) {
      return;
    }

    this.imageState[kind].dragging.set(true);
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'copy';
    }
  }

  onImageDragLeave(kind: SettingsImageKind, event: DragEvent): void {
    event.preventDefault();
    this.imageState[kind].dragging.set(false);
  }

  onImageDrop(kind: SettingsImageKind, event: DragEvent): void {
    event.preventDefault();
    this.imageState[kind].dragging.set(false);

    if (this.imageState[kind].uploading()) {
      return;
    }

    const transfer = event.dataTransfer;
    if (!transfer) {
      return;
    }

    const file = transfer.files?.item(0);
    if (file) {
      this.uploadImageFile(kind, file);
      return;
    }

    const droppedText = transfer.getData('text/uri-list') || transfer.getData('text/plain');
    if (this.tryUseDroppedUrl(kind, droppedText)) {
      return;
    }

    this.toast.error(`Drop a ${this.getImageLabel(kind).toLowerCase()} image file or image URL.`);
  }

  removeImage(kind: SettingsImageKind, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (this.imageState[kind].uploading()) {
      return;
    }

    this.getImageControl(kind).setValue(kind === 'logo' ? SettingsPageComponent.defaultInvoiceLogoUrl : '');
    this.getImageControl(kind).markAsDirty();
    this.resetImageInput(kind);
    this.toast.success(this.getImageRemovedMessage(kind));
  }

  private resolvePreviewUrl(value: string): string | null {
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

  private uploadImageFile(kind: SettingsImageKind, file: File): void {
    if (file.size === 0) {
      this.toast.error(`Selected ${this.getImageLabel(kind).toLowerCase()} file is empty.`);
      this.resetImageInput(kind);
      return;
    }

    if (file.size > SettingsPageComponent.maxImageSizeBytes) {
      this.toast.error(`${this.getImageLabel(kind)} file must be 5 MB or smaller.`);
      this.resetImageInput(kind);
      return;
    }

    const mimeType = file.type.toLowerCase();
    if (mimeType && !SettingsPageComponent.allowedImageMimeTypes.has(mimeType)) {
      this.toast.error(`Supported ${this.getImageLabel(kind).toLowerCase()} formats are PNG, JPG, and WEBP.`);
      this.resetImageInput(kind);
      return;
    }

    this.imageState[kind].uploading.set(true);
    this.uploadImageRequest(kind, file)
      .pipe(finalize(() => this.imageState[kind].uploading.set(false)))
      .subscribe({
        next: (result) => {
          this.getImageControl(kind).setValue(result.url);
          this.getImageControl(kind).markAsDirty();
          this.toast.success(`${this.getImageLabel(kind)} uploaded.`);
          this.resetImageInput(kind);
        },
        error: (error) => {
          this.toast.error(this.readError(error, `Unable to upload ${this.getImageLabel(kind).toLowerCase()}.`));
          this.resetImageInput(kind);
        }
      });
  }

  private tryUseDroppedUrl(kind: SettingsImageKind, value: string): boolean {
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

      this.getImageControl(kind).setValue(parsed.toString());
      this.getImageControl(kind).markAsDirty();
      this.toast.success(`${this.getImageLabel(kind)} URL added.`);
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

  private normalizePersistedOptionalImageUrl(raw: string | null | undefined): string {
    const value = (raw ?? '').trim();
    if (!value) {
      return '';
    }

    return SettingsPageComponent.isPersistableLogoUrl(value)
      ? value
      : '';
  }

  private syncTaxConfiguration(isTaxApplicable: boolean): void {
    if (isTaxApplicable) {
      if (Number(this.settingsForm.controls.defaultTaxRate.value) <= 0) {
        this.settingsForm.controls.defaultTaxRate.setValue(0.08, { emitEvent: false });
      }
      return;
    }

    if (Number(this.settingsForm.controls.defaultTaxRate.value) !== 0) {
      this.settingsForm.controls.defaultTaxRate.setValue(0, { emitEvent: false });
    }
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

  private validateImageUrl(kind: SettingsImageKind, value: string): boolean {
    if (value && !SettingsPageComponent.isPersistableLogoUrl(value)) {
      this.getImageControl(kind).markAsTouched();
      this.toast.error(`${this.getImageLabel(kind)} URL must be an HTTP/HTTPS URL, /uploads path, or root static path like /logo-name.svg.`);
      return false;
    }

    return true;
  }

  private getImageControl(kind: SettingsImageKind) {
    switch (kind) {
      case 'logo':
        return this.settingsForm.controls.logoUrl;
      case 'stamp':
        return this.settingsForm.controls.companyStampUrl;
      case 'signature':
        return this.settingsForm.controls.companySignatureUrl;
    }
  }

  private getImageInput(kind: SettingsImageKind): ElementRef<HTMLInputElement> | undefined {
    switch (kind) {
      case 'logo':
        return this.logoFileInput;
      case 'stamp':
        return this.stampFileInput;
      case 'signature':
        return this.signatureFileInput;
    }
  }

  private getImageLabel(kind: SettingsImageKind): string {
    switch (kind) {
      case 'logo':
        return 'Logo';
      case 'stamp':
        return 'Company stamp';
      case 'signature':
        return 'Signature';
    }
  }

  private getImageRemovedMessage(kind: SettingsImageKind): string {
    switch (kind) {
      case 'logo':
        return 'Logo reset to default invoice branding.';
      case 'stamp':
        return 'Company stamp removed.';
      case 'signature':
        return 'Signature removed.';
    }
  }

  private uploadImageRequest(kind: SettingsImageKind, file: File) {
    switch (kind) {
      case 'logo':
        return this.api.uploadSettingsLogo(file);
      case 'stamp':
        return this.api.uploadSettingsStamp(file);
      case 'signature':
        return this.api.uploadSettingsSignature(file);
    }
  }

  private resetImageInput(kind: SettingsImageKind): void {
    const input = this.getImageInput(kind);
    if (input) {
      input.nativeElement.value = '';
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



