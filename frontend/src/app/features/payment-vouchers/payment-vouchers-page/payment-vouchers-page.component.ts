import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppDateBadgeComponent } from '../../../shared/components/app-date-badge/app-date-badge.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { ExpenseLedgerRow, PagedResult, PaymentVoucher, PaymentVoucherListItem, ReceivedInvoiceListItem } from '../../../core/models/app.models';
import { extractApiError } from '../../../core/utils/api-error.util';
import { PortalApiService } from '../../services/portal-api.service';

@Component({
  selector: 'app-payment-vouchers-page',
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
    AppSearchBarComponent
  ],
  template: `
    <app-page-header title="Payment Vouchers" subtitle="Create, approve, post, and print supplier-side payment vouchers from live records">
      <app-button (clicked)="openCreate()">New Voucher</app-button>
    </app-page-header>

    <app-card>
      <div class="toolbar">
        <app-search-bar [value]="search()" placeholder="Search voucher, payee, details, or bank" (searchChange)="onSearchChange($event)"></app-search-bar>
        <select [value]="statusFilter()" (change)="statusFilter.set(asValue($event)); refresh()">
          <option value="">All statuses</option>
          <option value="Draft">Draft</option>
          <option value="Approved">Approved</option>
          <option value="Posted">Posted</option>
          <option value="Cancelled">Cancelled</option>
        </select>
      </div>

      <app-data-table [hasData]="(page()?.items?.length ?? 0) > 0" emptyTitle="No payment vouchers" emptyDescription="Create vouchers manually or link them to received invoices and manual expenses.">
        <thead>
          <tr>
            <th>Voucher No</th>
            <th>Date</th>
            <th>Pay To</th>
            <th>Method</th>
            <th>Amount</th>
            <th>Status</th>
            <th>Linked Invoice</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let voucher of page()?.items">
            <td>{{ voucher.voucherNumber }}</td>
            <td><app-date-badge [value]="voucher.date"></app-date-badge></td>
            <td>{{ voucher.payTo }}</td>
            <td>{{ voucher.paymentMethod }}</td>
            <td>{{ voucher.amount | appCurrency:'MVR' }}</td>
            <td>{{ voucher.status }}</td>
            <td>{{ voucher.linkedReceivedInvoiceNumber || '-' }}</td>
            <td class="actions">
              <app-button size="sm" variant="secondary" (clicked)="edit(voucher)">Edit</app-button>
              <app-button size="sm" variant="secondary" (clicked)="exportPdf(voucher)">PDF</app-button>
              <app-button *ngIf="isAdmin() && voucher.status === 'Draft'" size="sm" variant="secondary" (clicked)="approve(voucher)">Approve</app-button>
              <app-button *ngIf="isAdmin() && voucher.status === 'Approved'" size="sm" variant="secondary" (clicked)="post(voucher)">Post</app-button>
              <app-button *ngIf="isAdmin() && voucher.status !== 'Cancelled'" size="sm" variant="danger" (clicked)="cancel(voucher)">Cancel</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>
    </app-card>

    <div class="drawer" *ngIf="formOpen()">
      <app-card>
        <h3>{{ editId() ? 'Edit Payment Voucher' : 'New Payment Voucher' }}</h3>
        <form [formGroup]="form" (ngSubmit)="save()" class="form-grid">
          <div class="three-col">
            <label>Date <input type="date" formControlName="date"></label>
            <label>Pay To <input type="text" formControlName="payTo"></label>
            <label>Payment Method
              <select formControlName="paymentMethod">
                <option value="Transfer">Transfer</option>
                <option value="Cash">Cash</option>
                <option value="Card">Card</option>
                <option value="Cheque">Cheque</option>
              </select>
            </label>
          </div>
          <div class="three-col">
            <label>Amount <input type="number" formControlName="amount" step="0.01"></label>
            <label>Bank <input type="text" formControlName="bank"></label>
            <label>Account Number <input type="text" formControlName="accountNumber"></label>
          </div>
          <div class="three-col">
            <label>Cheque Number <input type="text" formControlName="chequeNumber"></label>
            <label>Approved By <input type="text" formControlName="approvedBy"></label>
            <label>Received By <input type="text" formControlName="receivedBy"></label>
          </div>
          <div class="two-col">
            <label>Linked Received Invoice
              <select formControlName="linkedReceivedInvoiceId">
                <option value="">Optional link</option>
                <option *ngFor="let item of receivedInvoices()" [value]="item.id">{{ item.invoiceNumber }} - {{ item.supplierName }}</option>
              </select>
            </label>
            <label>Linked Manual Expense
              <select formControlName="linkedExpenseEntryId">
                <option value="">Optional link</option>
                <option *ngFor="let item of manualEntries()" [value]="item.sourceId">{{ item.documentNumber }} - {{ item.payeeName }}</option>
              </select>
            </label>
          </div>
          <label>Details <textarea rows="3" formControlName="details"></textarea></label>
          <label>Notes <textarea rows="3" formControlName="notes"></textarea></label>
          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closeForm()">Cancel</app-button>
            <app-button type="submit">Save</app-button>
          </div>
        </form>
      </app-card>
    </div>
  `,
  styles: `
    .toolbar, .three-col, .two-col, .form-actions, .actions {
      display: flex;
      gap: .75rem;
      align-items: center;
      flex-wrap: wrap;
    }
    .toolbar {
      justify-content: space-between;
      margin-bottom: .85rem;
    }
    .drawer {
      position: fixed;
      inset: 0;
      background: rgba(26, 38, 68, .18);
      display: grid;
      place-items: center;
      padding: 1rem;
      z-index: 40;
    }
    .drawer app-card {
      width: min(920px, 100%);
      max-height: calc(100vh - 2rem);
      overflow: auto;
    }
    .form-grid {
      display: grid;
      gap: .9rem;
    }
    .three-col > label {
      flex: 1 1 220px;
    }
    .two-col > label {
      flex: 1 1 320px;
    }
    label {
      display: grid;
      gap: .35rem;
      color: #536b98;
      font-weight: 500;
    }
    input, select, textarea {
      width: 100%;
      border: 1px solid #d7e0f3;
      border-radius: 14px;
      padding: .8rem .95rem;
      background: rgba(255,255,255,.92);
      font: inherit;
    }
    .actions, .form-actions {
      justify-content: flex-end;
    }
  `
})
export class PaymentVouchersPageComponent implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly page = signal<PagedResult<PaymentVoucherListItem> | null>(null);
  readonly search = signal('');
  readonly statusFilter = signal('');
  readonly receivedInvoices = signal<ReceivedInvoiceListItem[]>([]);
  readonly manualEntries = signal<ExpenseLedgerRow[]>([]);
  readonly formOpen = signal(false);
  readonly editId = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    date: [new Date().toISOString().slice(0, 10), Validators.required],
    payTo: ['', [Validators.required, Validators.maxLength(200)]],
    details: ['', [Validators.required, Validators.maxLength(500)]],
    paymentMethod: 'Transfer',
    accountNumber: ['', Validators.maxLength(120)],
    chequeNumber: ['', Validators.maxLength(120)],
    bank: ['', Validators.maxLength(120)],
    amount: 0,
    approvedBy: ['', Validators.maxLength(160)],
    receivedBy: ['', Validators.maxLength(160)],
    linkedReceivedInvoiceId: '',
    linkedExpenseEntryId: '',
    notes: ['', Validators.maxLength(2000)],
    status: 'Draft'
  });

  ngOnInit(): void {
    this.loadLookups();
    this.refresh();
  }

  isAdmin(): boolean {
    return this.auth.user()?.role === 'Admin';
  }

  onSearchChange(value: string): void {
    this.search.set(value);
    this.refresh();
  }

  openCreate(): void {
    this.editId.set(null);
    this.form.reset({
      date: new Date().toISOString().slice(0, 10),
      payTo: '',
      details: '',
      paymentMethod: 'Transfer',
      accountNumber: '',
      chequeNumber: '',
      bank: '',
      amount: 0,
      approvedBy: '',
      receivedBy: '',
      linkedReceivedInvoiceId: '',
      linkedExpenseEntryId: '',
      notes: '',
      status: 'Draft'
    });
    this.formOpen.set(true);
  }

  edit(voucher: PaymentVoucherListItem): void {
    this.api.getPaymentVoucherById(voucher.id).subscribe({
      next: (detail: PaymentVoucher) => {
        this.editId.set(detail.id);
        this.form.reset({
          date: detail.date,
          payTo: detail.payTo,
          details: detail.details,
          paymentMethod: detail.paymentMethod,
          accountNumber: detail.accountNumber ?? '',
          chequeNumber: detail.chequeNumber ?? '',
          bank: detail.bank ?? '',
          amount: detail.amount,
          approvedBy: detail.approvedBy ?? '',
          receivedBy: detail.receivedBy ?? '',
          linkedReceivedInvoiceId: detail.linkedReceivedInvoiceId ?? '',
          linkedExpenseEntryId: detail.linkedExpenseEntryId ?? '',
          notes: detail.notes ?? '',
          status: detail.status
        });
        this.formOpen.set(true);
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload = this.form.getRawValue();
    const request$ = this.editId()
      ? this.api.updatePaymentVoucher(this.editId()!, payload)
      : this.api.createPaymentVoucher(payload);

    request$.subscribe({
      next: () => {
        this.toast.success(this.editId() ? 'Payment voucher updated.' : 'Payment voucher created.');
        this.formOpen.set(false);
        this.refresh();
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  approve(voucher: PaymentVoucherListItem): void {
    this.api.approvePaymentVoucher(voucher.id).subscribe({
      next: () => {
        this.toast.success('Payment voucher approved.');
        this.refresh();
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  post(voucher: PaymentVoucherListItem): void {
    this.api.postPaymentVoucher(voucher.id).subscribe({
      next: () => {
        this.toast.success('Payment voucher posted.');
        this.refresh();
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  cancel(voucher: PaymentVoucherListItem): void {
    this.api.cancelPaymentVoucher(voucher.id).subscribe({
      next: () => {
        this.toast.success('Payment voucher cancelled.');
        this.refresh();
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  exportPdf(voucher: PaymentVoucherListItem): void {
    this.api.exportPaymentVoucher(voucher.id).subscribe({
      next: (blob) => window.open(URL.createObjectURL(blob), '_blank'),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  refresh(): void {
    this.api.getPaymentVouchers({
      pageNumber: 1,
      pageSize: 100,
      search: this.search() || undefined,
      status: this.statusFilter() || undefined
    }).subscribe({
      next: (page) => this.page.set(page),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  loadLookups(): void {
    this.api.getReceivedInvoices({ pageNumber: 1, pageSize: 100 }).subscribe({
      next: (page) => this.receivedInvoices.set(page.items),
      error: (error) => this.toast.error(extractApiError(error))
    });

    this.api.getExpenseLedger({ pageNumber: 1, pageSize: 100, sourceType: 'Manual' }).subscribe({
      next: (page) => this.manualEntries.set(page.items),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  asValue(event: Event): string {
    return (event.target as HTMLSelectElement).value;
  }
}
