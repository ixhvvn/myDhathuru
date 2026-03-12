import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { PortalAdminBusinessDetail, PortalAdminBusinessListItem } from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { NAME_REGEX, PHONE_REGEX } from '../../../core/validators/input-patterns';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';

@Component({
  selector: 'app-portal-admin-businesses-page',
  standalone: true,
  imports: [CommonModule, DatePipe, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppEmptyStateComponent, AppLoaderComponent],
  template: `
    <section class="page-head">
      <h1>Businesses</h1>
      <p>Manage tenant business accounts, login details, access status, and reset actions.</p>
    </section>

    <app-card class="filter-card">
      <form [formGroup]="filterForm" class="filters" (ngSubmit)="applyFilters()">
        <label>
          Search
          <input type="text" formControlName="search" placeholder="Company, email, phone, registration">
        </label>
        <label>
          Status
          <select formControlName="status">
            <option value="">All statuses</option>
            <option value="Active">Active</option>
            <option value="Disabled">Disabled</option>
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
        </div>
      </form>
    </app-card>

    <app-loader *ngIf="loading()"></app-loader>

    <app-card *ngIf="!loading()">
      <app-empty-state
        *ngIf="rows().length === 0"
        title="No businesses found"
        description="No records match the selected filter.">
      </app-empty-state>

      <div class="table-wrap" *ngIf="rows().length > 0">
        <table>
          <thead>
            <tr>
              <th>Company</th>
              <th>Status</th>
              <th>Staff</th>
              <th>Vessels</th>
              <th>Created</th>
              <th>Last Activity</th>
              <th class="action-col">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let row of rows()">
              <td>
                <strong>{{ row.companyName }}</strong>
                <small>{{ row.companyEmail }}</small>
                <small>{{ row.companyPhone }}</small>
              </td>
              <td>
                <span class="status" [attr.data-status]="row.status">{{ row.status }}</span>
              </td>
              <td>{{ row.staffCount }}</td>
              <td>{{ row.vesselCount }}</td>
              <td>{{ row.createdAt | date:'yyyy-MM-dd' }}</td>
              <td>{{ row.lastActivityAt ? (row.lastActivityAt | date:'yyyy-MM-dd HH:mm') : '-' }}</td>
              <td class="actions-cell">
                <app-button size="sm" variant="secondary" (clicked)="viewDetails(row.tenantId)">View</app-button>
                <app-button size="sm" variant="secondary" (clicked)="editBusiness(row.tenantId)">Edit Login</app-button>
                <app-button size="sm" variant="warning" (clicked)="sendResetLink(row.tenantId)">Send Reset</app-button>
                <app-button *ngIf="row.status === 'Active'" size="sm" variant="danger" (clicked)="openDisableModal(row)">Disable</app-button>
                <app-button *ngIf="row.status === 'Disabled'" size="sm" variant="success" (clicked)="enableBusiness(row)">Enable</app-button>
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

    <div class="modal-backdrop" *ngIf="detail() || editDetail() || disableTarget()"></div>

    <section class="modal" *ngIf="detail() as selected">
      <h2>Business Details</h2>
      <dl>
        <div><dt>Company</dt><dd>{{ selected.companyName }}</dd></div>
        <div><dt>Email</dt><dd>{{ selected.companyEmail }}</dd></div>
        <div><dt>Phone</dt><dd>{{ selected.companyPhone }}</dd></div>
        <div><dt>TIN</dt><dd>{{ selected.tinNumber }}</dd></div>
        <div><dt>Business Registration</dt><dd>{{ selected.businessRegistrationNumber }}</dd></div>
        <div><dt>Status</dt><dd>{{ selected.status }}</dd></div>
        <div><dt>Primary Admin</dt><dd>{{ selected.primaryAdmin?.fullName || '-' }} ({{ selected.primaryAdmin?.email || '-' }})</dd></div>
        <div><dt>Staff Count</dt><dd>{{ selected.staffCount }}</dd></div>
        <div><dt>Vessel Count</dt><dd>{{ selected.vesselCount }}</dd></div>
        <div><dt>Customer Count</dt><dd>{{ selected.customerCount }}</dd></div>
        <div><dt>Invoice Count</dt><dd>{{ selected.invoiceCount }}</dd></div>
        <div *ngIf="selected.disabledReason"><dt>Disabled Reason</dt><dd>{{ selected.disabledReason }}</dd></div>
      </dl>
      <div class="modal-actions">
        <app-button variant="secondary" (clicked)="detail.set(null)">Close</app-button>
      </div>
    </section>

    <section class="modal" *ngIf="editDetail() as selected">
      <h2>Edit Business Login Details</h2>
      <form [formGroup]="editForm" (ngSubmit)="saveEdit(selected.tenantId)">
        <label>
          Admin Full Name
          <input type="text" formControlName="adminFullName">
        </label>
        <small class="error" *ngIf="editForm.controls.adminFullName.touched && editForm.controls.adminFullName.hasError('pattern')">
          Name must not contain numbers.
        </small>
        <label>
          Admin Login Email
          <input type="email" formControlName="adminLoginEmail">
        </label>
        <small class="error" *ngIf="editForm.controls.adminLoginEmail.touched && editForm.controls.adminLoginEmail.hasError('email')">
          Enter a valid email.
        </small>
        <label>
          Company Email
          <input type="email" formControlName="companyEmail">
        </label>
        <label>
          Company Phone
          <input type="text" formControlName="companyPhone">
        </label>
        <small class="error" *ngIf="editForm.controls.companyPhone.touched && editForm.controls.companyPhone.hasError('pattern')">
          Phone must contain only digits.
        </small>
        <div class="modal-actions">
          <app-button type="button" variant="secondary" (clicked)="cancelEdit()">Cancel</app-button>
          <app-button type="submit" [loading]="actionLoading()">Save</app-button>
        </div>
      </form>
    </section>

    <section class="modal" *ngIf="disableTarget() as selected">
      <h2>Disable Business Account</h2>
      <p>Disabled businesses cannot log in until re-enabled by portal admin.</p>
      <form [formGroup]="disableForm" (ngSubmit)="confirmDisable(selected.tenantId)">
        <label>
          Reason (optional)
          <textarea rows="4" formControlName="reason" placeholder="Reason for disabling"></textarea>
        </label>
        <div class="modal-actions">
          <app-button type="button" variant="secondary" (clicked)="disableTarget.set(null)">Cancel</app-button>
          <app-button type="submit" variant="danger" [loading]="actionLoading()">Disable</app-button>
        </div>
      </form>
    </section>
  `,
  styles: `
    .page-head h1 { margin: 0; font-family: var(--font-heading); color: #2f4269; font-size: 1.46rem; font-weight: 600; }
    .page-head p { margin: .3rem 0 0; color: #61739a; }
    .filter-card { margin-top: .75rem; --card-padding: .8rem; }
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
    label {
      display: grid;
      gap: .22rem;
      color: #5f739d;
      font-size: .78rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    input, select, textarea {
      border: 1px solid #ccdaf5;
      border-radius: 10px;
      background: #fff;
      padding: .56rem .65rem;
      font-size: .87rem;
      color: #34486d;
    }
    input:focus, select:focus, textarea:focus {
      outline: none;
      border-color: #7e8df7;
      box-shadow: 0 0 0 3px rgba(126,141,247,.16);
    }
    .actions {
      display: flex;
      gap: .42rem;
      flex-wrap: wrap;
      min-width: 140px;
      justify-content: flex-end;
      margin-left: auto;
    }
    .table-wrap {
      border: 1px solid #d9e3fa;
      border-radius: 14px;
      overflow: auto;
      margin-top: .2rem;
    }
    .results-meta {
      margin-top: .56rem;
      color: #5f739d;
      font-size: .83rem;
    }
    table {
      width: 100%;
      min-width: 1050px;
      border-collapse: collapse;
      font-size: .84rem;
    }
    th, td {
      padding: .5rem .58rem;
      border-bottom: 1px solid #e0e8fb;
      color: #415680;
      vertical-align: top;
    }
    th {
      background: #f3f7ff;
      color: #5e74a1;
      text-transform: uppercase;
      letter-spacing: .04em;
      font-family: var(--font-heading);
      font-size: .74rem;
      font-weight: 600;
      white-space: nowrap;
    }
    td strong {
      display: block;
      color: #2f4369;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .82rem;
    }
    td small {
      display: block;
      color: #63779f;
      font-size: .75rem;
      line-height: 1.35;
    }
    .status {
      display: inline-flex;
      border-radius: 999px;
      padding: .22rem .48rem;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .72rem;
      border: 1px solid transparent;
    }
    .status[data-status='Active'] {
      color: #2f9870;
      border-color: rgba(124, 215, 180, .5);
      background: rgba(208, 245, 225, .74);
    }
    .status[data-status='Disabled'] {
      color: #b44c6b;
      border-color: rgba(231, 154, 178, .5);
      background: rgba(255, 219, 230, .74);
    }
    .action-col {
      width: 1%;
      min-width: 132px;
      white-space: nowrap;
    }
    .actions-cell {
      display: grid;
      gap: .32rem;
      justify-items: start;
      align-content: start;
      white-space: normal;
      min-width: 0;
    }
    .pagination {
      margin-top: .62rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      color: #5f739d;
      font-size: .84rem;
    }
    .pagination button {
      border: 1px solid #d6e1f8;
      background: linear-gradient(140deg, #f6f9ff, #edf4ff);
      color: #50638a;
      border-radius: 10px;
      padding: .42rem .62rem;
      font-family: var(--font-heading);
      font-size: .8rem;
      font-weight: 600;
      cursor: pointer;
    }
    .pagination button:disabled {
      opacity: .55;
      cursor: not-allowed;
    }
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
      width: min(680px, calc(100vw - 1.2rem));
      max-height: calc(100dvh - 1.2rem);
      overflow: auto;
      border-radius: 18px;
      border: 1px solid #d5e1fa;
      background: #f9fbff;
      box-shadow: 0 30px 70px rgba(56, 73, 118, .34);
      padding: .9rem;
    }
    .modal h2 {
      margin: 0 0 .5rem;
      color: #2f4269;
      font-family: var(--font-heading);
      font-size: 1.08rem;
      font-weight: 600;
    }
    .modal p {
      margin: 0 0 .56rem;
      color: #5f739d;
      font-size: .86rem;
    }
    dl {
      margin: 0;
      display: grid;
      gap: .38rem;
    }
    dl > div {
      border: 1px solid #dce6fb;
      border-radius: 10px;
      background: #fff;
      padding: .48rem .56rem;
    }
    dt {
      margin: 0;
      color: #60739e;
      font-size: .74rem;
      font-family: var(--font-heading);
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: .03em;
    }
    dd {
      margin: .2rem 0 0;
      color: #33486c;
      font-size: .88rem;
    }
    .error {
      color: #c14d71;
      font-size: .76rem;
      margin-top: -.26rem;
    }
    .modal-actions {
      margin-top: .62rem;
      display: flex;
      justify-content: flex-end;
      gap: .46rem;
    }
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
    @media (max-width: 980px) {
      .filters > label {
        flex: 1 1 100%;
        min-width: 0;
      }
      .actions { width: 100%; justify-content: flex-start; }
    }
  `
})
export class PortalAdminBusinessesPageComponent {
  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly actionLoading = signal(false);
  readonly rows = signal<PortalAdminBusinessListItem[]>([]);
  readonly detail = signal<PortalAdminBusinessDetail | null>(null);
  readonly editDetail = signal<PortalAdminBusinessDetail | null>(null);
  readonly disableTarget = signal<PortalAdminBusinessListItem | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);

  readonly filterForm = this.fb.nonNullable.group({
    search: [''],
    status: [''],
    pageSize: [10]
  });

  readonly disableForm = this.fb.nonNullable.group({
    reason: ['', [Validators.maxLength(300)]]
  });

  readonly editForm = this.fb.nonNullable.group({
    adminFullName: ['', [Validators.required, Validators.pattern(NAME_REGEX)]],
    adminLoginEmail: ['', [Validators.required, Validators.email]],
    companyEmail: ['', [Validators.required, Validators.email]],
    companyPhone: ['', [Validators.required, Validators.pattern(PHONE_REGEX)]]
  });

  constructor() {
    this.load();
  }

  applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  resetFilters(): void {
    this.filterForm.reset({
      search: '',
      status: '',
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

  viewDetails(tenantId: string): void {
    this.api.getBusinessById(tenantId).subscribe({
      next: (result) => this.detail.set(result),
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load business details.'))
    });
  }

  editBusiness(tenantId: string): void {
    this.api.getBusinessById(tenantId).subscribe({
      next: (result) => {
        this.editDetail.set(result);
        this.editForm.reset({
          adminFullName: result.primaryAdmin?.fullName || '',
          adminLoginEmail: result.primaryAdmin?.email || '',
          companyEmail: result.companyEmail,
          companyPhone: result.companyPhone
        });
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load business login details.'))
    });
  }

  cancelEdit(): void {
    if (this.actionLoading()) {
      return;
    }
    this.editDetail.set(null);
  }

  saveEdit(tenantId: string): void {
    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }

    this.actionLoading.set(true);
    this.api.updateBusinessLoginDetails(tenantId, this.editForm.getRawValue())
      .pipe(finalize(() => this.actionLoading.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Business login details updated.');
          this.editDetail.set(null);
          this.load();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to update login details.'))
      });
  }

  openDisableModal(row: PortalAdminBusinessListItem): void {
    this.disableForm.reset({ reason: '' });
    this.disableTarget.set(row);
  }

  confirmDisable(tenantId: string): void {
    this.actionLoading.set(true);
    this.api.disableBusiness(tenantId, this.disableForm.getRawValue().reason)
      .pipe(finalize(() => this.actionLoading.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Business account disabled.');
          this.disableTarget.set(null);
          this.load();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to disable business account.'))
      });
  }

  enableBusiness(row: PortalAdminBusinessListItem): void {
    const confirmed = typeof window !== 'undefined'
      ? window.confirm(`Enable business account for ${row.companyName}?`)
      : true;

    if (!confirmed) {
      return;
    }

    this.actionLoading.set(true);
    this.api.enableBusiness(row.tenantId)
      .pipe(finalize(() => this.actionLoading.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Business account enabled.');
          this.load();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to enable business account.'))
      });
  }

  sendResetLink(tenantId: string): void {
    this.actionLoading.set(true);
    this.api.sendBusinessResetLink(tenantId)
      .pipe(finalize(() => this.actionLoading.set(false)))
      .subscribe({
        next: () => this.toast.success('Password reset link sent.'),
        error: (error) => this.toast.error(extractApiError(error, 'Unable to send reset link.'))
      });
  }

  private load(): void {
    this.loading.set(true);
    const filter = this.filterForm.getRawValue();
    const selectedPageSize = this.resolvePageSize(filter.pageSize);
    this.pageSize.set(selectedPageSize);

    this.api.getBusinesses({
      search: filter.search.trim(),
      status: filter.status || undefined,
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
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load businesses.'))
    });
  }

  private resolvePageSize(pageSize: unknown): number {
    const parsed = Number(pageSize);
    return parsed === 10 || parsed === 20 || parsed === 50 || parsed === 80 || parsed === 100 ? parsed : 10;
  }
}

