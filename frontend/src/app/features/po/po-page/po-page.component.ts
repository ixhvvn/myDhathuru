import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppDateBadgeComponent } from '../../../shared/components/app-date-badge/app-date-badge.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { PagedResult, PurchaseOrder, PurchaseOrderListItem, SupplierLookup, Vessel } from '../../../core/models/app.models';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { PortalApiService } from '../../services/portal-api.service';

type DatePreset =
  | ''
  | 'today'
  | 'yesterday'
  | 'last-7-days'
  | 'last-week'
  | 'last-month'
  | 'last-30-days'
  | 'custom-range';

@Component({
  selector: 'app-po-page',
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
    <app-page-header title="PO" subtitle="Create and export purchase orders">
      <app-button (clicked)="openCreate()">New PO</app-button>
    </app-page-header>

    <app-card>
      <div class="toolbar">
        <div class="toolbar-left">
          <label class="page-size">
            <span>Show</span>
            <select [value]="pageSize()" (change)="onPageSizeChange($event)">
              <option *ngFor="let option of pageSizeOptions" [value]="option">{{ option }}</option>
            </select>
          </label>
          <app-button variant="secondary" (clicked)="toggleFilterPanel()">{{ appliedDateLabel() ? 'Filter Applied' : 'Filter' }}</app-button>
        </div>

        <div class="search-tools">
          <app-search-bar
            [value]="searchDraft()"
            placeholder="Search PO or supplier"
            (searchChange)="onSearchDraftChange($event)">
          </app-search-bar>
          <app-button variant="secondary" (clicked)="applySearch()">Search</app-button>
        </div>
      </div>

      <div class="filter-panel" *ngIf="filterPanelOpen()">
        <label>
          Date Filter
          <select [value]="selectedDatePreset()" (change)="onDatePresetChange($event)">
            <option value="">All Dates</option>
            <option *ngFor="let option of datePresetOptions" [value]="option.value">{{ option.label }}</option>
          </select>
        </label>

        <div class="custom-range" *ngIf="selectedDatePreset() === 'custom-range'">
          <label>Start Date <input type="date" [value]="customStartDate()" (input)="onCustomStartDateChange($event)"></label>
          <label>End Date <input type="date" [value]="customEndDate()" (input)="onCustomEndDateChange($event)"></label>
        </div>

        <div class="filter-actions">
          <app-button variant="secondary" (clicked)="clearFilters()">Clear</app-button>
          <app-button (clicked)="applyFilters()">Apply Filter</app-button>
        </div>
      </div>

      <div class="summary" *ngIf="purchaseOrders() as page">
        Total: {{ page.totalCount }}.
        <span *ngIf="appliedDateLabel()"> Date filter: {{ appliedDateLabel() }}.</span>
        <span *ngIf="search()"> Search: "{{ search() }}".</span>
      </div>

      <app-data-table [hasData]="(purchaseOrders()?.items?.length ?? 0) > 0" emptyTitle="No purchase orders" emptyDescription="Create your first PO.">
        <thead>
          <tr>
            <th>PO No</th>
            <th>Supplier</th>
            <th>Courier</th>
            <th>Currency</th>
            <th>Amount</th>
            <th>Date Issued</th>
            <th>Required Date</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let purchaseOrder of purchaseOrders()?.items">
            <td>{{ purchaseOrder.purchaseOrderNo }}</td>
            <td>{{ purchaseOrder.supplier }}</td>
            <td>{{ purchaseOrder.courierName || '-' }}</td>
            <td>{{ purchaseOrder.currency }}</td>
            <td>{{ purchaseOrder.amount | appCurrency: purchaseOrder.currency }}</td>
            <td><app-date-badge [value]="purchaseOrder.dateIssued"></app-date-badge></td>
            <td><app-date-badge [value]="purchaseOrder.requiredDate"></app-date-badge></td>
            <td class="actions">
              <app-button size="sm" variant="secondary" (clicked)="openDetail(purchaseOrder)">View</app-button>
              <app-button size="sm" variant="secondary" (clicked)="edit(purchaseOrder)">Edit</app-button>
              <app-button *ngIf="isAdmin()" size="sm" variant="danger" (clicked)="confirmDelete(purchaseOrder)">Delete</app-button>
              <app-button size="sm" variant="secondary" (clicked)="exportPurchaseOrder(purchaseOrder)">PDF</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>

      <div class="pager" *ngIf="purchaseOrders() as page">
        <span>Showing {{ showingRangeLabel(page) }} | Page {{ page.pageNumber }} of {{ page.totalPages || 1 }}</span>
        <div>
          <app-button size="sm" variant="secondary" [disabled]="page.pageNumber <= 1" (clicked)="changePage(page.pageNumber - 1)">Prev</app-button>
          <app-button size="sm" variant="secondary" [disabled]="page.pageNumber >= page.totalPages" (clicked)="changePage(page.pageNumber + 1)">Next</app-button>
        </div>
      </div>
    </app-card>

    <div class="drawer" *ngIf="formOpen()">
      <app-card>
        <h3>{{ editId() ? 'Edit PO' : 'New PO' }}</h3>
        <form [formGroup]="form" (ngSubmit)="save()" class="form-grid">
          <div class="two-col">
            <label>Supplier
              <select formControlName="supplierId">
                <option value="">Select supplier</option>
                <option *ngFor="let supplier of suppliers()" [value]="supplier.id">{{ supplier.name }}</option>
              </select>
            </label>
            <label>Courier
              <select formControlName="courierId">
                <option value="">Select courier</option>
                <option *ngFor="let vessel of vessels()" [value]="vessel.id">{{ vessel.name }}</option>
              </select>
            </label>
          </div>

          <div class="three-col">
            <label>Date Issued <input type="date" formControlName="dateIssued"></label>
            <label>Required Date <input type="date" formControlName="requiredDate"></label>
            <label>Currency
              <select formControlName="currency">
                <option value="MVR">MVR</option>
                <option value="USD">USD</option>
              </select>
            </label>
          </div>

          <div class="two-col">
            <ng-container *ngIf="taxApplicable(); else taxDisabledState">
              <label>Tax Rate <input type="number" formControlName="taxRate" step="0.01"></label>
            </ng-container>
            <div class="required-callout">
              <strong>Delivery Target</strong>
              <span>Add a required date so it prints in the PDF.</span>
            </div>
          </div>

          <ng-template #taxDisabledState>
            <div class="tax-callout">
              <strong>Tax disabled</strong>
              <span>POs are saved and exported without tax.</span>
            </div>
          </ng-template>

          <label>Notes <textarea rows="3" formControlName="notes"></textarea></label>

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
            <app-button variant="secondary" (clicked)="closeForm()">Cancel</app-button>
            <app-button type="submit">Save</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <div class="drawer" *ngIf="detail()">
      <app-card>
        <h3>PO {{ detail()?.purchaseOrderNo }}</h3>
        <p><strong>Supplier:</strong> {{ detail()?.supplierName }}</p>
        <p><strong>Contact:</strong> {{ detail()?.supplierContactNumber || '-' }} | {{ detail()?.supplierEmail || '-' }}</p>
        <p><strong>Courier:</strong> {{ detail()?.courierName || '-' }}</p>
        <p><strong>Currency:</strong> {{ detail()?.currency }}</p>
        <p><strong>Issued:</strong> {{ detail()?.dateIssued }} | <strong>Required Date:</strong> {{ detail()?.requiredDate }}</p>
        <p *ngIf="taxApplicable()"><strong>Subtotal:</strong> {{ detail()?.subtotal || 0 | appCurrency: (detail()?.currency || 'MVR') }} | <strong>GST:</strong> {{ detail()?.taxAmount || 0 | appCurrency: (detail()?.currency || 'MVR') }}</p>
        <p *ngIf="!taxApplicable()"><strong>Subtotal:</strong> {{ detail()?.subtotal || 0 | appCurrency: (detail()?.currency || 'MVR') }}</p>
        <p><strong>Grand Total:</strong> {{ detail()?.grandTotal || 0 | appCurrency: (detail()?.currency || 'MVR') }}</p>
        <p><strong>Notes:</strong> {{ detail()?.notes || '-' }}</p>
        <div class="detail-list">
          <div *ngFor="let item of detail()?.items">{{ item.description }} - {{ item.qty }} x {{ item.rate | appCurrency: (detail()?.currency || 'MVR') }} = {{ item.total || 0 | appCurrency: (detail()?.currency || 'MVR') }}</div>
        </div>
        <div class="form-actions">
          <app-button variant="secondary" (clicked)="detail.set(null)">Close</app-button>
        </div>
      </app-card>
    </div>

    <app-confirm-dialog
      [open]="deleteDialogOpen()"
      title="Delete PO"
      message="This deletes the PO and its items."
      (cancel)="closeDeleteDialog()"
      (confirm)="deletePurchaseOrder()">
    </app-confirm-dialog>
  `,
  styles: `
    .toolbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: .75rem;
      margin-bottom: .75rem;
      flex-wrap: wrap;
    }
    .toolbar-left {
      display: flex;
      align-items: center;
      gap: .55rem;
      flex-wrap: wrap;
    }
    .page-size {
      display: inline-flex;
      align-items: center;
      gap: .45rem;
      color: var(--text-muted);
      font-size: .82rem;
      font-weight: 600;
    }
    .page-size select,
    .filter-panel select,
    input,
    textarea {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .52rem .6rem;
      background: rgba(255,255,255,.92);
    }
    .search-tools {
      display: grid;
      grid-template-columns: minmax(260px, 1fr) auto;
      gap: .55rem;
      align-items: center;
      min-width: min(520px, 100%);
      flex: 1 1 520px;
    }
    .filter-panel {
      border: 1px solid var(--border-soft);
      border-radius: 14px;
      background: linear-gradient(155deg, rgba(247, 250, 255, .92), rgba(239, 245, 255, .82));
      padding: .8rem;
      display: grid;
      gap: .75rem;
      margin-bottom: .8rem;
    }
    .filter-panel label,
    .custom-range label {
      display: grid;
      gap: .2rem;
      font-size: .82rem;
      color: var(--text-muted);
    }
    .custom-range {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: .75rem;
    }
    .filter-actions {
      display: flex;
      justify-content: flex-end;
      gap: .5rem;
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
      min-width: 300px;
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
      width: min(900px, 100%);
      max-height: 92vh;
      overflow: auto;
      --card-bg: linear-gradient(160deg, rgba(255,255,255,.95), rgba(245,248,255,.9));
    }
    .form-grid { display: grid; gap: .78rem; }
    .two-col { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .75rem; }
    .three-col { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .75rem; }
    label { display: grid; gap: .2rem; font-size: .82rem; color: var(--text-muted); align-content: start; }
    .item-section { display: flex; justify-content: space-between; align-items: center; }
    .item-row { display: grid; grid-template-columns: 2fr 1fr 1fr auto; gap: .5rem; align-items: end; }
    .item-field { display: grid; gap: .2rem; font-size: .78rem; color: var(--text-muted); }
    .item-actions { display: flex; align-items: end; }
    .tax-callout,
    .required-callout {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .68rem .74rem;
      background: rgba(246, 250, 255, .7);
      display: grid;
      gap: .18rem;
      align-content: start;
    }
    .tax-callout strong,
    .required-callout strong {
      color: var(--text-main);
      font-size: .84rem;
      font-family: var(--font-heading);
    }
    .tax-callout span,
    .required-callout span {
      color: var(--text-muted);
      font-size: .77rem;
      line-height: 1.4;
    }
    .form-actions { display: flex; justify-content: flex-end; gap: .5rem; }
    .detail-list { margin: .6rem 0; display: grid; gap: .3rem; }
    @media (max-width: 900px) {
      .search-tools {
        min-width: 100%;
      }
      .three-col {
        grid-template-columns: 1fr;
      }
    }
    @media (max-width: 760px) {
      .toolbar {
        align-items: stretch;
      }
      .search-tools {
        grid-template-columns: 1fr;
      }
      .custom-range,
      .two-col {
        grid-template-columns: 1fr;
      }
      .item-row {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .item-actions {
        grid-column: 1 / -1;
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
    }
    @media (max-width: 700px) {
      .actions {
        min-width: 0;
        flex-wrap: wrap;
      }
      .item-section {
        flex-wrap: wrap;
        gap: .45rem;
      }
      .item-section app-button,
      .item-actions app-button,
      .form-actions app-button {
        width: 100%;
      }
      .item-row,
      .form-actions {
        grid-template-columns: 1fr;
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
export class PoPageComponent implements OnInit {
  readonly purchaseOrders = signal<PagedResult<PurchaseOrderListItem> | null>(null);
  readonly suppliers = signal<SupplierLookup[]>([]);
  readonly vessels = signal<Vessel[]>([]);
  readonly search = signal('');
  readonly searchDraft = signal('');
  readonly pageNumber = signal(1);
  readonly pageSize = signal(10);
  readonly filterPanelOpen = signal(false);
  readonly selectedDatePreset = signal<DatePreset>('');
  readonly customStartDate = signal('');
  readonly customEndDate = signal('');
  readonly appliedDateFrom = signal('');
  readonly appliedDateTo = signal('');
  readonly appliedDateLabel = signal('');
  readonly formOpen = signal(false);
  readonly editId = signal<string | null>(null);
  readonly detail = signal<PurchaseOrder | null>(null);
  readonly taxApplicable = signal(true);
  readonly defaultTaxRate = signal(0.08);
  readonly defaultDueDays = signal(7);
  readonly deleteDialogOpen = signal(false);
  readonly deleteTarget = signal<PurchaseOrderListItem | null>(null);

  readonly pageSizeOptions = [10, 20, 40, 80, 100] as const;
  readonly datePresetOptions = [
    { value: 'today', label: 'Today' },
    { value: 'yesterday', label: 'Yesterday' },
    { value: 'last-7-days', label: 'Last 7 Days' },
    { value: 'last-week', label: 'Last Week' },
    { value: 'last-month', label: 'Last Month' },
    { value: 'last-30-days', label: 'Last 30 Days' },
    { value: 'custom-range', label: 'Custom Range' }
  ] as const;

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  readonly form = this.fb.nonNullable.group({
    supplierId: ['', Validators.required],
    courierId: [''],
    dateIssued: [this.today(), Validators.required],
    requiredDate: [this.futureDate(7), Validators.required],
    currency: ['MVR', Validators.required],
    taxRate: [0.08, Validators.required],
    notes: [''],
    items: this.fb.array([this.createItemForm()])
  });

  get items(): FormArray {
    return this.form.controls.items as FormArray;
  }

  ngOnInit(): void {
    this.loadSettings();
    this.reload();
    this.loadLookup();
  }

  onPageSizeChange(event: Event): void {
    this.pageSize.set(Number((event.target as HTMLSelectElement).value) || 10);
    this.pageNumber.set(1);
    this.reload();
  }

  onSearchDraftChange(value: string): void {
    this.searchDraft.set(value);
  }

  applySearch(): void {
    this.search.set(this.searchDraft().trim());
    this.pageNumber.set(1);
    this.reload();
  }

  toggleFilterPanel(): void {
    this.filterPanelOpen.update((value) => !value);
  }

  onDatePresetChange(event: Event): void {
    const preset = (event.target as HTMLSelectElement).value as DatePreset;
    this.selectedDatePreset.set(preset);
    if (preset !== 'custom-range') {
      this.customStartDate.set('');
      this.customEndDate.set('');
    }
  }

  onCustomStartDateChange(event: Event): void {
    this.customStartDate.set((event.target as HTMLInputElement).value);
  }

  onCustomEndDateChange(event: Event): void {
    this.customEndDate.set((event.target as HTMLInputElement).value);
  }

  applyFilters(): void {
    const preset = this.selectedDatePreset();
    if (!preset) {
      this.appliedDateFrom.set('');
      this.appliedDateTo.set('');
      this.appliedDateLabel.set('');
      this.filterPanelOpen.set(false);
      this.pageNumber.set(1);
      this.reload();
      return;
    }

    const range = this.resolveDateRange(preset, this.customStartDate(), this.customEndDate());
    if (!range) {
      this.toast.error('Select a valid date filter.');
      return;
    }

    this.appliedDateFrom.set(range.from);
    this.appliedDateTo.set(range.to);
    this.appliedDateLabel.set(range.label);
    this.filterPanelOpen.set(false);
    this.pageNumber.set(1);
    this.reload();
  }

  clearFilters(): void {
    this.selectedDatePreset.set('');
    this.customStartDate.set('');
    this.customEndDate.set('');
    this.appliedDateFrom.set('');
    this.appliedDateTo.set('');
    this.appliedDateLabel.set('');
    this.filterPanelOpen.set(false);
    this.pageNumber.set(1);
    this.reload();
  }

  changePage(page: number): void {
    this.pageNumber.set(page);
    this.reload();
  }

  openCreate(): void {
    this.editId.set(null);
    this.form.reset({
      supplierId: '',
      courierId: '',
      dateIssued: this.today(),
      requiredDate: this.futureDate(this.defaultDueDays()),
      currency: 'MVR',
      taxRate: this.taxApplicable() ? this.defaultTaxRate() : 0,
      notes: ''
    });
    this.form.setControl('items', this.fb.array([this.createItemForm()]));
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
    this.editId.set(null);
  }

  edit(purchaseOrder: PurchaseOrderListItem): void {
    this.api.getPurchaseOrderById(purchaseOrder.id).subscribe({
      next: (detail) => {
        this.editId.set(purchaseOrder.id);
        this.form.reset({
          supplierId: detail.supplierId,
          courierId: detail.courierId || '',
          dateIssued: detail.dateIssued,
          requiredDate: detail.requiredDate,
          currency: detail.currency,
          taxRate: detail.taxRate,
          notes: detail.notes || ''
        });
        this.form.setControl('items', this.fb.array(detail.items.map((item) => this.createItemForm(item))));
        this.formOpen.set(true);
      },
      error: () => this.toast.error('Failed to load PO details.')
    });
  }

  openDetail(purchaseOrder: PurchaseOrderListItem): void {
    this.api.getPurchaseOrderById(purchaseOrder.id).subscribe({
      next: (detail) => this.detail.set(detail),
      error: () => this.toast.error('Failed to load PO details.')
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
      this.form.markAllAsTouched();
      this.toast.error('Please complete required PO fields.');
      return;
    }

    const raw = this.form.getRawValue();
    if (raw.requiredDate < raw.dateIssued) {
      this.toast.error('Required date cannot be earlier than the issued date.');
      return;
    }

    const payload = {
      supplierId: raw.supplierId,
      courierId: raw.courierId || null,
      dateIssued: raw.dateIssued,
      requiredDate: raw.requiredDate,
      currency: raw.currency,
      taxRate: this.taxApplicable() ? Number(raw.taxRate) : 0,
      notes: raw.notes || null,
      items: raw.items.map((item) => ({
        description: item.description,
        qty: Number(item.qty),
        rate: Number(item.rate)
      }))
    };

    const request$ = this.editId()
      ? this.api.updatePurchaseOrder(this.editId()!, payload)
      : this.api.createPurchaseOrder(payload);

    request$.subscribe({
      next: () => {
        this.toast.success('PO saved.');
        this.closeForm();
        this.reload();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to save PO.'))
    });
  }

  exportPurchaseOrder(purchaseOrder: PurchaseOrderListItem): void {
    this.api.exportPurchaseOrder(purchaseOrder.id).subscribe({
      next: (file) => this.download(file, `${purchaseOrder.purchaseOrderNo}.pdf`),
      error: () => this.toast.error('Failed to export PO PDF.')
    });
  }

  confirmDelete(purchaseOrder: PurchaseOrderListItem): void {
    this.deleteTarget.set(purchaseOrder);
    this.deleteDialogOpen.set(true);
  }

  closeDeleteDialog(): void {
    this.deleteDialogOpen.set(false);
    this.deleteTarget.set(null);
  }

  deletePurchaseOrder(): void {
    const target = this.deleteTarget();
    if (!target) {
      return;
    }

    this.api.deletePurchaseOrder(target.id).subscribe({
      next: () => {
        this.toast.success('PO deleted.');
        if (this.detail()?.id === target.id) {
          this.detail.set(null);
        }
        if (this.editId() === target.id) {
          this.closeForm();
        }
        this.closeDeleteDialog();
        this.reload();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to delete PO.'))
    });
  }

  showingRangeLabel(page: PagedResult<PurchaseOrderListItem>): string {
    if (page.totalCount === 0) {
      return '0 of 0';
    }

    const start = (page.pageNumber - 1) * page.pageSize + 1;
    const end = Math.min(page.pageNumber * page.pageSize, page.totalCount);
    return `${start}-${end} of ${page.totalCount}`;
  }

  isAdmin(): boolean {
    return this.auth.user()?.role === 'Admin';
  }

  private loadLookup(): void {
    this.api.getSupplierLookup().subscribe({
      next: (result) => this.suppliers.set(result),
      error: () => this.toast.error('Failed to load suppliers.')
    });

    this.api.getAllVessels().subscribe({
      next: (result) => this.vessels.set(result),
      error: () => this.toast.error('Failed to load couriers.')
    });
  }

  private loadSettings(): void {
    this.api.getSettings().subscribe({
      next: (settings) => {
        this.taxApplicable.set(settings.isTaxApplicable);
        this.defaultTaxRate.set(settings.defaultTaxRate > 0 ? settings.defaultTaxRate : 0.08);
        this.defaultDueDays.set(settings.defaultDueDays > 0 ? settings.defaultDueDays : 7);

        if (!this.editId()) {
          this.form.controls.taxRate.setValue(settings.isTaxApplicable ? this.defaultTaxRate() : 0);
          this.form.controls.requiredDate.setValue(this.futureDate(this.defaultDueDays()));
        }
      },
      error: () => this.toast.error('Failed to load settings.')
    });
  }

  private reload(): void {
    this.api.getPurchaseOrders({
      pageNumber: this.pageNumber(),
      pageSize: this.pageSize(),
      search: this.search(),
      dateFrom: this.appliedDateFrom(),
      dateTo: this.appliedDateTo()
    }).subscribe({
      next: (result) => {
        if (result.totalPages > 0 && result.pageNumber > result.totalPages) {
          this.pageNumber.set(result.totalPages);
          this.reload();
          return;
        }

        this.purchaseOrders.set(result);
      },
      error: () => this.toast.error('Failed to load POs.')
    });
  }

  private createItemForm(item?: { description?: string; qty?: number; rate?: number }) {
    return this.fb.nonNullable.group({
      description: [item?.description || '', Validators.required],
      qty: [item?.qty ?? 1, [Validators.required, Validators.min(0.01)]],
      rate: [item?.rate ?? 0, [Validators.required, Validators.min(0)]]
    });
  }

  private resolveDateRange(preset: DatePreset, customStart: string, customEnd: string): { from: string; to: string; label: string } | null {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    if (preset === 'custom-range') {
      if (!customStart || !customEnd) {
        return null;
      }

      const start = this.parseDate(customStart);
      const end = this.parseDate(customEnd);
      if (!start || !end) {
        return null;
      }

      const normalized = start <= end ? { start, end } : { start: end, end: start };
      return {
        from: this.formatDate(normalized.start),
        to: this.formatDate(normalized.end),
        label: `${this.formatDate(normalized.start)} to ${this.formatDate(normalized.end)}`
      };
    }

    let start = new Date(today);
    let end = new Date(today);
    let label = '';

    switch (preset) {
      case 'today':
        label = 'Today';
        break;
      case 'yesterday':
        start = this.addDays(today, -1);
        end = this.addDays(today, -1);
        label = 'Yesterday';
        break;
      case 'last-7-days':
        start = this.addDays(today, -6);
        label = 'Last 7 Days';
        break;
      case 'last-week': {
        const currentWeekStart = this.startOfWeek(today);
        start = this.addDays(currentWeekStart, -7);
        end = this.addDays(currentWeekStart, -1);
        label = 'Last Week';
        break;
      }
      case 'last-month': {
        const currentMonthStart = new Date(today.getFullYear(), today.getMonth(), 1);
        start = new Date(currentMonthStart.getFullYear(), currentMonthStart.getMonth() - 1, 1);
        end = new Date(currentMonthStart.getFullYear(), currentMonthStart.getMonth(), 0);
        label = 'Last Month';
        break;
      }
      case 'last-30-days':
        start = this.addDays(today, -29);
        label = 'Last 30 Days';
        break;
      default:
        return null;
    }

    return {
      from: this.formatDate(start),
      to: this.formatDate(end),
      label
    };
  }

  private startOfWeek(date: Date): Date {
    const start = new Date(date);
    start.setDate(start.getDate() - start.getDay());
    start.setHours(0, 0, 0, 0);
    return start;
  }

  private addDays(date: Date, days: number): Date {
    const next = new Date(date);
    next.setDate(next.getDate() + days);
    next.setHours(0, 0, 0, 0);
    return next;
  }

  private parseDate(value: string): Date | null {
    const [year, month, day] = value.split('-').map(Number);
    if (!year || !month || !day) {
      return null;
    }

    return new Date(year, month - 1, day);
  }

  private today(): string {
    return this.formatDate(new Date());
  }

  private futureDate(days: number): string {
    return this.formatDate(this.addDays(new Date(), days));
  }

  private formatDate(value: Date): string {
    const year = value.getFullYear();
    const month = `${value.getMonth() + 1}`.padStart(2, '0');
    const day = `${value.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
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
}
