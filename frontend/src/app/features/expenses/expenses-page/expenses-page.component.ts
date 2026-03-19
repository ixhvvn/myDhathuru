import { CommonModule } from '@angular/common';
import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppDateBadgeComponent } from '../../../shared/components/app-date-badge/app-date-badge.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { ExpenseCategoryLookup, ExpenseEntryDetail, ExpenseLedgerRow, ExpenseSummary, PagedResult, SupplierLookup } from '../../../core/models/app.models';
import { extractApiError } from '../../../core/utils/api-error.util';
import { setAppScrollLock } from '../../../core/utils/app-scroll-lock.util';
import { PortalApiService } from '../../services/portal-api.service';

@Component({
  selector: 'app-expenses-page',
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
    <app-page-header title="Expense Ledger" subtitle="Review all purchase-side, rent, payroll, and manual expenses from live transactions">
      <app-button variant="secondary" [loading]="exportLoading() === 'excel'" [disabled]="(page()?.items?.length ?? 0) === 0" (clicked)="exportLedger('excel')">Export Excel</app-button>
      <app-button variant="secondary" [loading]="exportLoading() === 'pdf'" [disabled]="(page()?.items?.length ?? 0) === 0" (clicked)="exportLedger('pdf')">Export PDF</app-button>
      <app-button (clicked)="openCreate()">New Manual Expense</app-button>
    </app-page-header>

    <div class="summary-grid" *ngIf="summary() as totals">
      <app-card>
        <strong>Total Net</strong>
        <span>{{ totals.totalNetAmount | appCurrency:'MVR' }}</span>
      </app-card>
      <app-card>
        <strong>Total Tax</strong>
        <span>{{ totals.totalTaxAmount | appCurrency:'MVR' }}</span>
      </app-card>
      <app-card>
        <strong>Total Gross</strong>
        <span>{{ totals.totalGrossAmount | appCurrency:'MVR' }}</span>
      </app-card>
      <app-card>
        <strong>Pending</strong>
        <span>{{ totals.totalPendingAmount | appCurrency:'MVR' }}</span>
      </app-card>
    </div>

    <app-card>
      <div class="toolbar">
        <app-search-bar [value]="search()" placeholder="Search document, payee, category, or supplier" (searchChange)="onSearchChange($event)"></app-search-bar>
        <div class="toolbar-actions">
          <select [value]="selectedCategoryId()" (change)="selectedCategoryId.set(asValue($event)); refresh()">
            <option value="">All categories</option>
            <option *ngFor="let category of categories()" [value]="category.id">{{ category.name }}</option>
          </select>
          <select [value]="selectedSourceType()" (change)="selectedSourceType.set(asValue($event)); refresh()">
            <option value="">All sources</option>
            <option value="ReceivedInvoice">Received Invoice</option>
            <option value="Rent">Rent</option>
            <option value="Payroll">Payroll</option>
            <option value="Manual">Manual</option>
          </select>
          <label class="checkbox"><input type="checkbox" [checked]="pendingOnly()" (change)="togglePending($event)"> Pending only</label>
        </div>
      </div>

      <app-data-table [hasData]="(page()?.items?.length ?? 0) > 0" emptyTitle="No expense rows" emptyDescription="Ledger totals are sourced from received invoices, rent, payroll periods, and manual entries.">
        <thead>
          <tr>
            <th>Date</th>
            <th>Document</th>
            <th>Source</th>
            <th>Category</th>
            <th>Payee</th>
            <th>Gross</th>
            <th>Pending</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let row of page()?.items">
            <td><app-date-badge [value]="row.transactionDate"></app-date-badge></td>
            <td>{{ row.documentNumber }}</td>
            <td>{{ row.sourceType }}</td>
            <td>{{ row.expenseCategoryName }}</td>
            <td>{{ row.payeeName }}</td>
            <td>{{ row.grossAmount | appCurrency:row.currency }}</td>
            <td>{{ row.pendingAmount | appCurrency:row.currency }}</td>
            <td class="row-actions">
              <div class="row-action-buttons">
                <app-button *ngIf="row.sourceType === 'Manual'" size="sm" variant="secondary" (clicked)="editManual(row)">Edit</app-button>
                <app-button *ngIf="row.sourceType === 'Manual' && isAdmin()" size="sm" variant="danger" (clicked)="confirmDelete(row)">Delete</app-button>
              </div>
            </td>
          </tr>
        </tbody>
      </app-data-table>
    </app-card>

    <div class="detail-grid" *ngIf="summary() as totals">
      <app-card>
        <h3>By Category</h3>
        <div class="breakdown-row" *ngFor="let item of totals.byCategory">
          <span>{{ item.label }}</span>
          <strong>{{ item.grossAmount | appCurrency:'MVR' }}</strong>
        </div>
      </app-card>
      <app-card>
        <h3>By Month</h3>
        <div class="breakdown-row" *ngFor="let item of totals.byMonth">
          <span>{{ item.label }}</span>
          <strong>{{ item.grossAmount | appCurrency:'MVR' }}</strong>
        </div>
      </app-card>
    </div>

    <div class="drawer" *ngIf="formOpen()">
      <app-card>
        <h3>{{ editId() ? 'Edit Manual Expense' : 'New Manual Expense' }}</h3>
        <form [formGroup]="form" (ngSubmit)="save()" class="form-grid">
          <div class="three-col">
            <label>Date <input type="date" formControlName="transactionDate"></label>
            <label>Document Number <input type="text" formControlName="documentNumber"></label>
            <label>Currency
              <select formControlName="currency">
                <option value="MVR">MVR</option>
                <option value="USD">USD</option>
              </select>
            </label>
          </div>
          <div class="three-col">
            <label>Expense Category
              <select formControlName="expenseCategoryId">
                <option value="">Select category</option>
                <option *ngFor="let category of categories()" [value]="category.id">{{ category.name }}</option>
              </select>
            </label>
            <label>Supplier
              <select formControlName="supplierId">
                <option value="">Optional supplier</option>
                <option *ngFor="let supplier of suppliers()" [value]="supplier.id">{{ supplier.name }}</option>
              </select>
            </label>
            <label>Payee Name <input type="text" formControlName="payeeName"></label>
          </div>
          <div class="four-col">
            <label>Net Amount <input type="number" formControlName="netAmount" step="0.01"></label>
            <label>Tax Amount <input type="number" formControlName="taxAmount" step="0.01"></label>
            <label>Claimable Tax <input type="number" formControlName="claimableTaxAmount" step="0.01"></label>
            <label>Pending Amount <input type="number" formControlName="pendingAmount" step="0.01"></label>
          </div>
          <label>Description <textarea rows="3" formControlName="description"></textarea></label>
          <label>Notes <textarea rows="3" formControlName="notes"></textarea></label>
          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closeForm()">Cancel</app-button>
            <app-button type="submit">Save</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <app-confirm-dialog
      [open]="deleteDialogOpen()"
      title="Delete expense entry"
      message="This permanently deletes the manual expense entry."
      (cancel)="closeDeleteDialog()"
      (confirm)="deleteManual()">
    </app-confirm-dialog>
  `,
  styles: `
    .summary-grid, .detail-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 1rem;
      margin-bottom: 1rem;
    }
    .summary-grid app-card span {
      display: block;
      margin-top: .4rem;
      font-size: 1.35rem;
      font-weight: 700;
      color: #345089;
    }
    .toolbar, .toolbar-actions, .three-col, .four-col, .form-actions, .row-action-buttons {
      display: flex;
      gap: .75rem;
      align-items: center;
      flex-wrap: wrap;
    }
    .toolbar {
      justify-content: space-between;
      margin-bottom: .85rem;
    }
    .breakdown-row {
      display: flex;
      justify-content: space-between;
      gap: .75rem;
      padding: .45rem 0;
      border-bottom: 1px solid #edf1fb;
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
    .four-col > label {
      flex: 1 1 180px;
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
    .checkbox {
      display: flex;
      gap: .5rem;
      align-items: center;
    }
    .checkbox input {
      width: auto;
    }
    .toolbar-actions, .form-actions {
      justify-content: flex-end;
    }
    .row-actions {
      white-space: nowrap;
    }
    .row-action-buttons {
      justify-content: flex-end;
      min-height: 100%;
    }
  `
})
export class ExpensesPageComponent implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);
  private readonly scrollLockOwner = {};

  readonly page = signal<PagedResult<ExpenseLedgerRow> | null>(null);
  readonly summary = signal<ExpenseSummary | null>(null);
  readonly categories = signal<ExpenseCategoryLookup[]>([]);
  readonly suppliers = signal<SupplierLookup[]>([]);
  readonly search = signal('');
  readonly selectedCategoryId = signal('');
  readonly selectedSourceType = signal('');
  readonly pendingOnly = signal(false);
  readonly formOpen = signal(false);
  readonly editId = signal<string | null>(null);
  readonly deleteDialogOpen = signal(false);
  readonly deleting = signal<ExpenseLedgerRow | null>(null);
  readonly exportLoading = signal<'excel' | 'pdf' | null>(null);
  private readonly overlayScrollLockEffect = effect((onCleanup) => {
    setAppScrollLock(this.scrollLockOwner, this.formOpen());
    onCleanup(() => setAppScrollLock(this.scrollLockOwner, false));
  });

  readonly form = this.fb.nonNullable.group({
    transactionDate: [new Date().toISOString().slice(0, 10), Validators.required],
    documentNumber: ['', [Validators.required, Validators.maxLength(60)]],
    expenseCategoryId: ['', Validators.required],
    supplierId: [''],
    payeeName: ['', [Validators.required, Validators.maxLength(200)]],
    currency: 'MVR',
    netAmount: 0,
    taxAmount: 0,
    claimableTaxAmount: 0,
    pendingAmount: 0,
    description: ['', Validators.maxLength(500)],
    notes: ['', Validators.maxLength(2000)]
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

  togglePending(event: Event): void {
    this.pendingOnly.set((event.target as HTMLInputElement).checked);
    this.refresh();
  }

  openCreate(): void {
    this.editId.set(null);
    this.form.reset({
      transactionDate: new Date().toISOString().slice(0, 10),
      documentNumber: '',
      expenseCategoryId: '',
      supplierId: '',
      payeeName: '',
      currency: 'MVR',
      netAmount: 0,
      taxAmount: 0,
      claimableTaxAmount: 0,
      pendingAmount: 0,
      description: '',
      notes: ''
    });
    this.formOpen.set(true);
  }

  editManual(row: ExpenseLedgerRow): void {
    this.api.getExpenseEntryById(row.sourceId).subscribe({
      next: (entry: ExpenseEntryDetail) => {
        this.editId.set(entry.id);
        this.form.reset({
          transactionDate: entry.transactionDate,
          documentNumber: entry.documentNumber,
          expenseCategoryId: entry.expenseCategoryId,
          supplierId: entry.supplierId ?? '',
          payeeName: entry.payeeName,
          currency: entry.currency,
          netAmount: entry.netAmount,
          taxAmount: entry.taxAmount,
          claimableTaxAmount: entry.claimableTaxAmount,
          pendingAmount: entry.pendingAmount,
          description: entry.description ?? '',
          notes: entry.notes ?? ''
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
      ? this.api.updateExpenseEntry(this.editId()!, payload)
      : this.api.createExpenseEntry(payload);

    request$.subscribe({
      next: () => {
        this.toast.success(this.editId() ? 'Expense entry updated.' : 'Expense entry created.');
        this.formOpen.set(false);
        this.refresh();
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  confirmDelete(row: ExpenseLedgerRow): void {
    this.deleting.set(row);
    this.deleteDialogOpen.set(true);
  }

  closeDeleteDialog(): void {
    this.deleteDialogOpen.set(false);
    this.deleting.set(null);
  }

  deleteManual(): void {
    const row = this.deleting();
    if (!row) {
      return;
    }

    this.api.deleteExpenseEntry(row.sourceId).subscribe({
      next: () => {
        this.toast.success('Expense entry deleted.');
        this.closeDeleteDialog();
        this.refresh();
      },
      error: (error) => {
        this.toast.error(extractApiError(error));
        this.closeDeleteDialog();
      }
    });
  }

  exportLedger(format: 'excel' | 'pdf'): void {
    this.exportLoading.set(format);

    const params = this.buildExportParams();
    const request$ = format === 'excel'
      ? this.api.exportExpenseLedgerExcel(params)
      : this.api.exportExpenseLedgerPdf(params);

    request$
      .pipe(finalize(() => this.exportLoading.set(null)))
      .subscribe({
        next: (blob) => {
          const stamp = new Date().toISOString().slice(0, 10);
          this.downloadFile(blob, `expense-ledger-${stamp}.${format === 'excel' ? 'xlsx' : 'pdf'}`);
          this.toast.success(`Expense ledger ${format.toUpperCase()} exported.`);
        },
        error: (error) => this.toast.error(extractApiError(error))
      });
  }

  refresh(): void {
    const params = {
      pageNumber: 1,
      pageSize: 100,
      ...this.buildExportParams()
    };

    this.api.getExpenseLedger(params).subscribe({
      next: (page) => this.page.set(page),
      error: (error) => this.toast.error(extractApiError(error))
    });

    this.api.getExpenseSummary({}).subscribe({
      next: (summary) => this.summary.set(summary),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  loadLookups(): void {
    this.api.getExpenseCategoryLookup().subscribe({
      next: (categories) => this.categories.set(categories),
      error: (error) => this.toast.error(extractApiError(error))
    });

    this.api.getSupplierLookup().subscribe({
      next: (suppliers) => this.suppliers.set(suppliers),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  asValue(event: Event): string {
    return (event.target as HTMLSelectElement).value;
  }

  private buildExportParams(): Record<string, unknown> {
    return {
      search: this.search() || undefined,
      expenseCategoryId: this.selectedCategoryId() || undefined,
      sourceType: this.selectedSourceType() || undefined,
      pendingOnly: this.pendingOnly() || undefined
    };
  }

  private downloadFile(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    window.URL.revokeObjectURL(url);
  }
}
