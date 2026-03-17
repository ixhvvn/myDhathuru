import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { Router } from '@angular/router';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { AppDocumentEmailDialogComponent } from '../../../shared/components/app-document-email-dialog/app-document-email-dialog.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppDateBadgeComponent } from '../../../shared/components/app-date-badge/app-date-badge.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AppStatusChipComponent } from '../../../shared/components/app-status-chip/app-status-chip.component';
import { Customer, DocumentEmailRequest, DocumentEmailStatus, PagedResult, Quotation, QuotationListItem, TenantSettings, Vessel } from '../../../core/models/app.models';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { resolveDocumentEmailBody } from '../../../core/utils/document-email-template.util';
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
  selector: 'app-quote-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppButtonComponent,
    AppCardComponent,
    AppConfirmDialogComponent,
    AppDocumentEmailDialogComponent,
    AppCurrencyPipe,
    AppDataTableComponent,
    AppDateBadgeComponent,
    AppPageHeaderComponent,
    AppSearchBarComponent,
    AppStatusChipComponent
  ],
  template: `
    <app-page-header title="Quote" subtitle="Create polished quotations, track quotation history, and export PDF quotes">
      <app-button (clicked)="openCreate()">New Quote</app-button>
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
            placeholder="Search quotation no or customer"
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

      <div class="summary" *ngIf="quotes() as page">
        Total quotes: {{ page.totalCount }}.
        <span *ngIf="appliedDateLabel()"> Date filter: {{ appliedDateLabel() }}.</span>
        <span *ngIf="search()"> Search: "{{ search() }}".</span>
      </div>

      <app-data-table [hasData]="(quotes()?.items?.length ?? 0) > 0" emptyTitle="No quotations" emptyDescription="Create your first quotation to start the history.">
        <thead>
          <tr>
            <th>Quotation No</th>
            <th>Customer</th>
            <th>Courier</th>
            <th>Currency</th>
            <th>Amount</th>
            <th>Date Issued</th>
            <th>Valid Until</th>
            <th>Email Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let quote of quotes()?.items">
            <td>{{ quote.quotationNo }}</td>
            <td>{{ quote.customer }}</td>
            <td>{{ quote.courierName || '-' }}</td>
            <td>{{ quote.currency }}</td>
            <td>{{ quote.amount | appCurrency: quote.currency }}</td>
            <td><app-date-badge [value]="quote.dateIssued"></app-date-badge></td>
            <td><app-date-badge [value]="quote.validUntil"></app-date-badge></td>
            <td>
              <app-status-chip [label]="quote.emailStatus === 'Emailed' ? 'Emailed' : 'Email Pending'" [variant]="emailStatusVariant(quote.emailStatus)"></app-status-chip>
            </td>
            <td class="actions">
              <app-button size="sm" variant="secondary" (clicked)="openDetail(quote)">View</app-button>
              <app-button size="sm" variant="secondary" (clicked)="edit(quote)">Edit</app-button>
              <app-button
                size="sm"
                [variant]="quote.convertedDeliveryNoteId || quote.convertedInvoiceId ? 'secondary' : 'primary'"
                (clicked)="handleSaleAction(quote)">
                {{ quote.convertedDeliveryNoteId ? 'View Delivery Note' : (quote.convertedInvoiceId ? 'View Sale' : 'Create Delivery Note') }}
              </app-button>
              <app-button *ngIf="isAdmin()" size="sm" variant="danger" (clicked)="confirmDelete(quote)">Delete</app-button>
              <app-button size="sm" variant="secondary" (clicked)="openEmailDialog(quote)">Email</app-button>
              <app-button size="sm" variant="secondary" (clicked)="exportQuote(quote)">PDF</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>

      <div class="pager" *ngIf="quotes() as page">
        <span>Showing {{ showingRangeLabel(page) }} | Page {{ page.pageNumber }} of {{ page.totalPages || 1 }}</span>
        <div>
          <app-button size="sm" variant="secondary" [disabled]="page.pageNumber <= 1" (clicked)="changePage(page.pageNumber - 1)">Prev</app-button>
          <app-button size="sm" variant="secondary" [disabled]="page.pageNumber >= page.totalPages" (clicked)="changePage(page.pageNumber + 1)">Next</app-button>
        </div>
      </div>
    </app-card>

    <div class="drawer" *ngIf="formOpen()">
      <app-card>
        <h3>{{ editId() ? 'Edit Quotation' : 'New Quotation' }}</h3>
        <form [formGroup]="form" (ngSubmit)="save()" class="form-grid">
          <div class="two-col">
            <label>Customer
              <select formControlName="customerId">
                <option value="">Select customer</option>
                <option *ngFor="let customer of customers()" [value]="customer.id">{{ customer.name }}</option>
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
            <label>Valid Until <input type="date" formControlName="validUntil"></label>
            <label>Currency
              <select formControlName="currency">
                <option value="MVR">MVR</option>
                <option value="USD">USD</option>
              </select>
            </label>
          </div>

          <div class="two-col">
            <label>PO Number (Optional) <input type="text" formControlName="poNumber"></label>
            <ng-container *ngIf="taxApplicable(); else taxDisabledState">
              <label>Tax Rate <input type="number" formControlName="taxRate" step="0.01"></label>
            </ng-container>
          </div>

          <ng-template #taxDisabledState>
            <div class="tax-callout">
              <strong>Tax disabled</strong>
              <span>Quotations for this business are saved and exported without tax.</span>
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
        <h3>Quotation {{ detail()?.quotationNo }}</h3>
        <p><strong>Customer:</strong> {{ detail()?.customerName }}</p>
        <p><strong>Contact:</strong> {{ detail()?.customerPhone || '-' }} | {{ detail()?.customerEmail || '-' }}</p>
        <p><strong>Courier:</strong> {{ detail()?.courierName || '-' }}</p>
        <p><strong>PO No:</strong> {{ detail()?.poNumber || '-' }}</p>
        <p><strong>Converted Delivery Note:</strong> {{ detail()?.convertedDeliveryNoteNo || 'Not converted yet' }}</p>
        <p *ngIf="detail()?.convertedInvoiceNo"><strong>Issued Invoice:</strong> {{ detail()?.convertedInvoiceNo }}</p>
        <p><strong>Currency:</strong> {{ detail()?.currency }}</p>
        <p><strong>Issued:</strong> {{ detail()?.dateIssued }} | <strong>Valid Until:</strong> {{ detail()?.validUntil }}</p>
        <p *ngIf="taxApplicable()"><strong>Subtotal:</strong> {{ detail()?.subtotal || 0 | appCurrency: (detail()?.currency || 'MVR') }} | <strong>GST:</strong> {{ detail()?.taxAmount || 0 | appCurrency: (detail()?.currency || 'MVR') }}</p>
        <p *ngIf="!taxApplicable()"><strong>Subtotal:</strong> {{ detail()?.subtotal || 0 | appCurrency: (detail()?.currency || 'MVR') }}</p>
        <p><strong>Grand Total:</strong> {{ detail()?.grandTotal || 0 | appCurrency: (detail()?.currency || 'MVR') }}</p>
        <p><strong>Notes:</strong> {{ detail()?.notes || '-' }}</p>
        <div class="detail-list">
          <div *ngFor="let item of detail()?.items">{{ item.description }} - {{ item.qty }} x {{ item.rate | appCurrency: (detail()?.currency || 'MVR') }} = {{ item.total || 0 | appCurrency: (detail()?.currency || 'MVR') }}</div>
        </div>
        <div class="form-actions">
          <app-button *ngIf="detail()?.convertedDeliveryNoteNo" variant="secondary" (clicked)="openDeliveryNotes(detail()?.convertedDeliveryNoteNo || '')">Open Delivery Note</app-button>
          <app-button *ngIf="detail()?.convertedInvoiceNo" variant="secondary" (clicked)="openSaleHistory(detail()?.convertedInvoiceNo || '')">Open Sale</app-button>
          <app-button variant="secondary" (clicked)="detail.set(null)">Close</app-button>
        </div>
      </app-card>
    </div>

    <app-confirm-dialog
      [open]="deleteDialogOpen()"
      title="Delete quotation"
      message="This permanently deletes the quotation and its items from the database."
      (cancel)="closeDeleteDialog()"
      (confirm)="deleteQuote()">
    </app-confirm-dialog>

    <app-document-email-dialog
      [open]="emailDialogOpen()"
      title="Email Quotation"
      [toEmail]="emailTo()"
      [body]="emailBody()"
      [attachmentName]="emailAttachmentName()"
      [loading]="emailSending()"
      [emailStatus]="emailStatus()"
      [lastEmailedAt]="emailLastEmailedAt()"
      (cancel)="closeEmailDialog()"
      (send)="sendEmail($event)">
    </app-document-email-dialog>
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
      min-width: 390px;
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
    .tax-callout {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .68rem .74rem;
      background: rgba(246, 250, 255, .7);
      display: grid;
      gap: .18rem;
      align-content: start;
    }
    .tax-callout strong {
      color: var(--text-main);
      font-size: .84rem;
      font-family: var(--font-heading);
    }
    .tax-callout span {
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
export class QuotePageComponent implements OnInit {
  private static readonly salesHistorySearchKey = 'sales-history-search';

  readonly quotes = signal<PagedResult<QuotationListItem> | null>(null);
  readonly customers = signal<Customer[]>([]);
  readonly vessels = signal<Vessel[]>([]);
  readonly settings = signal<TenantSettings | null>(null);
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
  readonly detail = signal<Quotation | null>(null);
  readonly taxApplicable = signal(true);
  readonly defaultTaxRate = signal(0.08);
  readonly defaultDueDays = signal(7);
  readonly deleteDialogOpen = signal(false);
  readonly deleteTarget = signal<QuotationListItem | null>(null);
  readonly emailDialogOpen = signal(false);
  readonly emailTargetId = signal<string | null>(null);
  readonly emailTo = signal('');
  readonly emailBody = signal('');
  readonly emailAttachmentName = signal('quotation.pdf');
  readonly emailStatus = signal<DocumentEmailStatus>('Pending');
  readonly emailLastEmailedAt = signal<string | null>(null);
  readonly emailSending = signal(false);

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
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  readonly form = this.fb.nonNullable.group({
    customerId: ['', Validators.required],
    courierId: [''],
    poNumber: [''],
    dateIssued: [this.today(), Validators.required],
    validUntil: [this.futureDate(7), Validators.required],
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
      customerId: '',
      courierId: '',
      poNumber: '',
      dateIssued: this.today(),
      validUntil: this.futureDate(this.defaultDueDays()),
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

  edit(quote: QuotationListItem): void {
    this.api.getQuoteById(quote.id).subscribe({
      next: (detail) => {
        this.editId.set(quote.id);
        this.form.reset({
          customerId: detail.customerId,
          courierId: detail.courierId || '',
          poNumber: detail.poNumber || '',
          dateIssued: detail.dateIssued,
          validUntil: detail.validUntil,
          currency: detail.currency,
          taxRate: detail.taxRate,
          notes: detail.notes || ''
        });
        this.form.setControl('items', this.fb.array(detail.items.map((item) => this.createItemForm(item))));
        this.formOpen.set(true);
      },
      error: () => this.toast.error('Failed to load quotation details.')
    });
  }

  openDetail(quote: QuotationListItem): void {
    this.api.getQuoteById(quote.id).subscribe({
      next: (detail) => this.detail.set(detail),
      error: () => this.toast.error('Failed to load quotation details.')
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
      this.toast.error('Please complete required quotation fields.');
      return;
    }

    const raw = this.form.getRawValue();
    if (raw.validUntil < raw.dateIssued) {
      this.toast.error('Valid until date cannot be earlier than the issued date.');
      return;
    }

    const payload = {
      customerId: raw.customerId,
      courierId: raw.courierId || null,
      poNumber: raw.poNumber || null,
      dateIssued: raw.dateIssued,
      validUntil: raw.validUntil,
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
      ? this.api.updateQuote(this.editId()!, payload)
      : this.api.createQuote(payload);

    request$.subscribe({
      next: () => {
        this.toast.success(`Quotation ${this.editId() ? 'updated' : 'created'} successfully.`);
        this.closeForm();
        this.reload();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to save quotation.'))
    });
  }

  exportQuote(quote: QuotationListItem): void {
    this.api.exportQuote(quote.id).subscribe({
      next: (file) => this.download(file, `${quote.quotationNo}.pdf`),
      error: () => this.toast.error('Failed to export quotation PDF.')
    });
  }

  handleSaleAction(quote: QuotationListItem): void {
    if (quote.convertedDeliveryNoteNo) {
      this.openDeliveryNotes(quote.convertedDeliveryNoteNo);
      return;
    }

    if (quote.convertedInvoiceNo) {
      this.openSaleHistory(quote.convertedInvoiceNo);
      return;
    }

    this.api.convertQuoteToSale(quote.id).subscribe({
      next: (result) => {
        const targetLabel = result.targetType === 'DeliveryNote' ? 'delivery note' : 'sale';
        const message = result.alreadyConverted
          ? `Quotation already converted. Opening ${targetLabel} ${result.documentNo}.`
          : `Quotation converted to ${targetLabel} ${result.documentNo}.`;
        this.toast.success(message);
        this.reload();
        if (this.detail()?.id === quote.id) {
          this.api.getQuoteById(quote.id).subscribe({
            next: (detail) => this.detail.set(detail)
          });
        }

        if (result.targetType === 'DeliveryNote') {
          this.openDeliveryNotes(result.documentNo);
          return;
        }

        this.openSaleHistory(result.documentNo);
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to create a delivery note from this quotation.'))
    });
  }

  confirmDelete(quote: QuotationListItem): void {
    this.deleteTarget.set(quote);
    this.deleteDialogOpen.set(true);
  }

  closeDeleteDialog(): void {
    this.deleteDialogOpen.set(false);
    this.deleteTarget.set(null);
  }

  deleteQuote(): void {
    const target = this.deleteTarget();
    if (!target) {
      return;
    }

    this.api.deleteQuote(target.id).subscribe({
      next: () => {
        this.toast.success('Quotation deleted.');
        if (this.detail()?.id === target.id) {
          this.detail.set(null);
        }
        if (this.editId() === target.id) {
          this.closeForm();
        }
        this.closeDeleteDialog();
        this.reload();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to delete quotation.'))
    });
  }

  showingRangeLabel(page: PagedResult<QuotationListItem>): string {
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

  emailStatusVariant(status: DocumentEmailStatus): 'green' | 'amber' {
    return status === 'Emailed' ? 'green' : 'amber';
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
  }

  private loadSettings(): void {
    this.api.getSettings().subscribe({
      next: (settings) => {
        this.settings.set(settings);
        this.taxApplicable.set(settings.isTaxApplicable);
        this.defaultTaxRate.set(settings.defaultTaxRate > 0 ? settings.defaultTaxRate : 0.08);
        this.defaultDueDays.set(settings.defaultDueDays > 0 ? settings.defaultDueDays : 7);

        if (!this.editId()) {
          this.form.controls.taxRate.setValue(settings.isTaxApplicable ? this.defaultTaxRate() : 0);
          this.form.controls.validUntil.setValue(this.futureDate(this.defaultDueDays()));
        }
      },
      error: () => this.toast.error('Failed to load quote settings.')
    });
  }

  openEmailDialog(quote: QuotationListItem): void {
    this.api.getQuoteById(quote.id).subscribe({
      next: (detail) => {
        if (!detail.customerEmail?.trim()) {
          this.toast.error('Customer email is not configured for this quotation.');
          return;
        }

        this.emailTargetId.set(quote.id);
        this.emailTo.set(detail.customerEmail.trim());
        this.emailBody.set(this.buildEmailBody());
        this.emailAttachmentName.set(`${detail.quotationNo}.pdf`);
        this.emailStatus.set(detail.emailStatus);
        this.emailLastEmailedAt.set(detail.lastEmailedAt ?? null);
        this.emailDialogOpen.set(true);
      },
      error: () => this.toast.error('Failed to load quotation email details.')
    });
  }

  closeEmailDialog(): void {
    this.emailDialogOpen.set(false);
    this.emailTargetId.set(null);
    this.emailTo.set('');
    this.emailBody.set('');
    this.emailAttachmentName.set('quotation.pdf');
    this.emailStatus.set('Pending');
    this.emailLastEmailedAt.set(null);
  }

  sendEmail(payload: DocumentEmailRequest): void {
    const quoteId = this.emailTargetId();
    if (!quoteId) {
      return;
    }

    this.emailSending.set(true);
    this.api.sendQuoteEmail(quoteId, payload)
      .pipe(finalize(() => this.emailSending.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Quotation emailed.');
          if (this.detail()?.id === quoteId) {
            this.api.getQuoteById(quoteId).subscribe({
              next: (detail) => this.detail.set(detail)
            });
          }
          this.closeEmailDialog();
          this.reload();
        },
        error: (error) => this.toast.error(this.readError(error, 'Unable to email quotation.'))
      });
  }

  private reload(): void {
    this.api.getQuotes({
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

        this.quotes.set(result);
      },
      error: () => this.toast.error('Failed to load quotations.')
    });
  }

  openSaleHistory(invoiceNo: string): void {
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.setItem(QuotePageComponent.salesHistorySearchKey, invoiceNo);
    }
    void this.router.navigate(['/app/sales-history']);
  }

  openDeliveryNotes(deliveryNoteNo: string): void {
    void this.router.navigate(['/app/delivery-notes'], {
      queryParams: { search: deliveryNoteNo }
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

  private buildEmailBody(): string {
    return resolveDocumentEmailBody(
      this.settings()?.quotationEmailBodyTemplate,
      'Quotation',
      this.companyName()
    );
  }

  private companyName(): string {
    return this.settings()?.companyName?.trim() || this.auth.user()?.companyName?.trim() || 'Your Company';
  }
}
