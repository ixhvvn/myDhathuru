import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppDateBadgeComponent } from '../../../shared/components/app-date-badge/app-date-badge.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AppStatusChipComponent } from '../../../shared/components/app-status-chip/app-status-chip.component';
import { Customer, DeliveryNoteListItem, Invoice, InvoiceListItem, PagedResult, Vessel } from '../../../core/models/app.models';
import { AuthService } from '../../../core/services/auth.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { PortalApiService } from '../../services/portal-api.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-invoices-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppButtonComponent,
    AppCardComponent,
    AppCurrencyPipe,
    AppDataTableComponent,
    AppDateBadgeComponent,
    AppPageHeaderComponent,
    AppSearchBarComponent,
    AppStatusChipComponent
  ],
  template: `
    <app-page-header title="Sales History" subtitle="Create invoices and receive payments">
      <app-button *ngIf="isAdmin()" variant="danger" (clicked)="openClearAllDialog()">Clear All</app-button>
      <app-button (clicked)="openCreate()">New Invoice</app-button>
    </app-page-header>

    <app-card>
      <div class="filters">
        <select [value]="createdDatePreset()" (change)="onCreatedDatePresetChange($event)">
          <option value="">All Dates</option>
          <option *ngFor="let option of datePresetOptions" [value]="option.value">{{ option.label }}</option>
        </select>
        <app-search-bar [value]="search()" placeholder="Search invoice or customer" (searchChange)="onSearch($event)"></app-search-bar>
      </div>
      <div class="summary" *ngIf="invoices() as page">
        Showing {{ page.totalCount }} invoice{{ page.totalCount === 1 ? '' : 's' }}
        <span *ngIf="createdDatePreset()">for {{ currentDateFilterLabel() }}</span>.
      </div>

      <app-data-table [hasData]="(invoices()?.items?.length ?? 0) > 0" emptyTitle="No invoices" emptyDescription="Create your first invoice.">
        <thead>
          <tr>
            <th>Invoice No</th>
            <th>Customer</th>
            <th>Currency</th>
            <th>Amount</th>
            <th>Date Issued</th>
            <th>Date Due</th>
            <th>Payment Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let invoice of invoices()?.items">
            <td>{{ invoice.invoiceNo }}</td>
            <td>{{ invoice.customer }}</td>
            <td>{{ invoice.currency }}</td>
            <td>{{ invoice.amount | appCurrency: invoice.currency }}</td>
            <td><app-date-badge [value]="invoice.dateIssued"></app-date-badge></td>
            <td><app-date-badge [value]="invoice.dateDue"></app-date-badge></td>
            <td>
              <app-status-chip [label]="invoice.paymentStatus" [variant]="statusVariant(invoice.paymentStatus)"></app-status-chip>
            </td>
            <td class="actions">
              <app-button size="sm" variant="secondary" (clicked)="openDetail(invoice)">View</app-button>
              <app-button size="sm" variant="secondary" (clicked)="edit(invoice)">Edit</app-button>
              <app-button
                size="sm"
                [variant]="paymentButtonVariant(invoice.paymentStatus)"
                [disabled]="invoice.paymentStatus === 'Paid'"
                (clicked)="openPayment(invoice)">
                {{ paymentButtonLabel(invoice.paymentStatus) }}
              </app-button>
              <app-button size="sm" variant="secondary" (clicked)="exportInvoice(invoice)">PDF</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>

      <div class="pager" *ngIf="invoices() as page">
        <span>Page {{ page.pageNumber }} of {{ page.totalPages || 1 }}</span>
        <div>
          <app-button size="sm" variant="secondary" [disabled]="page.pageNumber <= 1" (clicked)="changePage(page.pageNumber - 1)">Prev</app-button>
          <app-button size="sm" variant="secondary" [disabled]="page.pageNumber >= page.totalPages" (clicked)="changePage(page.pageNumber + 1)">Next</app-button>
        </div>
      </div>
    </app-card>

    <div class="drawer" *ngIf="formOpen()">
      <app-card>
        <h3>{{ editId() ? 'Edit Invoice' : 'New Invoice' }}</h3>
        <form [formGroup]="form" (ngSubmit)="save()" class="form-grid">
          <div class="two-col">
            <label>Customer
              <select formControlName="customerId">
                <option value="">Select customer</option>
                <option *ngFor="let customer of customers()" [value]="customer.id">{{ customer.name }}</option>
              </select>
            </label>
            <label>Delivery Note
              <select formControlName="deliveryNoteId" (change)="onDeliveryNoteChange($event)">
                <option value="">None</option>
                <option *ngFor="let note of deliveryNotes()" [value]="note.id">{{ note.deliveryNoteNo }}</option>
              </select>
            </label>
          </div>
          <small class="field-note" *ngIf="isCustomerLocked()">Customer is locked to the selected delivery note.</small>

          <div class="two-col">
            <label>Courier
              <select formControlName="courierId">
                <option value="">Select courier</option>
                <option *ngFor="let vessel of vessels()" [value]="vessel.id">{{ vessel.name }}</option>
              </select>
            </label>
            <label>PO Number (Optional) <input type="text" formControlName="poNumber"></label>
          </div>
          <small class="field-note" *ngIf="isCourierLocked()">Courier is locked to the selected delivery note.</small>

          <div class="two-col">
            <label>Date Issued <input type="date" formControlName="dateIssued"></label>
            <label>Date Due <input type="date" formControlName="dateDue"></label>
          </div>

          <div class="two-col">
            <label>Tax Rate <input type="number" formControlName="taxRate" step="0.01"></label>
            <label>Currency
              <select formControlName="currency">
                <option value="MVR">MVR</option>
                <option value="USD">USD</option>
              </select>
            </label>
          </div>
          <small class="field-note" *ngIf="isCurrencyLocked()">Currency is locked to the selected delivery note.</small>

          <label>Notes <textarea rows="2" formControlName="notes"></textarea></label>

          <div class="item-section">
            <h4>Items</h4>
            <app-button size="sm" variant="secondary" (clicked)="addItem()">Add Item</app-button>
          </div>
          <div formArrayName="items" class="items-grid">
            <div *ngFor="let item of items.controls; let i = index" [formGroupName]="i" class="item-row">
              <label class="item-field">
                <span>Description</span>
                <input type="text" formControlName="description" placeholder="Description">
              </label>
              <label class="item-field">
                <span>Qty</span>
                <input type="number" formControlName="qty" placeholder="Qty">
              </label>
              <label class="item-field">
                <span>Rate</span>
                <input type="number" formControlName="rate" placeholder="Rate">
              </label>
              <div class="item-actions">
                <app-button size="sm" variant="danger" (clicked)="removeItem(i)" [disabled]="items.length === 1">Remove</app-button>
              </div>
            </div>
          </div>

          <div class="form-actions">
            <app-button variant="secondary" (clicked)="formOpen.set(false)">Cancel</app-button>
            <app-button type="submit">Save</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <div class="drawer" *ngIf="detail()">
      <app-card>
        <h3>Invoice {{ detail()?.invoiceNo }}</h3>
        <p><strong>Customer:</strong> {{ detail()?.customerName }}</p>
        <p><strong>Courier:</strong> {{ detail()?.courierName || '-' }}</p>
        <p><strong>PO No:</strong> {{ detail()?.poNumber || '-' }}</p>
        <p><strong>Currency:</strong> {{ detail()?.currency }}</p>
        <p><strong>Issued:</strong> {{ detail()?.dateIssued }} | <strong>Due:</strong> {{ detail()?.dateDue }}</p>
        <p><strong>Subtotal:</strong> {{ detail()?.subtotal || 0 | appCurrency: (detail()?.currency || 'MVR') }} | <strong>GST:</strong> {{ detail()?.taxAmount || 0 | appCurrency: (detail()?.currency || 'MVR') }}</p>
        <p><strong>Grand Total:</strong> {{ detail()?.grandTotal || 0 | appCurrency: (detail()?.currency || 'MVR') }} | <strong>Balance:</strong> {{ detail()?.balance || 0 | appCurrency: (detail()?.currency || 'MVR') }}</p>
        <div class="detail-list">
          <div *ngFor="let item of detail()?.items">{{ item.description }} - {{ item.qty }} x {{ item.rate | appCurrency: (detail()?.currency || 'MVR') }} = {{ item.total || 0 | appCurrency: (detail()?.currency || 'MVR') }}</div>
        </div>
        <div class="detail-list">
          <h4>Payments</h4>
          <div *ngFor="let payment of detail()?.payments">{{ payment.paymentDate }} | {{ payment.method }} | {{ payment.amount | appCurrency: (payment.currency || detail()?.currency || 'MVR') }}</div>
        </div>
        <div class="form-actions">
          <app-button variant="secondary" (clicked)="detail.set(null)">Close</app-button>
        </div>
      </app-card>
    </div>

    <div class="drawer" *ngIf="paymentOpen()">
      <app-card>
        <h3>Receive Payment</h3>
        <form [formGroup]="paymentForm" (ngSubmit)="savePayment()" class="form-grid">
          <label>Currency
            <select formControlName="currency">
              <option value="MVR">MVR</option>
              <option value="USD">USD</option>
            </select>
          </label>
          <label>
            Amount
            <input type="number" formControlName="amount" step="0.01" [attr.max]="paymentMaxAmount()">
            <small class="field-error" *ngIf="paymentForm.controls.amount.touched && paymentForm.controls.amount.hasError('max')">
              Amount cannot exceed current balance.
            </small>
          </label>
          <small class="field-note">Max balance: {{ paymentMaxAmount() | appCurrency: (paymentInvoice()?.currency || paymentForm.getRawValue().currency || 'MVR') }}</small>
          <label>Date <input type="date" formControlName="paymentDate"></label>
          <label>Method
            <select formControlName="method">
              <option value="Cash">Cash</option>
              <option value="Card">Card</option>
              <option value="Transfer">Transfer</option>
            </select>
          </label>
          <label>Reference <input type="text" formControlName="reference"></label>
          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closePayment()">Cancel</app-button>
            <app-button type="submit">Save Payment</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <div class="drawer" *ngIf="clearAllDialogOpen()">
      <app-card class="clear-card">
        <h3>Clear All Sales History</h3>
        <p>This will permanently delete all invoices, invoice items and payments for this business from the application and database.</p>
        <form [formGroup]="clearAllForm" (ngSubmit)="clearAllSalesHistory()" class="form-grid">
          <label>Confirm with your password
            <input type="password" formControlName="password" autocomplete="current-password" placeholder="Enter your account password">
          </label>
          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closeClearAllDialog()">Cancel</app-button>
            <app-button type="submit" variant="danger" [loading]="clearAllPending()">Clear All</app-button>
          </div>
        </form>
      </app-card>
    </div>
  `,
  styles: `
    .filters {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: .6rem;
      margin-bottom: .75rem;
      align-items: center;
    }
    .filters app-search-bar { width: 100%; }
    .filters select {
      width: 100%;
      min-width: 0;
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .58rem .66rem;
      background: linear-gradient(145deg, rgba(255,255,255,.92), rgba(245,249,255,.85));
      height: 42px;
    }
    .summary {
      margin: .1rem 0 .7rem;
      font-size: .84rem;
      color: var(--text-muted);
      font-weight: 600;
    }
    .actions {
      display: flex;
      gap: .35rem;
      flex-wrap: nowrap;
      align-items: center;
      min-width: 420px;
    }
    .actions app-button {
      flex: 0 0 auto;
      white-space: nowrap;
    }
    .pager { margin-top: .8rem; display: flex; justify-content: space-between; align-items: center; gap: .55rem; }
    .drawer {
      position: fixed;
      inset: 0;
      z-index: 1200;
      background: rgba(43, 54, 87, .34);
      backdrop-filter: blur(4px);
      display: grid;
      place-items: center;
      padding: 1rem;
    }
    .drawer app-card {
      width: min(860px, 100%);
      max-height: 92vh;
      overflow: auto;
      --card-bg: linear-gradient(160deg, rgba(255,255,255,.95), rgba(245,248,255,.9));
    }
    .form-grid { display: grid; gap: .78rem; }
    .two-col { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .75rem; }
    label { display: grid; gap: .2rem; font-size: .82rem; color: var(--text-muted); align-content: start; }
    input, select, textarea {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .52rem .6rem;
      background: rgba(255,255,255,.92);
    }
    .item-section { display: flex; justify-content: space-between; align-items: center; }
    .item-row { display: grid; grid-template-columns: 2fr 1fr 1fr auto; gap: .5rem; align-items: end; }
    .item-field { display: grid; gap: .2rem; font-size: .78rem; color: var(--text-muted); }
    .item-actions { display: flex; align-items: end; }
    .field-note { font-size: .78rem; color: var(--text-muted); margin-top: -.1rem; }
    .field-error { font-size: .75rem; line-height: 1.25; color: #bf2f46; margin-top: .08rem; display: block; }
    .form-actions { display: flex; justify-content: flex-end; gap: .5rem; }
    .detail-list { margin: .6rem 0; display: grid; gap: .3rem; }
    .clear-card {
      width: min(520px, 100%);
    }
    .clear-card p {
      margin: .3rem 0 .8rem;
      color: var(--text-muted);
      line-height: 1.45;
    }
    @media (max-width: 940px) {
      .item-row {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .item-actions {
        grid-column: 1 / -1;
      }
    }
    @media (max-width: 700px) {
      .filters {
        grid-template-columns: 1fr;
      }
      .filters app-search-bar {
        max-width: none;
      }
      .two-col {
        grid-template-columns: 1fr;
      }
      .item-section {
        flex-wrap: wrap;
        gap: .45rem;
      }
      .item-section app-button {
        width: 100%;
      }
      .item-row {
        grid-template-columns: 1fr;
      }
      .item-actions app-button {
        width: 100%;
      }
      .form-actions {
        flex-wrap: wrap;
      }
      .form-actions app-button {
        display: block;
        flex: 1 1 140px;
      }
      .pager {
        flex-direction: column;
        align-items: flex-start;
      }
      .pager > div {
        display: inline-flex;
        gap: .45rem;
        width: 100%;
      }
      .drawer {
        padding: .6rem;
      }
      .drawer app-card {
        max-height: 95dvh;
      }
    }
  `
})
export class InvoicesPageComponent implements OnInit {
  readonly invoices = signal<PagedResult<InvoiceListItem> | null>(null);
  readonly search = signal('');
  readonly pageNumber = signal(1);
  readonly createdDatePreset = signal('');
  readonly formOpen = signal(false);
  readonly editId = signal<string | null>(null);
  readonly detail = signal<Invoice | null>(null);
  readonly isCustomerLocked = signal(false);
  readonly isCurrencyLocked = signal(false);
  readonly isCourierLocked = signal(false);
  readonly paymentOpen = signal(false);
  readonly paymentInvoiceId = signal<string | null>(null);
  readonly paymentInvoice = signal<Invoice | null>(null);
  readonly paymentMaxAmount = signal(0);
  readonly clearAllDialogOpen = signal(false);
  readonly clearAllPending = signal(false);

  readonly customers = signal<Customer[]>([]);
  readonly vessels = signal<Vessel[]>([]);
  readonly deliveryNotes = signal<DeliveryNoteListItem[]>([]);

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  readonly form = this.fb.nonNullable.group({
    customerId: ['', Validators.required],
    deliveryNoteId: [''],
    courierId: [''],
    poNumber: [''],
    dateIssued: [this.today(), Validators.required],
    dateDue: [this.today(), Validators.required],
    currency: ['MVR', Validators.required],
    taxRate: [0.08, Validators.required],
    notes: [''],
    items: this.fb.array([this.createItemForm()])
  });

  readonly paymentForm = this.fb.nonNullable.group({
    currency: ['MVR', Validators.required],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    paymentDate: [this.today(), Validators.required],
    method: ['Cash', Validators.required],
    reference: ['']
  });

  readonly clearAllForm = this.fb.nonNullable.group({
    password: ['', Validators.required]
  });

  readonly datePresetOptions = [
    { value: 'today', label: 'Today' },
    { value: 'yesterday', label: 'Yesterday' },
    { value: 'last-week', label: 'Last Week' },
    { value: 'last-7-days', label: 'Last 7 Days' },
    { value: 'last-month', label: 'Last Month' },
    { value: 'last-30-days', label: 'Last 30 Days' }
  ] as const;

  get items(): FormArray {
    return this.form.controls.items as FormArray;
  }

  ngOnInit(): void {
    this.reload();
    this.loadLookup();
  }

  statusVariant(status: string): 'green' | 'red' | 'amber' | 'gray' {
    if (status === 'Paid') {
      return 'green';
    }
    if (status === 'Partial') {
      return 'amber';
    }
    return 'red';
  }

  paymentButtonVariant(status: string): 'secondary' | 'warning' | 'success' {
    if (status === 'Paid') {
      return 'success';
    }
    if (status === 'Partial') {
      return 'warning';
    }
    return 'secondary';
  }

  paymentButtonLabel(status: string): string {
    if (status === 'Paid') {
      return 'Paid';
    }
    if (status === 'Partial') {
      return 'Partial';
    }
    return 'Receive Payment';
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.pageNumber.set(1);
    this.reload();
  }

  onCreatedDatePresetChange(event: Event): void {
    this.createdDatePreset.set((event.target as HTMLSelectElement).value);
    this.pageNumber.set(1);
    this.reload();
  }

  changePage(page: number): void {
    this.pageNumber.set(page);
    this.reload();
  }

  openCreate(): void {
    this.editId.set(null);
    this.isCustomerLocked.set(false);
    this.isCurrencyLocked.set(false);
    this.isCourierLocked.set(false);
    this.form.reset({ customerId: '', deliveryNoteId: '', courierId: '', poNumber: '', dateIssued: this.today(), dateDue: this.today(), currency: 'MVR', taxRate: 0.08, notes: '' });
    this.form.controls.customerId.enable();
    this.form.controls.currency.enable();
    this.form.controls.courierId.enable();
    this.form.setControl('items', this.fb.array([this.createItemForm()]));
    this.formOpen.set(true);
  }

  edit(invoice: InvoiceListItem): void {
    this.api.getInvoiceById(invoice.id).subscribe({
      next: (detail) => {
        this.editId.set(invoice.id);
        const hasDeliveryNote = !!detail.deliveryNoteId;
        this.isCustomerLocked.set(hasDeliveryNote);
        this.isCurrencyLocked.set(hasDeliveryNote);
        this.isCourierLocked.set(hasDeliveryNote);
        this.form.reset({
          customerId: detail.customerId,
          deliveryNoteId: detail.deliveryNoteId || '',
          courierId: detail.courierId || '',
          poNumber: detail.poNumber || '',
          dateIssued: detail.dateIssued,
          dateDue: detail.dateDue,
          currency: detail.currency,
          taxRate: detail.taxRate,
          notes: detail.notes || ''
        });
        if (hasDeliveryNote) {
          this.form.controls.customerId.disable();
          this.form.controls.currency.disable();
          this.form.controls.courierId.disable();
        } else {
          this.form.controls.customerId.enable();
          this.form.controls.currency.enable();
          this.form.controls.courierId.enable();
        }
        this.form.setControl('items', this.fb.array(detail.items.map((item) => this.createItemForm(item))));
        this.formOpen.set(true);
      },
      error: () => this.toast.error('Failed to load invoice details.')
    });
  }

  onDeliveryNoteChange(event: Event): void {
    const deliveryNoteId = (event.target as HTMLSelectElement).value;

    if (!deliveryNoteId) {
      this.isCustomerLocked.set(false);
      this.isCurrencyLocked.set(false);
      this.isCourierLocked.set(false);
      this.form.controls.customerId.enable();
      this.form.controls.currency.enable();
      this.form.controls.courierId.enable();
      return;
    }

    this.api.getDeliveryNoteById(deliveryNoteId).subscribe({
      next: (note) => {
        this.form.controls.customerId.setValue(note.customerId);
        this.form.controls.currency.setValue(note.currency);
        this.form.controls.courierId.setValue(note.vesselId || '');
        if (!this.form.controls.poNumber.value) {
          this.form.controls.poNumber.setValue(note.deliveryNoteNo);
        }
        this.form.controls.customerId.disable();
        this.form.controls.currency.disable();
        this.form.controls.courierId.disable();
        this.isCustomerLocked.set(true);
        this.isCurrencyLocked.set(true);
        this.isCourierLocked.set(true);
      },
      error: () => this.toast.error('Failed to load delivery note customer.')
    });
  }

  openDetail(invoice: InvoiceListItem): void {
    this.api.getInvoiceById(invoice.id).subscribe({
      next: (detail) => this.detail.set(detail),
      error: () => this.toast.error('Failed to load invoice details.')
    });
  }

  openPayment(invoice: InvoiceListItem): void {
    this.paymentInvoiceId.set(invoice.id);
    this.api.getInvoiceById(invoice.id).subscribe({
      next: (detail) => {
        const maxAmount = Math.max(Number(detail.balance || 0), 0);
        if (maxAmount <= 0) {
          this.toast.info('This invoice is already fully paid.');
          return;
        }

        this.paymentInvoice.set(detail);
        this.paymentMaxAmount.set(maxAmount);
        this.paymentForm.controls.amount.setValidators([Validators.required, Validators.min(0.01), Validators.max(maxAmount)]);
        this.paymentForm.controls.amount.updateValueAndValidity({ emitEvent: false });
        this.paymentForm.reset({
          currency: detail.currency || invoice.currency || 'MVR',
          amount: maxAmount,
          paymentDate: this.today(),
          method: 'Cash',
          reference: ''
        });
        this.paymentForm.controls.currency.disable();
        this.paymentOpen.set(true);
      },
      error: (error) => this.toast.error(this.readError(error, 'Failed to load invoice payment details.'))
    });
  }

  addItem(): void {
    this.items.push(this.createItemForm());
  }

  removeItem(index: number): void {
    if (this.items.length <= 1) {
      return;
    }
    this.items.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) {
      this.toast.error('Please complete required invoice fields.');
      return;
    }

    const raw = this.form.getRawValue();
    const payload = {
      customerId: raw.customerId,
      deliveryNoteId: raw.deliveryNoteId || null,
      courierId: raw.courierId || null,
      poNumber: raw.poNumber || null,
      dateIssued: raw.dateIssued,
      dateDue: raw.dateDue,
      currency: raw.currency,
      taxRate: Number(raw.taxRate),
      notes: raw.notes || null,
      items: raw.items.map((item) => ({
        description: item.description,
        qty: Number(item.qty),
        rate: Number(item.rate)
      }))
    };

    const request$ = this.editId()
      ? this.api.updateInvoice(this.editId()!, payload)
      : this.api.createInvoice(payload);

    request$.subscribe({
      next: () => {
        this.toast.success(`Invoice ${this.editId() ? 'updated' : 'created'} successfully.`);
        this.formOpen.set(false);
        this.reload();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to save invoice.'))
    });
  }

  savePayment(): void {
    const invoiceId = this.paymentInvoiceId();
    if (!invoiceId) {
      return;
    }

    if (this.paymentForm.invalid) {
      this.paymentForm.markAllAsTouched();
      const amountControl = this.paymentForm.controls.amount;
      if (amountControl.hasError('max')) {
        this.toast.error(`Payment cannot exceed current balance (${this.paymentForm.getRawValue().currency} ${this.paymentMaxAmount().toFixed(2)}).`);
      } else {
        this.toast.error('Enter a payment amount greater than 0.');
      }
      return;
    }

    const payload = this.paymentForm.getRawValue();
    if (Number(payload.amount) > this.paymentMaxAmount()) {
      this.toast.error(`Payment cannot exceed current balance (${this.paymentForm.getRawValue().currency} ${this.paymentMaxAmount().toFixed(2)}).`);
      return;
    }

    this.api.receiveInvoicePayment(invoiceId, payload).subscribe({
      next: () => {
        this.toast.success('Payment recorded.');
        this.closePayment();
        this.reload();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to save payment.'))
    });
  }

  closePayment(): void {
    this.paymentOpen.set(false);
    this.paymentInvoice.set(null);
    this.paymentMaxAmount.set(0);
    this.paymentInvoiceId.set(null);
  }

  exportInvoice(invoice: InvoiceListItem): void {
    this.api.exportInvoice(invoice.id).subscribe({
      next: (file) => this.download(file, `${invoice.invoiceNo}.pdf`),
      error: () => this.toast.error('Failed to export invoice PDF.')
    });
  }

  openClearAllDialog(): void {
    this.clearAllForm.reset({ password: '' });
    this.clearAllDialogOpen.set(true);
  }

  closeClearAllDialog(): void {
    if (this.clearAllPending()) {
      return;
    }

    this.clearAllDialogOpen.set(false);
  }

  clearAllSalesHistory(): void {
    if (this.clearAllForm.invalid) {
      this.clearAllForm.markAllAsTouched();
      return;
    }

    this.clearAllPending.set(true);
    const password = this.clearAllForm.controls.password.value;

    this.api.clearAllInvoices(password)
      .pipe(finalize(() => this.clearAllPending.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('All sales history deleted.');
          this.clearAllDialogOpen.set(false);
          this.detail.set(null);
          this.paymentOpen.set(false);
          this.pageNumber.set(1);
          this.reload();
          this.loadLookup();
        },
        error: (error) => this.toast.error(this.readError(error, 'Unable to clear sales history.'))
      });
  }

  isAdmin(): boolean {
    return this.auth.user()?.role === 'Admin';
  }

  private loadLookup(): void {
    this.api.getCustomers({ pageNumber: 1, pageSize: 500 }).subscribe({
      next: (result) => this.customers.set(result.items),
      error: () => this.toast.error('Failed to load customers.')
    });

    this.api.getAllVessels().subscribe({
      next: (result) => this.vessels.set(result),
      error: () => this.toast.error('Failed to load couriers.')
    });

    this.api.getDeliveryNotes({ pageNumber: 1, pageSize: 500 }).subscribe({
      next: (result) => this.deliveryNotes.set(result.items.filter((item) => !item.invoiceNo)),
      error: () => this.toast.error('Failed to load delivery notes.')
    });
  }

  private reload(): void {
    this.api.getInvoices({
      pageNumber: this.pageNumber(),
      pageSize: 10,
      search: this.search(),
      createdDatePreset: this.createdDatePreset()
    }).subscribe({
      next: (result) => this.invoices.set(result),
      error: () => this.toast.error('Failed to load invoices.')
    });
  }

  private createItemForm(item?: { description?: string; qty?: number; rate?: number }) {
    return this.fb.nonNullable.group({
      description: [item?.description || '', Validators.required],
      qty: [item?.qty ?? 1, [Validators.required, Validators.min(0.01)]],
      rate: [item?.rate ?? 0, [Validators.required, Validators.min(0)]]
    });
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private download(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  private readError(error: unknown, fallback: string): string {
    return extractApiError(error, fallback);
  }

  currentDateFilterLabel(): string {
    const selected = this.datePresetOptions.find((option) => option.value === this.createdDatePreset());
    return selected?.label ?? 'selected date range';
  }
}


