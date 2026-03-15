import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { ExpenseCategoryLookup, PagedResult, RentEntry, RentEntryListItem } from '../../../core/models/app.models';
import { extractApiError } from '../../../core/utils/api-error.util';
import { PortalApiService } from '../../services/portal-api.service';

@Component({
  selector: 'app-rent-page',
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
    <app-page-header title="Rent" subtitle="Track rent expenses so they feed directly into the expense ledger and summaries">
      <app-button (clicked)="openCreate()">New Rent Entry</app-button>
    </app-page-header>

    <app-card>
      <div class="toolbar">
        <app-search-bar [value]="search()" placeholder="Search rent no, property, or payee" (searchChange)="onSearchChange($event)"></app-search-bar>
        <div class="actions">
          <select [value]="selectedCategoryId()" (change)="selectedCategoryId.set(asValue($event)); refresh()">
            <option value="">All categories</option>
            <option *ngFor="let category of categories()" [value]="category.id">{{ category.name }}</option>
          </select>
          <app-button variant="secondary" (clicked)="clearFilters()">Clear</app-button>
        </div>
      </div>

      <app-data-table [hasData]="(page()?.items?.length ?? 0) > 0" emptyTitle="No rent entries" emptyDescription="Create a rent expense to keep it in the ledger and BPT rollups.">
        <thead>
          <tr>
            <th>Rent No</th>
            <th>Date</th>
            <th>Property</th>
            <th>Pay To</th>
            <th>Category</th>
            <th>Amount</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let item of page()?.items">
            <td>{{ item.rentNumber }}</td>
            <td><app-date-badge [value]="item.date"></app-date-badge></td>
            <td>{{ item.propertyName }}</td>
            <td>{{ item.payTo }}</td>
            <td>{{ item.expenseCategoryName }}</td>
            <td>{{ item.amount | appCurrency:item.currency }}</td>
            <td>{{ item.approvalStatus }}</td>
            <td class="actions">
              <app-button size="sm" variant="secondary" (clicked)="edit(item)">Edit</app-button>
              <app-button *ngIf="isAdmin()" size="sm" variant="danger" (clicked)="confirmDelete(item)">Delete</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>
    </app-card>

    <div class="drawer" *ngIf="formOpen()">
      <app-card>
        <h3>{{ editId() ? 'Edit Rent Entry' : 'New Rent Entry' }}</h3>
        <form [formGroup]="form" (ngSubmit)="save()" class="form-grid">
          <div class="two-col">
            <label>Date <input type="date" formControlName="date"></label>
            <label>Currency
              <select formControlName="currency">
                <option value="MVR">MVR</option>
                <option value="USD">USD</option>
              </select>
            </label>
          </div>
          <div class="two-col">
            <label>Property Name <input type="text" formControlName="propertyName"></label>
            <label>Pay To <input type="text" formControlName="payTo"></label>
          </div>
          <div class="two-col">
            <label>Expense Category
              <select formControlName="expenseCategoryId">
                <option value="">Select category</option>
                <option *ngFor="let category of categories()" [value]="category.id">{{ category.name }}</option>
              </select>
            </label>
            <label>Amount <input type="number" formControlName="amount" step="0.01"></label>
          </div>
          <div class="two-col">
            <label>Approval Status
              <select formControlName="approvalStatus">
                <option value="Approved">Approved</option>
                <option value="Draft">Draft</option>
                <option value="Rejected">Rejected</option>
              </select>
            </label>
          </div>
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
      title="Delete rent entry"
      message="This permanently deletes the rent expense."
      (cancel)="closeDeleteDialog()"
      (confirm)="deleteRent()">
    </app-confirm-dialog>
  `,
  styles: `
    .toolbar, .actions, .two-col, .form-actions {
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
      width: min(780px, 100%);
      max-height: calc(100vh - 2rem);
      overflow: auto;
    }
    .form-grid {
      display: grid;
      gap: .9rem;
    }
    .two-col > label {
      flex: 1 1 280px;
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
    .form-actions, .actions {
      justify-content: flex-end;
    }
  `
})
export class RentPageComponent implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly page = signal<PagedResult<RentEntryListItem> | null>(null);
  readonly categories = signal<ExpenseCategoryLookup[]>([]);
  readonly search = signal('');
  readonly selectedCategoryId = signal('');
  readonly formOpen = signal(false);
  readonly editId = signal<string | null>(null);
  readonly deleteDialogOpen = signal(false);
  readonly deleting = signal<RentEntryListItem | null>(null);

  readonly form = this.fb.nonNullable.group({
    date: [new Date().toISOString().slice(0, 10), Validators.required],
    propertyName: ['', [Validators.required, Validators.maxLength(200)]],
    payTo: ['', [Validators.required, Validators.maxLength(200)]],
    currency: 'MVR',
    amount: 0,
    expenseCategoryId: ['', Validators.required],
    approvalStatus: 'Approved',
    notes: ['', Validators.maxLength(1000)]
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

  clearFilters(): void {
    this.search.set('');
    this.selectedCategoryId.set('');
    this.refresh();
  }

  openCreate(): void {
    this.editId.set(null);
    this.form.reset({
      date: new Date().toISOString().slice(0, 10),
      propertyName: '',
      payTo: '',
      currency: 'MVR',
      amount: 0,
      expenseCategoryId: '',
      approvalStatus: 'Approved',
      notes: ''
    });
    this.formOpen.set(true);
  }

  edit(item: RentEntryListItem): void {
    this.api.getRentEntryById(item.id).subscribe({
      next: (entry) => {
        this.editId.set(entry.id);
        this.form.reset({
          date: entry.date,
          propertyName: entry.propertyName,
          payTo: entry.payTo,
          currency: entry.currency,
          amount: entry.amount,
          expenseCategoryId: entry.expenseCategoryId,
          approvalStatus: entry.approvalStatus,
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
      ? this.api.updateRentEntry(this.editId()!, payload)
      : this.api.createRentEntry(payload);

    request$.subscribe({
      next: () => {
        this.toast.success(this.editId() ? 'Rent entry updated.' : 'Rent entry created.');
        this.formOpen.set(false);
        this.refresh();
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  confirmDelete(item: RentEntryListItem): void {
    this.deleting.set(item);
    this.deleteDialogOpen.set(true);
  }

  closeDeleteDialog(): void {
    this.deleteDialogOpen.set(false);
    this.deleting.set(null);
  }

  deleteRent(): void {
    const item = this.deleting();
    if (!item) {
      return;
    }

    this.api.deleteRentEntry(item.id).subscribe({
      next: () => {
        this.toast.success('Rent entry deleted.');
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
    this.api.getRentEntries({
      pageNumber: 1,
      pageSize: 100,
      search: this.search() || undefined,
      expenseCategoryId: this.selectedCategoryId() || undefined
    }).subscribe({
      next: (page) => this.page.set(page),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  loadLookups(): void {
    this.api.getExpenseCategoryLookup().subscribe({
      next: (categories) => this.categories.set(categories),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  asValue(event: Event): string {
    return (event.target as HTMLSelectElement).value;
  }
}
