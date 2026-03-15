import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { PagedResult, Supplier } from '../../../core/models/app.models';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { PortalApiService } from '../../services/portal-api.service';

@Component({
  selector: 'app-suppliers-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppButtonComponent,
    AppCardComponent,
    AppConfirmDialogComponent,
    AppCurrencyPipe,
    AppDataTableComponent,
    AppPageHeaderComponent
  ],
  template: `
    <app-page-header title="Suppliers" subtitle="Manage vendor profiles used by received invoices and expense tracking">
      <app-button (clicked)="openCreate()">New Supplier</app-button>
    </app-page-header>

    <app-card>
      <div class="toolbar">
        <input type="search" [value]="search()" (input)="onSearch($event)" placeholder="Search supplier, TIN, phone, or email">
        <select [value]="statusFilter()" (change)="statusFilter.set(asString($event)); refresh()">
          <option value="all">All suppliers</option>
          <option value="active">Active only</option>
          <option value="inactive">Inactive only</option>
        </select>
      </div>

      <div class="summary" *ngIf="page() as data">
        Total suppliers: {{ data.totalCount }}
      </div>

      <app-data-table [hasData]="(page()?.items?.length ?? 0) > 0" emptyTitle="No suppliers" emptyDescription="Create your first supplier to start recording received invoices.">
        <thead>
          <tr>
            <th>Name</th>
            <th>TIN</th>
            <th>Contact</th>
            <th>Email</th>
            <th>Status</th>
            <th>Open Balance</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let supplier of page()?.items">
            <td>{{ supplier.name }}</td>
            <td>{{ supplier.tinNumber || '-' }}</td>
            <td>{{ supplier.contactNumber || '-' }}</td>
            <td>{{ supplier.email || '-' }}</td>
            <td>{{ supplier.isActive ? 'Active' : 'Inactive' }}</td>
            <td>{{ supplier.outstandingAmount | appCurrency:'MVR' }}</td>
            <td class="actions">
              <app-button size="sm" variant="secondary" (clicked)="edit(supplier)">Edit</app-button>
              <app-button *ngIf="isAdmin()" size="sm" variant="danger" (clicked)="confirmDelete(supplier)">Delete</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>

      <div class="pager" *ngIf="page() as data">
        <span>Page {{ data.pageNumber }} of {{ data.totalPages || 1 }}</span>
        <div class="actions">
          <app-button size="sm" variant="secondary" [disabled]="data.pageNumber <= 1" (clicked)="changePage(data.pageNumber - 1)">Prev</app-button>
          <app-button size="sm" variant="secondary" [disabled]="data.pageNumber >= data.totalPages" (clicked)="changePage(data.pageNumber + 1)">Next</app-button>
        </div>
      </div>
    </app-card>

    <div class="drawer" *ngIf="formOpen()">
      <app-card>
        <h3>{{ editId() ? 'Edit Supplier' : 'New Supplier' }}</h3>
        <form [formGroup]="form" (ngSubmit)="save()" class="form-grid">
          <div class="two-col">
            <label>Name <input type="text" formControlName="name"></label>
            <label>TIN Number <input type="text" formControlName="tinNumber"></label>
          </div>
          <div class="two-col">
            <label>Contact Number <input type="text" formControlName="contactNumber"></label>
            <label>Email <input type="email" formControlName="email"></label>
          </div>
          <label>Address <textarea rows="3" formControlName="address"></textarea></label>
          <label>Notes <textarea rows="3" formControlName="notes"></textarea></label>
          <label class="checkbox"><input type="checkbox" formControlName="isActive"> Active supplier</label>

          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closeForm()">Cancel</app-button>
            <app-button type="submit">Save</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <app-confirm-dialog
      [open]="deleteDialogOpen()"
      title="Delete supplier"
      message="This deletes the supplier record if it is not linked to any received invoices."
      (cancel)="closeDeleteDialog()"
      (confirm)="deleteSupplier()">
    </app-confirm-dialog>
  `,
  styles: `
    .toolbar, .pager, .actions, .two-col, .form-actions {
      display: flex;
      gap: .75rem;
      align-items: center;
      flex-wrap: wrap;
    }
    .toolbar, .pager {
      justify-content: space-between;
      margin-bottom: .85rem;
    }
    .summary {
      color: var(--text-muted);
      margin-bottom: .75rem;
      font-size: .95rem;
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
    .checkbox {
      display: flex;
      align-items: center;
      gap: .55rem;
    }
    .checkbox input {
      width: auto;
    }
    .form-actions {
      justify-content: flex-end;
    }
    .actions {
      justify-content: flex-end;
    }
  `
})
export class SuppliersPageComponent implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly page = signal<PagedResult<Supplier> | null>(null);
  readonly search = signal('');
  readonly statusFilter = signal<'all' | 'active' | 'inactive'>('active');
  readonly pageNumber = signal(1);
  readonly pageSize = 10;
  readonly formOpen = signal(false);
  readonly editId = signal<string | null>(null);
  readonly deleteDialogOpen = signal(false);
  readonly deleting = signal<Supplier | null>(null);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    tinNumber: ['', Validators.maxLength(100)],
    contactNumber: ['', Validators.maxLength(50)],
    email: ['', Validators.maxLength(200)],
    address: ['', Validators.maxLength(400)],
    notes: ['', Validators.maxLength(1000)],
    isActive: true
  });

  ngOnInit(): void {
    this.refresh();
  }

  isAdmin(): boolean {
    return this.auth.user()?.role === 'Admin';
  }

  onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
    this.pageNumber.set(1);
    this.refresh();
  }

  openCreate(): void {
    this.editId.set(null);
    this.form.reset({ name: '', tinNumber: '', contactNumber: '', email: '', address: '', notes: '', isActive: true });
    this.formOpen.set(true);
  }

  edit(supplier: Supplier): void {
    this.editId.set(supplier.id);
    this.form.reset({
      name: supplier.name,
      tinNumber: supplier.tinNumber ?? '',
      contactNumber: supplier.contactNumber ?? '',
      email: supplier.email ?? '',
      address: supplier.address ?? '',
      notes: supplier.notes ?? '',
      isActive: supplier.isActive
    });
    this.formOpen.set(true);
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
      ? this.api.updateSupplier(this.editId()!, payload)
      : this.api.createSupplier(payload);

    request$.subscribe({
      next: () => {
        this.toast.success(this.editId() ? 'Supplier updated.' : 'Supplier created.');
        this.formOpen.set(false);
        this.refresh();
      },
      error: (error) => this.toast.error(extractApiError(error))
    });
  }

  changePage(page: number): void {
    this.pageNumber.set(page);
    this.refresh();
  }

  confirmDelete(supplier: Supplier): void {
    this.deleting.set(supplier);
    this.deleteDialogOpen.set(true);
  }

  closeDeleteDialog(): void {
    this.deleteDialogOpen.set(false);
    this.deleting.set(null);
  }

  deleteSupplier(): void {
    const supplier = this.deleting();
    if (!supplier) {
      return;
    }

    this.api.deleteSupplier(supplier.id).subscribe({
      next: () => {
        this.toast.success('Supplier deleted.');
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

    this.api.getSuppliers({
      pageNumber: this.pageNumber(),
      pageSize: this.pageSize,
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
