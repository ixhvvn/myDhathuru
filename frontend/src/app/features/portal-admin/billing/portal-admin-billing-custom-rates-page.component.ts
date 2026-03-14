import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  PortalAdminBillingBusinessOption,
  PortalAdminBillingCustomRate,
  PortalAdminBillingUpsertCustomRateRequest
} from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';

@Component({
  selector: 'app-portal-admin-billing-custom-rates-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppEmptyStateComponent, AppLoaderComponent],
  template: `
    <section class="page-head">
      <h1>Custom Rates</h1>
      <p>Set business-level billing overrides for software, vessel, and staff pricing.</p>
    </section>

    <app-card class="filter-card">
      <form [formGroup]="filterForm" class="filters" (ngSubmit)="applyFilters()">
        <label>Search <input type="text" formControlName="search" placeholder="Business name or email"></label>
        <label>
          Status
          <select formControlName="isActive">
            <option value="">All</option>
            <option value="true">Active</option>
            <option value="false">Inactive</option>
          </select>
        </label>
        <label class="per-page">
          Per Page
          <select formControlName="pageSize">
            <option [ngValue]="10">10</option>
            <option [ngValue]="20">20</option>
            <option [ngValue]="50">50</option>
            <option [ngValue]="80">80</option>
            <option [ngValue]="100">100</option>
          </select>
        </label>
        <div class="actions">
          <app-button type="submit">Generate</app-button>
          <app-button type="button" variant="secondary" (clicked)="resetFilters()">Reset</app-button>
          <app-button type="button" variant="success" (clicked)="openCreate()">Add Custom Rate</app-button>
        </div>
      </form>
    </app-card>

    <app-loader *ngIf="loading()"></app-loader>

    <app-card *ngIf="!loading()" class="results-card">
      <app-empty-state
        *ngIf="rows().length === 0"
        title="No custom rates configured"
        description="Create business-specific overrides to replace global default billing rates.">
      </app-empty-state>

      <div class="table-wrap" *ngIf="rows().length > 0">
        <table>
          <thead>
            <tr>
              <th>Business</th>
              <th>Software Fee</th>
              <th>Vessel Fee</th>
              <th>Staff Fee</th>
              <th>Currency</th>
              <th>Status</th>
              <th>Effective</th>
              <th class="action-col">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let row of rows()">
              <td>{{ row.companyName }}</td>
              <td>{{ row.softwareFee | number:'1.2-2' }}</td>
              <td>{{ row.vesselFee | number:'1.2-2' }}</td>
              <td>{{ row.staffFee | number:'1.2-2' }}</td>
              <td>{{ row.currency }}</td>
              <td><span class="status" [attr.data-active]="row.isActive">{{ row.isActive ? 'Active' : 'Inactive' }}</span></td>
              <td>{{ row.effectiveFrom || '-' }} to {{ row.effectiveTo || '-' }}</td>
              <td class="actions-cell">
                <app-button size="sm" variant="secondary" (clicked)="openEdit(row)">Edit</app-button>
                <app-button size="sm" variant="danger" (clicked)="remove(row)">Delete</app-button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="results-meta" *ngIf="rows().length > 0">
        Total {{ totalCount() }} record{{ totalCount() === 1 ? '' : 's' }}
      </div>

      <div class="pagination" *ngIf="totalCount() > pageSize()">
        <button type="button" [disabled]="page() <= 1" (click)="changePage(page() - 1)">Prev</button>
        <span>Page {{ page() }} of {{ totalPages() }}</span>
        <button type="button" [disabled]="page() >= totalPages()" (click)="changePage(page() + 1)">Next</button>
      </div>
    </app-card>

    <div class="modal-backdrop" *ngIf="editing()"></div>
    <section class="modal" *ngIf="editing()">
      <h2>{{ editingId() ? 'Edit Custom Rate' : 'Add Custom Rate' }}</h2>
      <form [formGroup]="form" (ngSubmit)="save()">
        <label>
          Business
          <select formControlName="tenantId">
            <option value="">Select business</option>
            <option *ngFor="let business of businesses()" [value]="business.tenantId">
              {{ business.companyName }}
            </option>
          </select>
        </label>
        <label>Software Fee <input type="number" step="0.01" formControlName="softwareFee"></label>
        <label>Vessel Fee <input type="number" step="0.01" formControlName="vesselFee"></label>
        <label>Staff Fee <input type="number" step="0.01" formControlName="staffFee"></label>
        <label>
          Currency
          <select formControlName="currency">
            <option value="MVR">MVR</option>
            <option value="USD">USD</option>
          </select>
        </label>
        <label>Effective From <input type="date" formControlName="effectiveFrom"></label>
        <label>Effective To <input type="date" formControlName="effectiveTo"></label>
        <label class="checkbox"><input type="checkbox" formControlName="isActive"> Active custom rate</label>
        <label class="full">Notes <textarea rows="3" formControlName="notes"></textarea></label>

        <div class="modal-actions">
          <app-button type="button" variant="secondary" (clicked)="closeModal()">Cancel</app-button>
          <app-button type="submit" [loading]="saving()">Save</app-button>
        </div>
      </form>
    </section>
  `,
  styles: `
    .page-head h1 { margin: 0; color: #2f4269; font-family: var(--font-heading); font-size: 1.48rem; font-weight: 600; }
    .page-head p { margin: .3rem 0 0; color: #62749d; }
    .filter-card { margin-top: .8rem; --card-padding: .82rem; }
    .results-card { margin-top: .8rem; }
    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: .56rem;
      align-items: end;
    }
    .filters > label {
      min-width: min(220px, 100%);
      flex: 1 1 220px;
    }
    .filters > label.per-page {
      min-width: 140px;
      flex: 0 1 160px;
    }
    label { display: grid; gap: .2rem; color: #5f739d; font-size: .78rem; font-family: var(--font-heading); font-weight: 600; }
    input:not([type='checkbox']), select, textarea {
      border: 1px solid #ccdaf5;
      border-radius: 11px;
      background: #fff;
      color: #34496e;
      padding: .55rem .63rem;
      font-size: .86rem;
      min-height: 41px;
    }
    input:not([type='checkbox']):focus, select:focus, textarea:focus {
      outline: none;
      border-color: #7f8df5;
      box-shadow: 0 0 0 3px rgba(126, 140, 245, .16);
    }
    .checkbox input[type='checkbox'] {
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
    .checkbox input[type='checkbox']:focus {
      outline: none;
      box-shadow: 0 0 0 3px rgba(126, 140, 245, .16);
    }
    .actions {
      display: flex;
      gap: .42rem;
      flex-wrap: wrap;
      min-width: 180px;
      justify-content: flex-end;
      margin-left: auto;
    }
    .table-wrap { border: 1px solid #d9e3fa; border-radius: 14px; overflow: auto; }
    .results-meta { margin-top: .56rem; color: #5f739d; font-size: .83rem; }
    table { width: 100%; min-width: 920px; border-collapse: collapse; font-size: .83rem; }
    th, td { padding: .5rem .58rem; border-bottom: 1px solid #e2e9fc; color: #42567f; text-align: left; }
    th {
      background: #f3f7ff;
      color: #5e74a2;
      text-transform: uppercase;
      letter-spacing: .04em;
      font-family: var(--font-heading);
      font-size: .73rem;
      font-weight: 600;
      white-space: nowrap;
    }
    .status {
      display: inline-flex;
      border-radius: 999px;
      padding: .2rem .46rem;
      border: 1px solid transparent;
      font-size: .72rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .status[data-active='true'] { color: #2f9870; border-color: rgba(123, 215, 179, .5); background: rgba(209, 245, 224, .75); }
    .status[data-active='false'] { color: #b54d6c; border-color: rgba(231, 155, 179, .5); background: rgba(255, 220, 232, .75); }
    .action-col {
      width: 1%;
      min-width: 168px;
      white-space: nowrap;
    }
    .actions-cell {
      display: flex;
      flex-direction: row;
      align-items: center;
      gap: .34rem;
      justify-content: flex-start;
      flex-wrap: nowrap;
      white-space: normal;
      min-width: 0;
    }
    .pagination {
      margin-top: .62rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      color: #61749d;
      font-size: .83rem;
    }
    .pagination button {
      border: 1px solid #d5e0f7;
      border-radius: 10px;
      background: linear-gradient(145deg, #f5f8ff, #edf3ff);
      color: #50638a;
      font-family: var(--font-heading);
      font-weight: 600;
      padding: .4rem .62rem;
      cursor: pointer;
    }
    .pagination button:disabled { opacity: .56; cursor: not-allowed; }
    .modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(35, 47, 79, .4);
      backdrop-filter: blur(2px);
      z-index: 120;
    }
    .modal {
      position: fixed;
      z-index: 121;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      width: min(720px, calc(100vw - 1.1rem));
      max-height: calc(100dvh - 1.1rem);
      overflow: auto;
      border-radius: 18px;
      border: 1px solid #d5e1fa;
      background: #f9fbff;
      box-shadow: 0 30px 70px rgba(56, 73, 118, .34);
      padding: .95rem;
    }
    .modal h2 { margin: 0 0 .55rem; color: #2f4269; font-family: var(--font-heading); font-size: 1.1rem; font-weight: 600; }
    .modal form {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: .54rem .6rem;
    }
    .modal label.full { grid-column: 1 / -1; }
    .checkbox {
      display: flex;
      align-items: center;
      gap: .46rem;
      border: 1px solid #d8e2f9;
      border-radius: 11px;
      background: #fff;
      padding: .5rem .58rem;
      min-height: 41px;
      color: #4a5f88;
      font-size: .82rem;
    }
    .modal-actions { grid-column: 1 / -1; display: flex; justify-content: flex-end; gap: .46rem; margin-top: .12rem; }
    @media (max-width: 1240px) {
      .filters > label {
        flex: 1 1 calc(50% - .56rem);
      }
      .actions {
        width: 100%;
        margin-left: 0;
        justify-content: flex-start;
      }
    }
    @media (max-width: 760px) {
      .filters > label {
        flex: 1 1 100%;
        min-width: 0;
      }
      .actions { width: 100%; }
      .modal form { grid-template-columns: 1fr; }
    }
  `
})
export class PortalAdminBillingCustomRatesPageComponent {
  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly rows = signal<PortalAdminBillingCustomRate[]>([]);
  readonly businesses = signal<PortalAdminBillingBusinessOption[]>([]);
  readonly editing = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);

  readonly filterForm = this.fb.nonNullable.group({
    search: [''],
    isActive: [''],
    pageSize: [10]
  });

  readonly form = this.fb.nonNullable.group({
    tenantId: ['', [Validators.required]],
    softwareFee: [2500, [Validators.required, Validators.min(0)]],
    vesselFee: [1000, [Validators.required, Validators.min(0)]],
    staffFee: [250, [Validators.required, Validators.min(0)]],
    currency: ['MVR' as 'MVR' | 'USD', [Validators.required]],
    isActive: [true],
    effectiveFrom: [''],
    effectiveTo: [''],
    notes: ['', [Validators.maxLength(500)]]
  });

  constructor() {
    this.loadBusinesses();
    this.load();
  }

  applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  resetFilters(): void {
    this.filterForm.reset({
      search: '',
      isActive: '',
      pageSize: 10
    });
    this.page.set(1);
    this.pageSize.set(10);
    this.load();
  }

  changePage(nextPage: number): void {
    this.page.set(nextPage);
    this.load();
  }

  openCreate(): void {
    this.editingId.set(null);
    this.form.reset({
      tenantId: '',
      softwareFee: 2500,
      vesselFee: 1000,
      staffFee: 250,
      currency: 'MVR',
      isActive: true,
      effectiveFrom: '',
      effectiveTo: '',
      notes: ''
    });
    this.editing.set(true);
  }

  openEdit(rate: PortalAdminBillingCustomRate): void {
    this.editingId.set(rate.id);
    this.form.reset({
      tenantId: rate.tenantId,
      softwareFee: rate.softwareFee,
      vesselFee: rate.vesselFee,
      staffFee: rate.staffFee,
      currency: rate.currency,
      isActive: rate.isActive,
      effectiveFrom: rate.effectiveFrom ?? '',
      effectiveTo: rate.effectiveTo ?? '',
      notes: rate.notes ?? ''
    });
    this.editing.set(true);
  }

  closeModal(): void {
    if (this.saving()) {
      return;
    }
    this.editing.set(false);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error('Please complete the custom rate form.');
      return;
    }

    const raw = this.form.getRawValue();
    const payload: PortalAdminBillingUpsertCustomRateRequest = {
      tenantId: raw.tenantId,
      softwareFee: raw.softwareFee,
      vesselFee: raw.vesselFee,
      staffFee: raw.staffFee,
      currency: raw.currency,
      isActive: raw.isActive,
      effectiveFrom: raw.effectiveFrom || undefined,
      effectiveTo: raw.effectiveTo || undefined,
      notes: raw.notes.trim() || undefined
    };

    const request$ = this.editingId()
      ? this.api.updateBillingCustomRate(this.editingId()!, payload)
      : this.api.createBillingCustomRate(payload);

    this.saving.set(true);
    request$
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.toast.success(this.editingId() ? 'Custom rate updated.' : 'Custom rate created.');
          this.editing.set(false);
          this.load();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to save custom rate.'))
      });
  }

  remove(rate: PortalAdminBillingCustomRate): void {
    const confirmed = typeof window !== 'undefined'
      ? window.confirm(`Delete custom rate for ${rate.companyName}?`)
      : true;
    if (!confirmed) {
      return;
    }

    this.api.deleteBillingCustomRate(rate.id).subscribe({
      next: () => {
        this.toast.success('Custom rate deleted.');
        this.load();
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to delete custom rate.'))
    });
  }

  private load(): void {
    this.loading.set(true);
    const filter = this.filterForm.getRawValue();
    const selectedPageSize = this.resolvePageSize(filter.pageSize);
    this.pageSize.set(selectedPageSize);
    this.api.getBillingCustomRates({
      search: filter.search.trim(),
      isActive: filter.isActive === '' ? undefined : filter.isActive,
      pageNumber: this.page(),
      pageSize: selectedPageSize
    })
    .pipe(finalize(() => this.loading.set(false)))
    .subscribe({
      next: (result) => {
        this.rows.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(Math.max(1, result.totalPages));
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load custom rates.'))
    });
  }

  private resolvePageSize(pageSize: unknown): number {
    const parsed = Number(pageSize);
    return parsed === 10 || parsed === 20 || parsed === 50 || parsed === 80 || parsed === 100 ? parsed : 10;
  }

  private loadBusinesses(): void {
    this.api.getBillingBusinessOptions().subscribe({
      next: (result) => this.businesses.set(result),
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load businesses for custom rates.'))
    });
  }
}



