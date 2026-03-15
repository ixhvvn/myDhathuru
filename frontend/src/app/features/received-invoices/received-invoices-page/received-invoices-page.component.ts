import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppDateBadgeComponent } from '../../../shared/components/app-date-badge/app-date-badge.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AuthService } from '../../../core/services/auth.service';
import {
  ExpenseCategoryLookup,
  PagedResult,
  PaymentMethod,
  ReceivedInvoice,
  ReceivedInvoiceListItem,
  SupplierLookup
} from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { PortalApiService } from '../../services/portal-api.service';

@Component({
  selector: 'app-received-invoices-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppButtonComponent,
    AppCardComponent,
    AppConfirmDialogComponent,
    AppCurrencyPipe,
    AppDataTableComponent,
    AppDateBadgeComponent,
    AppPageHeaderComponent,
    AppSearchBarComponent
  ],
  template: `
    <app-page-header title="Received Invoices" subtitle="Track supplier invoices, attach scanned copies, and record settlements against live balances">
      <app-button (clicked)="openCreate()">New Received Invoice</app-button>
    </app-page-header>

    <app-card>
      <div class="toolbar">
        <app-search-bar [value]="search()" placeholder="Search supplier, invoice number, TIN, or outlet" (searchChange)="onSearchChange($event)"></app-search-bar>
        <div class="actions">
          <select [value]="selectedSupplierId()" (change)="selectedSupplierId.set(asValue($event)); refresh()">
            <option value="">All suppliers</option>
            <option *ngFor="let supplier of suppliers()" [value]="supplier.id">{{ supplier.name }}</option>
          </select>
          <select [value]="selectedCategoryId()" (change)="selectedCategoryId.set(asValue($event)); refresh()">
            <option value="">All categories</option>
            <option *ngFor="let category of categories()" [value]="category.id">{{ category.name }}</option>
          </select>
          <select [value]="statusFilter()" (change)="statusFilter.set(asValue($event)); refresh()">
            <option value="">All statuses</option>
            <option value="Unpaid">Unpaid</option>
            <option value="Partial">Partial</option>
            <option value="Paid">Paid</option>
            <option value="Overdue">Overdue</option>
          </select>
          <label class="checkbox"><input type="checkbox" [checked]="overdueOnly()" (change)="toggleOverdue($event)"> Overdue only</label>
        </div>
      </div>

      <app-data-table [hasData]="(page()?.items?.length ?? 0) > 0" emptyTitle="No received invoices" emptyDescription="Create a supplier invoice to track AP balances and expense reporting.">
        <thead>
          <tr>
            <th>Invoice No</th>
            <th>Supplier</th>
            <th>Invoice Date</th>
            <th>Due Date</th>
            <th>Category</th>
            <th>Total</th>
            <th>Balance</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let item of page()?.items">
            <td>{{ item.invoiceNumber }}</td>
            <td>{{ item.supplierName }}</td>
            <td><app-date-badge [value]="item.invoiceDate"></app-date-badge></td>
            <td><app-date-badge [value]="item.dueDate"></app-date-badge></td>
            <td>{{ item.expenseCategoryName }}</td>
            <td>{{ item.totalAmount | appCurrency:item.currency }}</td>
            <td>{{ item.balanceDue | appCurrency:item.currency }}</td>
            <td>{{ item.paymentStatus }}</td>
            <td class="actions">
              <app-button size="sm" variant="secondary" (clicked)="view(item)">View</app-button>
              <app-button size="sm" variant="secondary" (clicked)="edit(item)">Edit</app-button>
              <app-button size="sm" variant="secondary" (clicked)="openPayment(item)">Payment</app-button>
              <app-button *ngIf="isAdmin()" size="sm" variant="danger" (clicked)="confirmDelete(item)">Delete</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>
    </app-card>

    <div class="drawer" *ngIf="formOpen()">
      <app-card>
        <h3>{{ editId() ? 'Edit Received Invoice' : 'New Received Invoice' }}</h3>
        <form [formGroup]="form" (ngSubmit)="save()" class="form-grid">
          <div class="three-col">
            <label>Invoice Number <input type="text" formControlName="invoiceNumber"></label>
            <label>Invoice Date <input type="date" formControlName="invoiceDate"></label>
            <label>Due Date <input type="date" formControlName="dueDate"></label>
          </div>
          <div class="three-col">
            <label>Supplier
              <select formControlName="supplierId">
                <option value="">Select supplier</option>
                <option *ngFor="let supplier of suppliers()" [value]="supplier.id">{{ supplier.name }}</option>
              </select>
            </label>
            <label>Expense Category
              <select formControlName="expenseCategoryId">
                <option value="">Select category</option>
                <option *ngFor="let category of categories()" [value]="category.id">{{ category.name }}</option>
              </select>
            </label>
            <label>Currency
              <select formControlName="currency">
                <option value="MVR">MVR</option>
                <option value="USD">USD</option>
              </select>
            </label>
          </div>
          <div class="three-col">
            <label>Discount Amount <input type="number" formControlName="discountAmount" step="0.01"></label>
            <label>Default GST Rate <input type="number" formControlName="gstRate" step="0.01"></label>
            <label>Approval Status
              <select formControlName="approvalStatus">
                <option value="Approved">Approved</option>
                <option value="Draft">Draft</option>
                <option value="Rejected">Rejected</option>
              </select>
            </label>
          </div>
          <div class="three-col">
            <label>Outlet <input type="text" formControlName="outlet"></label>
            <label>Payment Method
              <select formControlName="paymentMethod">
                <option value="">Optional</option>
                <option *ngFor="let option of paymentMethods" [value]="option">{{ option }}</option>
              </select>
            </label>
            <label>MIRA Taxable Activity No <input type="text" formControlName="miraTaxableActivityNumber"></label>
          </div>
          <div class="three-col">
            <label>Receipt Reference <input type="text" formControlName="receiptReference"></label>
            <label>Settlement Reference <input type="text" formControlName="settlementReference"></label>
            <label>Revenue / Capital
              <select formControlName="revenueCapitalClassification">
                <option value="Revenue">Revenue</option>
                <option value="Capital">Capital</option>
              </select>
            </label>
          </div>
          <div class="two-col">
            <label>Bank Name <input type="text" formControlName="bankName"></label>
            <label>Bank Account Details <input type="text" formControlName="bankAccountDetails"></label>
          </div>
          <label>Description <textarea rows="2" formControlName="description"></textarea></label>
          <label>Notes <textarea rows="3" formControlName="notes"></textarea></label>
          <label class="checkbox"><input type="checkbox" formControlName="isTaxClaimable"> Claimable input tax</label>

          <div class="section-head">
            <h4>Items</h4>
            <app-button size="sm" variant="secondary" (clicked)="addItem()">Add Item</app-button>
          </div>

          <div formArrayName="items" class="item-grid">
            <div *ngFor="let item of items.controls; let i = index" [formGroupName]="i" class="item-row">
              <label class="item-field">
                <span>Description</span>
                <input type="text" formControlName="description" placeholder="Description">
              </label>
              <label class="item-field">
                <span>UOM</span>
                <input type="text" formControlName="uom" placeholder="UOM">
              </label>
              <label class="item-field">
                <span>Qty</span>
                <input type="number" formControlName="qty" placeholder="Qty" step="0.01">
              </label>
              <label class="item-field">
                <span>Rate</span>
                <input type="number" formControlName="rate" placeholder="Rate" step="0.01">
              </label>
              <label class="item-field">
                <span>Discount</span>
                <input type="number" formControlName="discountAmount" placeholder="Discount" step="0.01">
              </label>
              <label class="item-field">
                <span>GST Rate</span>
                <input type="number" formControlName="gstRate" placeholder="GST Rate" step="0.01">
              </label>
              <div class="item-actions">
                <app-button size="sm" variant="danger" (clicked)="removeItem(i)" [disabled]="items.length === 1">Remove</app-button>
              </div>
            </div>
          </div>

          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closeForm()">Cancel</app-button>
            <app-button type="submit">Save</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <div class="drawer" *ngIf="detail() as invoice">
      <app-card>
        <h3>Received Invoice {{ invoice.invoiceNumber }}</h3>
        <p><strong>Supplier:</strong> {{ invoice.supplierName }} | {{ invoice.supplierTin || '-' }}</p>
        <p><strong>Invoice Date:</strong> {{ invoice.invoiceDate }} | <strong>Due Date:</strong> {{ invoice.dueDate }}</p>
        <p><strong>Category:</strong> {{ invoice.expenseCategoryName }} | <strong>Status:</strong> {{ invoice.paymentStatus }}</p>
        <p><strong>Total:</strong> {{ invoice.totalAmount | appCurrency:invoice.currency }} | <strong>Balance:</strong> {{ invoice.balanceDue | appCurrency:invoice.currency }}</p>
        <p><strong>Taxable Activity:</strong> {{ invoice.miraTaxableActivityNumber || '-' }}</p>
        <p><strong>Description:</strong> {{ invoice.description || '-' }}</p>
        <p><strong>Notes:</strong> {{ invoice.notes || '-' }}</p>

        <h4>Items</h4>
        <div class="detail-row" *ngFor="let item of invoice.items">
          <span>{{ item.description }} ({{ item.qty }} {{ item.uom || '' }})</span>
          <strong>{{ (item.lineTotal || 0) | appCurrency:invoice.currency }}</strong>
        </div>

        <h4>Payments</h4>
        <div class="detail-row" *ngFor="let payment of invoice.payments">
          <span>{{ payment.paymentDate }} - {{ payment.method }} {{ payment.reference ? '(' + payment.reference + ')' : '' }}</span>
          <strong>{{ payment.amount | appCurrency:invoice.currency }}</strong>
        </div>
        <p *ngIf="invoice.payments.length === 0">No payments recorded.</p>

        <div class="attachment-box">
          <div class="attachment-head">
            <h4>Attachments</h4>
            <div class="actions">
              <input #attachmentInput type="file" class="hidden-input" accept=".pdf,.png,.jpg,.jpeg,.webp,.gif" (change)="onAttachmentSelected($event)">
              <app-button size="sm" variant="secondary" (clicked)="openAttachmentPicker()">Upload</app-button>
            </div>
          </div>
          <div class="detail-row" *ngFor="let attachment of invoice.attachments">
            <span>{{ attachment.fileName }}</span>
            <app-button size="sm" variant="secondary" (clicked)="viewAttachment(invoice.id, attachment.id)">View</app-button>
          </div>
          <p *ngIf="invoice.attachments.length === 0">No attachments uploaded.</p>
        </div>

        <div class="form-actions">
          <app-button variant="secondary" (clicked)="detail.set(null)">Close</app-button>
        </div>
      </app-card>
    </div>

    <div class="drawer" *ngIf="paymentTarget()">
      <app-card>
        <h3>Record Supplier Payment</h3>
        <form [formGroup]="paymentForm" (ngSubmit)="savePayment()" class="form-grid">
          <div class="three-col">
            <label>Payment Date <input type="date" formControlName="paymentDate"></label>
            <label>Amount <input type="number" formControlName="amount" step="0.01"></label>
            <label>Method
              <select formControlName="method">
                <option *ngFor="let option of paymentMethods" [value]="option">{{ option }}</option>
              </select>
            </label>
          </div>
          <div class="two-col">
            <label>Reference <input type="text" formControlName="reference"></label>
            <label>Notes <input type="text" formControlName="notes"></label>
          </div>
          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closePayment()">Cancel</app-button>
            <app-button type="submit">Save Payment</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <app-confirm-dialog
      [open]="deleteDialogOpen()"
      title="Delete received invoice"
      message="This permanently deletes the supplier invoice, its items, payments, and attachments from the database."
      (cancel)="closeDeleteDialog()"
      (confirm)="deleteInvoice()">
    </app-confirm-dialog>
  `,
  styles: `
    .toolbar, .actions, .three-col, .two-col, .form-actions, .section-head {
      display: flex;
      gap: .75rem;
      align-items: center;
      flex-wrap: wrap;
    }
    .toolbar, .section-head {
      justify-content: space-between;
      margin-bottom: .85rem;
    }
    .checkbox {
      display: flex;
      gap: .55rem;
      align-items: center;
    }
    .checkbox input, .hidden-input {
      width: auto;
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
      width: min(1080px, 100%);
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
    .item-grid {
      display: grid;
      gap: .7rem;
    }
    .item-row {
      display: grid;
      grid-template-columns: 2.4fr .9fr repeat(4, 1fr) auto;
      gap: .6rem;
      align-items: end;
    }
    .item-field {
      display: grid;
      gap: .3rem;
      color: #536b98;
      font-size: .82rem;
      font-weight: 500;
    }
    .item-actions {
      display: flex;
      align-items: end;
    }
    .detail-row {
      display: flex;
      justify-content: space-between;
      gap: .75rem;
      padding: .5rem 0;
      border-bottom: 1px solid #edf1fb;
    }
    .attachment-box {
      margin-top: 1rem;
      padding: 1rem;
      border: 1px solid #dbe5fa;
      border-radius: 18px;
      background: #f9fbff;
    }
    .attachment-head {
      display: flex;
      justify-content: space-between;
      gap: .75rem;
      align-items: center;
      margin-bottom: .5rem;
    }
    .form-actions, .actions {
      justify-content: flex-end;
    }
  `
})
export class ReceivedInvoicesPageComponent implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  @ViewChild('attachmentInput') private attachmentInput?: ElementRef<HTMLInputElement>;

  readonly page = signal<PagedResult<ReceivedInvoiceListItem> | null>(null);
  readonly suppliers = signal<SupplierLookup[]>([]);
  readonly categories = signal<ExpenseCategoryLookup[]>([]);
  readonly search = signal('');
  readonly selectedSupplierId = signal('');
  readonly selectedCategoryId = signal('');
  readonly statusFilter = signal('');
  readonly overdueOnly = signal(false);
  readonly formOpen = signal(false);
  readonly editId = signal<string | null>(null);
  readonly detail = signal<ReceivedInvoice | null>(null);
  readonly paymentTarget = signal<ReceivedInvoiceListItem | null>(null);
  readonly deleteDialogOpen = signal(false);
  readonly deleting = signal<ReceivedInvoiceListItem | null>(null);
  readonly paymentMethods: PaymentMethod[] = ['Transfer', 'Cash', 'Card', 'Cheque'];

  readonly form = this.fb.nonNullable.group({
    supplierId: ['', Validators.required],
    invoiceNumber: ['', [Validators.required, Validators.maxLength(60)]],
    invoiceDate: [new Date().toISOString().slice(0, 10), Validators.required],
    dueDate: [new Date().toISOString().slice(0, 10), Validators.required],
    outlet: ['', Validators.maxLength(160)],
    description: ['', Validators.maxLength(500)],
    notes: ['', Validators.maxLength(2000)],
    currency: 'MVR',
    discountAmount: 0,
    gstRate: 0.08,
    paymentMethod: '',
    receiptReference: ['', Validators.maxLength(120)],
    settlementReference: ['', Validators.maxLength(120)],
    bankName: ['', Validators.maxLength(120)],
    bankAccountDetails: ['', Validators.maxLength(200)],
    miraTaxableActivityNumber: ['', Validators.maxLength(50)],
    revenueCapitalClassification: 'Revenue',
    expenseCategoryId: ['', Validators.required],
    isTaxClaimable: true,
    approvalStatus: 'Approved',
    items: this.fb.array([this.createItemGroup()])
  });

  readonly paymentForm = this.fb.nonNullable.group({
    paymentDate: [new Date().toISOString().slice(0, 10), Validators.required],
    amount: 0,
    method: 'Transfer',
    reference: ['', Validators.maxLength(120)],
    notes: ['', Validators.maxLength(1000)]
  });

  ngOnInit(): void {
    this.loadLookups();
    this.refresh();
  }

  get items(): FormArray {
    return this.form.controls.items as FormArray;
  }

  isAdmin(): boolean {
    return this.auth.user()?.role === 'Admin';
  }

  onSearchChange(value: string): void {
    this.search.set(value);
    this.refresh();
  }

  toggleOverdue(event: Event): void {
    this.overdueOnly.set((event.target as HTMLInputElement).checked);
    this.refresh();
  }

  openCreate(): void {
    this.editId.set(null);
    this.form.reset({
      supplierId: '',
      invoiceNumber: '',
      invoiceDate: new Date().toISOString().slice(0, 10),
      dueDate: new Date().toISOString().slice(0, 10),
      outlet: '',
      description: '',
      notes: '',
      currency: 'MVR',
      discountAmount: 0,
      gstRate: 0.08,
      paymentMethod: '',
      receiptReference: '',
      settlementReference: '',
      bankName: '',
      bankAccountDetails: '',
      miraTaxableActivityNumber: '',
      revenueCapitalClassification: 'Revenue',
      expenseCategoryId: '',
      isTaxClaimable: true,
      approvalStatus: 'Approved'
    });
    this.resetItems([]);
    this.formOpen.set(true);
  }

  edit(item: ReceivedInvoiceListItem): void {
    this.api.getReceivedInvoiceById(item.id).subscribe({
      next: (invoice) => {
        this.editId.set(invoice.id);
        this.form.reset({
          supplierId: invoice.supplierId,
          invoiceNumber: invoice.invoiceNumber,
          invoiceDate: invoice.invoiceDate,
          dueDate: invoice.dueDate,
          outlet: invoice.outlet ?? '',
          description: invoice.description ?? '',
          notes: invoice.notes ?? '',
          currency: invoice.currency,
          discountAmount: invoice.discountAmount,
          gstRate: invoice.gstRate,
          paymentMethod: invoice.paymentMethod ?? '',
          receiptReference: invoice.receiptReference ?? '',
          settlementReference: invoice.settlementReference ?? '',
          bankName: invoice.bankName ?? '',
          bankAccountDetails: invoice.bankAccountDetails ?? '',
          miraTaxableActivityNumber: invoice.miraTaxableActivityNumber ?? '',
          revenueCapitalClassification: invoice.revenueCapitalClassification,
          expenseCategoryId: invoice.expenseCategoryId,
          isTaxClaimable: invoice.isTaxClaimable,
          approvalStatus: invoice.approvalStatus
        });
        this.resetItems(invoice.items);
        this.formOpen.set(true);
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  view(item: ReceivedInvoiceListItem): void {
    this.api.getReceivedInvoiceById(item.id).subscribe({
      next: (invoice) => this.detail.set(invoice),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  openPayment(item: ReceivedInvoiceListItem): void {
    this.paymentTarget.set(item);
    this.paymentForm.reset({
      paymentDate: new Date().toISOString().slice(0, 10),
      amount: item.balanceDue,
      method: 'Transfer',
      reference: '',
      notes: ''
    });
  }

  closePayment(): void {
    this.paymentTarget.set(null);
  }

  savePayment(): void {
    const target = this.paymentTarget();
    if (!target || this.paymentForm.invalid) {
      return;
    }

    this.api.recordReceivedInvoicePayment(target.id, this.paymentForm.getRawValue()).subscribe({
      next: () => {
        this.toast.success('Supplier payment recorded.');
        this.closePayment();
        this.refresh();
        if (this.detail()?.id === target.id) {
          this.view(target);
        }
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  openAttachmentPicker(): void {
    this.attachmentInput?.nativeElement.click();
  }

  onAttachmentSelected(event: Event): void {
    const invoice = this.detail();
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!invoice || !file) {
      return;
    }

    this.api.uploadReceivedInvoiceAttachment(invoice.id, file).subscribe({
      next: () => {
        this.toast.success('Attachment uploaded.');
        this.api.getReceivedInvoiceById(invoice.id).subscribe({
          next: (updated) => this.detail.set(updated),
          error: (error) => this.toast.error(extractApiError(error))
        });
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  viewAttachment(invoiceId: string, attachmentId: string): void {
    this.api.viewReceivedInvoiceAttachment(invoiceId, attachmentId).subscribe({
      next: (blob) => window.open(URL.createObjectURL(blob), '_blank'),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  addItem(): void {
    this.items.push(this.createItemGroup());
  }

  removeItem(index: number): void {
    if (this.items.length === 1) {
      return;
    }

    this.items.removeAt(index);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload = {
      ...this.form.getRawValue(),
      paymentMethod: this.form.getRawValue().paymentMethod || null
    };

    const request$ = this.editId()
      ? this.api.updateReceivedInvoice(this.editId()!, payload)
      : this.api.createReceivedInvoice(payload);

    request$.subscribe({
      next: () => {
        this.toast.success(this.editId() ? 'Received invoice updated.' : 'Received invoice created.');
        this.formOpen.set(false);
        this.refresh();
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  confirmDelete(item: ReceivedInvoiceListItem): void {
    this.deleting.set(item);
    this.deleteDialogOpen.set(true);
  }

  closeDeleteDialog(): void {
    this.deleteDialogOpen.set(false);
    this.deleting.set(null);
  }

  deleteInvoice(): void {
    const item = this.deleting();
    if (!item) {
      return;
    }

    this.api.deleteReceivedInvoice(item.id).subscribe({
      next: () => {
        this.toast.success('Received invoice deleted.');
        this.closeDeleteDialog();
        this.refresh();
      },
      error: (error) => {
        this.toast.error(extractApiError(error));
        this.closeDeleteDialog();
      }
    });
  }

  refresh(): void {
    this.api.getReceivedInvoices({
      pageNumber: 1,
      pageSize: 100,
      search: this.search() || undefined,
      supplierId: this.selectedSupplierId() || undefined,
      expenseCategoryId: this.selectedCategoryId() || undefined,
      paymentStatus: this.statusFilter() || undefined,
      overdueOnly: this.overdueOnly()
    }).subscribe({
      next: (page) => this.page.set(page),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  loadLookups(): void {
    this.api.getSupplierLookup().subscribe({
      next: (suppliers) => this.suppliers.set(suppliers),
      error: (error) => this.toast.error(extractApiError(error))
    });

    this.api.getExpenseCategoryLookup().subscribe({
      next: (categories) => this.categories.set(categories),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  private createItemGroup(item?: Partial<ReceivedInvoice['items'][number]>) {
    return this.fb.nonNullable.group({
      description: [item?.description ?? '', [Validators.required, Validators.maxLength(400)]],
      uom: [item?.uom ?? '', Validators.maxLength(50)],
      qty: item?.qty ?? 1,
      rate: item?.rate ?? 0,
      discountAmount: item?.discountAmount ?? 0,
      gstRate: item?.gstRate ?? 0
    });
  }

  private resetItems(items: ReceivedInvoice['items']): void {
    while (this.items.length) {
      this.items.removeAt(0);
    }

    if (items.length === 0) {
      this.items.push(this.createItemGroup());
      return;
    }

    items.forEach((item) => this.items.push(this.createItemGroup(item)));
  }
  asValue(event: Event): string {
    return (event.target as HTMLSelectElement).value;
  }
}
