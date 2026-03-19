import { CommonModule } from '@angular/common';
import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { ExpenseCategory, PagedResult } from '../../../core/models/app.models';
import { extractApiError } from '../../../core/utils/api-error.util';
import { setAppScrollLock } from '../../../core/utils/app-scroll-lock.util';
import { PortalApiService } from '../../services/portal-api.service';

@Component({
  selector: 'app-expense-categories-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppButtonComponent,
    AppCardComponent,
    AppConfirmDialogComponent,
    AppDataTableComponent,
    AppPageHeaderComponent
  ],
  template: `
    <app-page-header title="Expense Categories" subtitle="Maintain the category structure used by received invoices, rent, ledger summaries, and BPT mapping">
      <app-button (clicked)="openCreate()">New Category</app-button>
    </app-page-header>

    <app-card>
      <div class="toolbar">
        <input type="search" [value]="search()" (input)="onSearch($event)" placeholder="Search name or code">
        <select [value]="statusFilter()" (change)="statusFilter.set(asString($event)); refresh()">
          <option value="all">All categories</option>
          <option value="active">Active only</option>
          <option value="inactive">Inactive only</option>
        </select>
      </div>

      <app-data-table [hasData]="(page()?.items?.length ?? 0) > 0" emptyTitle="No expense categories" emptyDescription="Categories will also be auto-seeded for each tenant.">
        <thead>
          <tr>
            <th>Name</th>
            <th>Code</th>
            <th>BPT Mapping</th>
            <th>Status</th>
            <th>System</th>
            <th>Usage</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let category of page()?.items">
            <td>{{ category.name }}</td>
            <td>{{ category.code }}</td>
            <td>{{ category.bptCategoryCode }}</td>
            <td>{{ category.isActive ? 'Active' : 'Inactive' }}</td>
            <td>{{ category.isSystem ? 'Yes' : 'No' }}</td>
            <td>{{ category.usageCount }}</td>
            <td class="actions">
              <app-button size="sm" variant="secondary" (clicked)="edit(category)">Edit</app-button>
              <app-button *ngIf="isAdmin() && !category.isSystem" size="sm" variant="danger" (clicked)="confirmDelete(category)">Delete</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>
    </app-card>

    <div class="drawer" *ngIf="formOpen()">
      <app-card>
        <h3>{{ editId() ? 'Edit Expense Category' : 'New Expense Category' }}</h3>
        <form [formGroup]="form" (ngSubmit)="save()" class="form-grid" novalidate>
          <div class="two-col">
            <label>Name <input type="text" formControlName="name"></label>
            <label>Code <input type="text" formControlName="code"></label>
          </div>
          <div class="two-col">
            <label>BPT Category
              <select formControlName="bptCategoryCode">
                <option *ngFor="let option of bptOptions" [value]="option">{{ option }}</option>
              </select>
            </label>
            <label>Sort Order <input type="number" formControlName="sortOrder"></label>
          </div>
          <label>Description <textarea rows="3" formControlName="description"></textarea></label>
          <label class="checkbox"><input type="checkbox" formControlName="isActive"> Active category</label>
          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closeForm()">Cancel</app-button>
            <app-button type="submit">Save</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <app-confirm-dialog
      [open]="deleteDialogOpen()"
      title="Delete expense category"
      message="This deletes the category only if it is not in use."
      (cancel)="closeDeleteDialog()"
      (confirm)="deleteCategory()">
    </app-confirm-dialog>
  `,
  styles: `
    .toolbar, .two-col, .form-actions, .actions {
      display: flex;
      gap: .75rem;
      align-items: center;
      flex-wrap: wrap;
    }
    .toolbar {
      margin-bottom: .85rem;
      justify-content: space-between;
    }
    input, select, textarea {
      width: 100%;
      border: 1px solid #d7e0f3;
      border-radius: 14px;
      padding: .8rem .95rem;
      background: rgba(255,255,255,.92);
      font: inherit;
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
      width: min(760px, 100%);
      max-width: 100%;
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
    .checkbox {
      display: flex;
      gap: .55rem;
      align-items: center;
    }
    .checkbox input {
      width: auto;
    }
    .form-actions, .actions {
      justify-content: flex-end;
    }
  `
})
export class ExpenseCategoriesPageComponent implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);
  private readonly scrollLockOwner = {};

  readonly page = signal<PagedResult<ExpenseCategory> | null>(null);
  readonly search = signal('');
  readonly statusFilter = signal<'all' | 'active' | 'inactive'>('active');
  readonly formOpen = signal(false);
  readonly editId = signal<string | null>(null);
  readonly deleteDialogOpen = signal(false);
  readonly deleting = signal<ExpenseCategory | null>(null);
  private readonly overlayScrollLockEffect = effect((onCleanup) => {
    setAppScrollLock(this.scrollLockOwner, this.formOpen());
    onCleanup(() => setAppScrollLock(this.scrollLockOwner, false));
  });
  readonly bptOptions = [
    'Salary',
    'Rent',
    'LicenseAndRegistrationFees',
    'OfficeExpense',
    'RepairAndMaintenance',
    'DieselCharges',
    'HiredFerryCharges',
    'UtilitiesTelephoneInternet',
    'OfficeSupplies',
    'Insurance',
    'LegalAndProfessionalFees',
    'Travel',
    'Other'
  ];

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(160)]],
    code: ['', [Validators.required, Validators.maxLength(40)]],
    description: ['', Validators.maxLength(400)],
    bptCategoryCode: 'Other',
    isActive: true,
    sortOrder: 0
  });

  ngOnInit(): void {
    this.refresh();
  }

  isAdmin(): boolean {
    return this.auth.user()?.role === 'Admin';
  }

  onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
    this.refresh();
  }

  openCreate(): void {
    this.editId.set(null);
    this.form.reset({ name: '', code: '', description: '', bptCategoryCode: 'Other', isActive: true, sortOrder: 0 });
    this.formOpen.set(true);
  }

  edit(category: ExpenseCategory): void {
    this.editId.set(category.id);
    this.form.reset({
      name: category.name,
      code: category.code,
      description: category.description ?? '',
      bptCategoryCode: category.bptCategoryCode,
      isActive: category.isActive,
      sortOrder: category.sortOrder
    });
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error('Please complete required category fields.');
      return;
    }

    const raw = this.form.getRawValue();
    const payload = {
      ...raw,
      name: raw.name.trim(),
      code: raw.code.trim(),
      description: raw.description.trim(),
      sortOrder: Number(raw.sortOrder)
    };

    if (!payload.name || !payload.code) {
      this.form.controls.name.markAsTouched();
      this.form.controls.code.markAsTouched();
      this.toast.error('Name and code are required.');
      return;
    }

    const request$ = this.editId()
      ? this.api.updateExpenseCategory(this.editId()!, payload)
      : this.api.createExpenseCategory(payload);

    request$.subscribe({
      next: () => {
        this.toast.success(this.editId() ? 'Expense category updated.' : 'Expense category created.');
        this.formOpen.set(false);
        this.refresh();
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  confirmDelete(category: ExpenseCategory): void {
    this.deleting.set(category);
    this.deleteDialogOpen.set(true);
  }

  closeDeleteDialog(): void {
    this.deleteDialogOpen.set(false);
    this.deleting.set(null);
  }

  deleteCategory(): void {
    const category = this.deleting();
    if (!category) {
      return;
    }

    this.api.deleteExpenseCategory(category.id).subscribe({
      next: () => {
        this.toast.success('Expense category deleted.');
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
    const isActive = this.statusFilter() === 'all'
      ? undefined
      : this.statusFilter() === 'active';

    this.api.getExpenseCategories({
      pageNumber: 1,
      pageSize: 100,
      search: this.search() || undefined,
      isActive
    }).subscribe({
      next: (page) => this.page.set(page),
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  asString(event: Event): 'all' | 'active' | 'inactive' {
    return (event.target as HTMLSelectElement).value as 'all' | 'active' | 'inactive';
  }
}
