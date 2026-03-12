import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  PortalAdminBillingBusinessOption,
  PortalAdminBillingCustomInvoiceLineItemRequest,
  PortalAdminBillingGenerationResult
} from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';

type GenerateMode = 'single' | 'bulk' | 'custom';

@Component({
  selector: 'app-portal-admin-billing-generate-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppEmptyStateComponent, AppLoaderComponent],
  template: `
    <section class="page-head">
      <h1>Generate Invoices</h1>
      <p>Preview and create monthly platform invoices with default or custom pricing rules.</p>
    </section>

    <app-loader *ngIf="loadingBusinesses()"></app-loader>

    <ng-container *ngIf="!loadingBusinesses()">
      <app-card class="mode-switch">
        <button type="button" [class.active]="mode() === 'single'" (click)="mode.set('single')">Single Business</button>
        <button type="button" [class.active]="mode() === 'bulk'" (click)="mode.set('bulk')">Bulk Generation</button>
        <button type="button" [class.active]="mode() === 'custom'" (click)="mode.set('custom')">Custom Invoice</button>
      </app-card>

      <app-card *ngIf="mode() === 'single'" class="form-card">
        <h2>Generate Invoice for One Business</h2>
        <form [formGroup]="singleForm" class="form-grid">
          <label>
            Business
            <select formControlName="tenantId">
              <option value="">Select business</option>
              <option *ngFor="let business of businesses()" [value]="business.tenantId">
                {{ business.companyName }} ({{ business.staffCount }} staff - {{ business.vesselCount }} vessels)
              </option>
            </select>
          </label>
          <label>
            Billing Month
            <input type="month" formControlName="billingMonth">
          </label>
          <label class="checkbox"><input type="checkbox" formControlName="allowDuplicateForMonth"> Allow duplicate month invoice</label>
          <div class="actions">
            <app-button type="button" variant="secondary" [loading]="running()" (clicked)="runSingle(true)">Preview</app-button>
            <app-button type="button" [loading]="running()" (clicked)="runSingle(false)">Generate Invoice</app-button>
          </div>
        </form>
      </app-card>

      <app-card *ngIf="mode() === 'bulk'" class="form-card">
        <h2>Generate Invoices for All Businesses</h2>
        <form [formGroup]="bulkForm" class="form-grid">
          <label>
            Billing Month
            <input type="month" formControlName="billingMonth">
          </label>
          <label class="checkbox"><input type="checkbox" formControlName="includeDisabledBusinesses"> Include disabled businesses</label>
          <label class="checkbox"><input type="checkbox" formControlName="allowDuplicateForMonth"> Allow duplicates for selected month</label>
          <label class="full">
            Optional Business Selection
            <select multiple [value]="bulkTenantIds()" (change)="onMultiSelect($event, 'bulk')">
              <option *ngFor="let business of businesses()" [value]="business.tenantId">{{ business.companyName }}</option>
            </select>
          </label>
          <div class="actions">
            <app-button type="button" variant="secondary" [loading]="running()" (clicked)="runBulk(true)">Preview</app-button>
            <app-button type="button" [loading]="running()" (clicked)="runBulk(false)">Generate Invoices</app-button>
          </div>
        </form>
      </app-card>

      <app-card *ngIf="mode() === 'custom'" class="form-card">
        <h2>Create Custom Invoice</h2>
        <form [formGroup]="customForm" class="form-grid">
          <label class="full">
            Businesses
            <select multiple [value]="customTenantIds()" (change)="onMultiSelect($event, 'custom')">
              <option *ngFor="let business of businesses()" [value]="business.tenantId">{{ business.companyName }}</option>
            </select>
          </label>
          <label>Billing Month <input type="month" formControlName="billingMonth"></label>
          <label>
            Currency
            <select formControlName="currency">
              <option value="">Default by settings/custom rate</option>
              <option value="MVR">MVR</option>
              <option value="USD">USD</option>
            </select>
          </label>
          <label>Invoice Date <input type="date" formControlName="invoiceDate"></label>
          <label>Due Date <input type="date" formControlName="dueDate"></label>
          <label>Software Fee Override <input type="number" step="0.01" formControlName="softwareFee"></label>
          <label>Vessel Fee Override <input type="number" step="0.01" formControlName="vesselFee"></label>
          <label>Staff Fee Override <input type="number" step="0.01" formControlName="staffFee"></label>
          <label class="checkbox"><input type="checkbox" formControlName="saveAsBusinessCustomRate"> Save overrides as business custom rate</label>
          <label class="checkbox"><input type="checkbox" formControlName="allowDuplicateForMonth"> Allow duplicate month invoice</label>
          <label class="full">Notes <textarea rows="2" formControlName="notes"></textarea></label>
        </form>

        <div class="line-items">
          <div class="line-header">
            <h3>Additional Line Items</h3>
            <app-button size="sm" variant="secondary" (clicked)="addLineItem()">Add Item</app-button>
          </div>
          <div class="line-row" *ngFor="let item of customLineItems(); let i = index">
            <input type="text" [value]="item.description" placeholder="Description" (input)="updateLineItem(i, 'description', $event)">
            <input type="number" [value]="item.quantity" step="0.01" min="0.01" (input)="updateLineItem(i, 'quantity', $event)">
            <input type="number" [value]="item.rate" step="0.01" min="0" (input)="updateLineItem(i, 'rate', $event)">
            <app-button size="sm" variant="danger" (clicked)="removeLineItem(i)">Remove</app-button>
          </div>
        </div>

        <div class="actions">
          <app-button type="button" variant="secondary" [loading]="running()" (clicked)="runCustom(true)">Preview</app-button>
          <app-button type="button" [loading]="running()" (clicked)="runCustom(false)">Generate Custom Invoice</app-button>
        </div>
      </app-card>

      <app-card class="result-card" *ngIf="result() as generated">
        <h2>{{ generated.previewOnly ? 'Preview Result' : 'Generation Result' }}</h2>
        <p>{{ generated.generatedCount }} generated - {{ generated.skippedCount }} skipped</p>

        <app-empty-state
          *ngIf="generated.invoices.length === 0"
          title="No invoices generated"
          description="All selected businesses were skipped based on current rules.">
        </app-empty-state>

        <div class="table-wrap" *ngIf="generated.invoices.length > 0">
          <table>
            <thead>
              <tr>
                <th>Invoice</th>
                <th>Business</th>
                <th>Month</th>
                <th>Staff</th>
                <th>Vessels</th>
                <th>Total</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let row of generated.invoices">
                <td>{{ row.invoiceNumber }}</td>
                <td>{{ row.companyName }}</td>
                <td>{{ row.billingMonth | date:'yyyy-MM' }}</td>
                <td>{{ row.staffCount }}</td>
                <td>{{ row.vesselCount }}</td>
                <td>{{ row.currency }} {{ row.total | number:'1.2-2' }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="table-wrap skipped" *ngIf="generated.skipped.length > 0">
          <table>
            <thead>
              <tr>
                <th>Business</th>
                <th>Reason</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let row of generated.skipped">
                <td>{{ row.companyName }}</td>
                <td>{{ row.reason }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </app-card>
    </ng-container>
  `,
  styles: `
    .page-head h1 { margin: 0; color: #2f4269; font-family: var(--font-heading); font-size: 1.5rem; font-weight: 600; }
    .page-head p { margin: .3rem 0 0; color: #63759d; }
    .mode-switch {
      margin-top: .82rem;
      --card-padding: .5rem;
      display: flex;
      gap: .42rem;
      flex-wrap: wrap;
    }
    .mode-switch button {
      border: 1px solid #d5e0f8;
      border-radius: 12px;
      background: linear-gradient(145deg, rgba(255,255,255,.92), rgba(237,244,255,.88));
      color: #5a6d97;
      font-family: var(--font-heading);
      font-size: .84rem;
      font-weight: 600;
      padding: .52rem .82rem;
      cursor: pointer;
    }
    .mode-switch button.active {
      border-color: transparent;
      color: #fff;
      background: linear-gradient(135deg, #7383f8, #67a8ef);
      box-shadow: 0 12px 22px rgba(96, 112, 199, .25);
    }
    .form-card { margin-top: .72rem; }
    .form-card h2 { margin: 0 0 .62rem; color: #33486f; font-family: var(--font-heading); font-size: 1.05rem; font-weight: 600; }
    .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .56rem .62rem; }
    .form-grid > * { min-width: 0; }
    .form-grid label {
      display: grid;
      gap: .2rem;
      color: #5f739d;
      font-size: .78rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .form-grid label.full { grid-column: 1 / -1; }
    .checkbox {
      display: flex !important;
      align-items: center;
      gap: .44rem;
      border: 1px solid #d8e2f9;
      border-radius: 11px;
      background: rgba(255,255,255,.86);
      min-height: 41px;
      padding: .48rem .56rem;
      color: #4b6088;
      font-size: .82rem;
    }
    .form-grid input:not([type='checkbox']),
    .form-grid select,
    .form-grid textarea {
      border: 1px solid #ccdaf5;
      border-radius: 11px;
      background: #fff;
      color: #34496e;
      padding: .55rem .63rem;
      font-size: .86rem;
      min-height: 41px;
    }
    .form-grid .checkbox input[type='checkbox'] {
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
    .form-grid select[multiple] {
      min-height: 120px;
      padding: .4rem;
    }
    .form-grid input:not([type='checkbox']):focus,
    .form-grid select:focus,
    .form-grid textarea:focus {
      outline: none;
      border-color: #7f8df5;
      box-shadow: 0 0 0 3px rgba(126, 140, 245, .16);
    }
    .form-grid .checkbox input[type='checkbox']:focus {
      outline: none;
      box-shadow: 0 0 0 3px rgba(126, 140, 245, .16);
    }
    .actions {
      margin-top: .64rem;
      display: flex;
      justify-content: flex-end;
      gap: .44rem;
      flex-wrap: wrap;
    }
    .line-items {
      margin-top: .62rem;
      border: 1px solid #dce6fb;
      border-radius: 12px;
      background: #f8fbff;
      padding: .62rem;
      display: grid;
      gap: .44rem;
    }
    .line-header { display: flex; justify-content: space-between; align-items: center; gap: .5rem; }
    .line-header h3 { margin: 0; color: #3a507a; font-family: var(--font-heading); font-size: .92rem; font-weight: 600; }
    .line-row {
      display: grid;
      grid-template-columns: 1.5fr .6fr .7fr auto;
      gap: .42rem;
      align-items: center;
    }
    .line-row input {
      border: 1px solid #cddaf5;
      border-radius: 10px;
      background: #fff;
      color: #34496e;
      padding: .5rem .56rem;
      min-height: 39px;
      font-size: .84rem;
    }
    .result-card { margin-top: .74rem; }
    .result-card h2 { margin: 0; color: #344a70; font-family: var(--font-heading); font-size: 1.04rem; font-weight: 600; }
    .result-card p { margin: .28rem 0 .56rem; color: #60739d; font-size: .84rem; }
    .table-wrap { border: 1px solid #d9e3fa; border-radius: 12px; overflow: auto; margin-top: .48rem; }
    table { width: 100%; min-width: 780px; border-collapse: collapse; font-size: .83rem; }
    th, td { padding: .5rem .58rem; text-align: left; border-bottom: 1px solid #e2e9fc; color: #42567f; }
    th {
      background: #f3f7ff;
      color: #5e74a2;
      text-transform: uppercase;
      letter-spacing: .04em;
      font-family: var(--font-heading);
      font-size: .73rem;
      font-weight: 600;
    }
    .table-wrap.skipped th:first-child,
    .table-wrap.skipped td:first-child {
      width: 28%;
      white-space: nowrap;
    }
    @media (max-width: 1300px) {
      .form-grid { grid-template-columns: 1fr; }
      .line-row { grid-template-columns: 1fr; }
      .actions { justify-content: flex-start; }
    }
  `
})
export class PortalAdminBillingGeneratePageComponent {
  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly mode = signal<GenerateMode>('single');
  readonly loadingBusinesses = signal(true);
  readonly running = signal(false);
  readonly businesses = signal<PortalAdminBillingBusinessOption[]>([]);
  readonly bulkTenantIds = signal<string[]>([]);
  readonly customTenantIds = signal<string[]>([]);
  readonly customLineItems = signal<PortalAdminBillingCustomInvoiceLineItemRequest[]>([]);
  readonly result = signal<PortalAdminBillingGenerationResult | null>(null);

  readonly singleForm = this.fb.nonNullable.group({
    tenantId: ['', [Validators.required]],
    billingMonth: [this.currentMonth(), [Validators.required]],
    allowDuplicateForMonth: [false]
  });

  readonly bulkForm = this.fb.nonNullable.group({
    billingMonth: [this.currentMonth(), [Validators.required]],
    includeDisabledBusinesses: [false],
    allowDuplicateForMonth: [false]
  });

  readonly customForm = this.fb.nonNullable.group({
    billingMonth: [this.currentMonth(), [Validators.required]],
    currency: [''],
    invoiceDate: [''],
    dueDate: [''],
    softwareFee: [null as number | null],
    vesselFee: [null as number | null],
    staffFee: [null as number | null],
    saveAsBusinessCustomRate: [false],
    allowDuplicateForMonth: [false],
    notes: ['']
  });

  constructor() {
    this.loadBusinesses();
  }

  onMultiSelect(event: Event, target: 'bulk' | 'custom'): void {
    const select = event.target as HTMLSelectElement;
    const values = Array.from(select.selectedOptions).map((option) => option.value);
    if (target === 'bulk') {
      this.bulkTenantIds.set(values);
      return;
    }

    this.customTenantIds.set(values);
  }

  addLineItem(): void {
    this.customLineItems.update((items) => [...items, { description: '', quantity: 1, rate: 0 }]);
  }

  removeLineItem(index: number): void {
    this.customLineItems.update((items) => items.filter((_, i) => i !== index));
  }

  updateLineItem(index: number, key: 'description' | 'quantity' | 'rate', event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.customLineItems.update((items) =>
      items.map((item, i) =>
      {
        if (i !== index) {
          return item;
        }

        if (key === 'description') {
          return { ...item, description: value };
        }

        const numeric = Number(value);
        return { ...item, [key]: Number.isFinite(numeric) ? numeric : 0 };
      }));
  }

  runSingle(previewOnly: boolean): void {
    if (this.singleForm.invalid) {
      this.singleForm.markAllAsTouched();
      this.toast.error('Business and billing month are required.');
      return;
    }

    const raw = this.singleForm.getRawValue();
    this.running.set(true);
    this.api.generateBillingInvoice({
      tenantId: raw.tenantId,
      billingMonth: this.monthToDateOnly(raw.billingMonth),
      allowDuplicateForMonth: raw.allowDuplicateForMonth,
      previewOnly
    })
    .pipe(finalize(() => this.running.set(false)))
    .subscribe({
      next: (result) => {
        this.result.set(result);
        this.toast.success(previewOnly ? 'Single invoice preview generated.' : 'Single invoice generated.');
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to generate invoice.'))
    });
  }

  runBulk(previewOnly: boolean): void {
    if (this.bulkForm.invalid) {
      this.bulkForm.markAllAsTouched();
      this.toast.error('Billing month is required.');
      return;
    }

    const raw = this.bulkForm.getRawValue();
    this.running.set(true);
    this.api.generateBillingInvoicesBulk({
      billingMonth: this.monthToDateOnly(raw.billingMonth),
      includeDisabledBusinesses: raw.includeDisabledBusinesses,
      allowDuplicateForMonth: raw.allowDuplicateForMonth,
      previewOnly,
      tenantIds: this.bulkTenantIds()
    })
    .pipe(finalize(() => this.running.set(false)))
    .subscribe({
      next: (result) => {
        this.result.set(result);
        this.toast.success(previewOnly ? 'Bulk preview generated.' : 'Bulk invoices generated.');
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to generate bulk invoices.'))
    });
  }

  runCustom(previewOnly: boolean): void {
    if (this.customForm.invalid) {
      this.customForm.markAllAsTouched();
      this.toast.error('Please check custom invoice values.');
      return;
    }

    if (this.customTenantIds().length === 0) {
      this.toast.error('Select at least one business for custom invoice.');
      return;
    }

    const invalidLine = this.customLineItems().find((item) => !item.description.trim() || item.quantity <= 0);
    if (invalidLine) {
      this.toast.error('Each custom line item requires description and positive quantity.');
      return;
    }

    const raw = this.customForm.getRawValue();
    const selectedCurrency = raw.currency === 'MVR' || raw.currency === 'USD' ? raw.currency : undefined;
    this.running.set(true);
    this.api.createCustomBillingInvoices({
      tenantIds: this.customTenantIds(),
      billingMonth: this.monthToDateOnly(raw.billingMonth),
      currency: selectedCurrency,
      invoiceDate: raw.invoiceDate || undefined,
      dueDate: raw.dueDate || undefined,
      softwareFee: raw.softwareFee ?? undefined,
      vesselFee: raw.vesselFee ?? undefined,
      staffFee: raw.staffFee ?? undefined,
      saveAsBusinessCustomRate: raw.saveAsBusinessCustomRate,
      allowDuplicateForMonth: raw.allowDuplicateForMonth,
      previewOnly,
      notes: raw.notes.trim() || undefined,
      lineItems: this.customLineItems().map((item) => ({
        description: item.description.trim(),
        quantity: item.quantity,
        rate: item.rate
      }))
    })
    .pipe(finalize(() => this.running.set(false)))
    .subscribe({
      next: (result) => {
        this.result.set(result);
        this.toast.success(previewOnly ? 'Custom preview generated.' : 'Custom invoices generated.');
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to generate custom invoices.'))
    });
  }

  private loadBusinesses(): void {
    this.loadingBusinesses.set(true);
    this.api.getBillingBusinessOptions()
      .pipe(finalize(() => this.loadingBusinesses.set(false)))
      .subscribe({
        next: (result) => this.businesses.set(result),
        error: (error) => this.toast.error(extractApiError(error, 'Unable to load businesses for invoice generation.'))
      });
  }

  private monthToDateOnly(value: string): string {
    return `${value}-01`;
  }

  private currentMonth(): string {
    const now = new Date();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    return `${now.getFullYear()}-${month}`;
  }
}



