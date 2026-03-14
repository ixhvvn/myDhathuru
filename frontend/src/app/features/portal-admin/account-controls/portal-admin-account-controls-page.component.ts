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
  selector: 'app-portal-admin-account-controls-page',
  standalone: true,
  imports: [CommonModule, DatePipe, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppEmptyStateComponent, AppLoaderComponent],
  template: `
    <section class="page-head">
      <h1>Account Controls</h1>
      <p>Control business access, update login details, and send password reset actions without the business analytics view.</p>
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

    <app-card *ngIf="!loading()" class="results-card">
      <app-empty-state
        *ngIf="rows().length === 0"
        title="No account control records found"
        description="No businesses match the selected account filter.">
      </app-empty-state>

      <div class="table-wrap" *ngIf="rows().length > 0">
        <table>
          <thead>
            <tr>
              <th class="company-col">Business</th>
              <th class="email-col">Company Contact</th>
              <th class="status-col">Access</th>
              <th class="activity-col">Last Activity</th>
              <th class="action-col">Controls</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let row of rows()">
              <td class="company-cell">
                <strong>{{ row.companyName }}</strong>
                <small>Registration: {{ row.businessRegistrationNumber || '-' }}</small>
                <small>TIN: {{ row.tinNumber || '-' }}</small>
              </td>
              <td class="contact-cell">
                <span>{{ row.companyEmail || '-' }}</span>
                <small>{{ row.companyPhone || '-' }}</small>
              </td>
              <td class="status-cell">
                <span class="status" [attr.data-status]="row.status">{{ row.status }}</span>
              </td>
              <td class="activity-cell">
                <ng-container *ngIf="row.lastActivityAt; else noLastActivity">
                  <span>{{ row.lastActivityAt | date:'yyyy-MM-dd' }}</span>
                  <small>{{ row.lastActivityAt | date:'HH:mm' }}</small>
                </ng-container>
                <ng-template #noLastActivity>-</ng-template>
              </td>
              <td class="actions-cell">
                <div class="control-cluster">
                  <div class="control-row control-row--support">
                    <app-button class="action-btn action-btn--view" size="sm" variant="secondary" (clicked)="viewDetails(row.tenantId)">View</app-button>
                    <app-button class="action-btn action-btn--edit" size="sm" variant="secondary" (clicked)="editBusiness(row.tenantId)">Edit Login</app-button>
                  </div>
                  <div class="control-row control-row--state">
                    <app-button class="action-btn action-btn--reset" size="sm" variant="warning" (clicked)="sendResetLink(row.tenantId)">Send Reset</app-button>
                    <app-button
                      *ngIf="row.status === 'Active'"
                      class="action-btn action-btn--status action-btn--danger"
                      size="sm"
                      variant="danger"
                      (clicked)="openDisableModal(row)">Disable</app-button>
                    <app-button
                      *ngIf="row.status === 'Disabled'"
                      class="action-btn action-btn--status action-btn--enable"
                      size="sm"
                      variant="success"
                      (clicked)="enableBusiness(row)">Enable</app-button>
                  </div>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="results-footer" *ngIf="rows().length > 0">
        <div class="results-meta">
          Total {{ totalCount() }} business account{{ totalCount() === 1 ? '' : 's' }}
        </div>

        <div class="pagination">
          <span>Page {{ page() }} of {{ totalPages() }}</span>
          <div class="pagination-actions">
            <app-button size="sm" variant="secondary" [disabled]="page() <= 1" (clicked)="changePage(page() - 1)">Previous</app-button>
            <app-button size="sm" variant="secondary" [disabled]="page() >= totalPages()" (clicked)="changePage(page() + 1)">Next</app-button>
          </div>
        </div>
      </div>
    </app-card>

    <div class="modal-backdrop" *ngIf="detail() || editDetail() || disableTarget()"></div>

    <section class="modal" *ngIf="detail() as selected">
      <h2>Business Account Details</h2>
      <dl>
        <div><dt>Company</dt><dd>{{ selected.companyName }}</dd></div>
        <div><dt>Email</dt><dd>{{ selected.companyEmail }}</dd></div>
        <div><dt>Phone</dt><dd>{{ selected.companyPhone }}</dd></div>
        <div><dt>Status</dt><dd>{{ selected.status }}</dd></div>
        <div><dt>Primary Admin</dt><dd>{{ selected.primaryAdmin?.fullName || '-' }} ({{ selected.primaryAdmin?.email || '-' }})</dd></div>
        <div><dt>Registration</dt><dd>{{ selected.businessRegistrationNumber || '-' }}</dd></div>
        <div><dt>TIN</dt><dd>{{ selected.tinNumber || '-' }}</dd></div>
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
    .results-card { margin-top: .78rem; }
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
    .results-footer {
      margin-top: .72rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: .8rem;
      flex-wrap: wrap;
    }
    .results-meta {
      color: #5f739d;
      font-size: .83rem;
      font-weight: 600;
    }
    table {
      width: 100%;
      table-layout: fixed;
      border-collapse: collapse;
      font-size: .82rem;
    }
    th, td {
      padding: .56rem .62rem;
      border-bottom: 1px solid #e0e8fb;
      color: #415680;
      vertical-align: middle;
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
      text-align: left;
    }
    .company-col { width: 28%; }
    .email-col { width: 22%; }
    .status-col { width: 10%; text-align: center; }
    .activity-col { width: 12%; text-align: center; }
    .action-col {
      width: 24%;
      min-width: 280px;
      text-align: right;
    }
    .company-cell strong,
    .contact-cell span {
      display: block;
      color: #2f4369;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .82rem;
      line-height: 1.25;
    }
    .company-cell small,
    .contact-cell small {
      display: block;
      color: #63779f;
      font-size: .75rem;
      line-height: 1.35;
      overflow-wrap: anywhere;
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
    .status-cell,
    .activity-cell {
      text-align: center;
      white-space: nowrap;
    }
    .activity-cell span,
    .activity-cell small {
      display: block;
      line-height: 1.3;
    }
    .actions-cell {
      text-align: right;
      white-space: normal;
    }
    .control-cluster {
      display: inline-grid;
      gap: .34rem;
      justify-items: end;
      min-width: 0;
    }
    .control-row {
      display: flex;
      flex-wrap: wrap;
      gap: .34rem;
      justify-content: flex-end;
    }
    .control-row--state {
      align-items: center;
    }
    .actions-cell app-button {
      flex: 0 0 auto;
    }
    :host ::ng-deep .actions-cell app-button .app-btn {
      min-height: 31px;
      padding: .34rem .58rem;
      font-size: .72rem;
      white-space: nowrap;
      border-radius: 12px;
      box-shadow: none;
    }
    :host ::ng-deep .actions-cell .action-btn--view .app-btn,
    :host ::ng-deep .actions-cell .action-btn--edit .app-btn {
      background: linear-gradient(145deg, rgba(255,255,255,.96), rgba(240,245,255,.92));
      border-color: #d2dcf3;
      color: #52658e;
    }
    :host ::ng-deep .actions-cell .action-btn--edit .app-btn {
      background: linear-gradient(145deg, rgba(246,249,255,.97), rgba(234,241,255,.92));
      color: #465f95;
    }
    :host ::ng-deep .actions-cell .action-btn--reset .app-btn {
      background: linear-gradient(135deg, #f5b563, #ea9b36);
      box-shadow: 0 8px 16px rgba(214, 144, 55, .2);
    }
    :host ::ng-deep .actions-cell .action-btn--status .app-btn {
      min-width: 78px;
      justify-content: center;
      font-weight: 700;
    }
    :host ::ng-deep .actions-cell .action-btn--danger .app-btn {
      background: linear-gradient(135deg, #e88fa5, #dc7892);
      box-shadow: 0 8px 16px rgba(212, 94, 122, .18);
    }
    :host ::ng-deep .actions-cell .action-btn--enable .app-btn {
      background: linear-gradient(135deg, #56ba91, #419d77);
      box-shadow: 0 8px 16px rgba(68, 160, 123, .18);
    }
    :host ::ng-deep .actions-cell app-button .app-btn:not(:disabled):hover {
      transform: translateY(-1px);
    }
    .pagination {
      display: flex;
      align-items: center;
      gap: .65rem;
      color: #5f739d;
      font-size: .84rem;
      font-weight: 600;
    }
    .pagination-actions {
      display: inline-flex;
      gap: .42rem;
      align-items: center;
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
      table {
        table-layout: auto;
        min-width: 980px;
      }
      .action-col {
        min-width: 250px;
      }
      .results-footer,
      .pagination {
        align-items: flex-start;
      }
      .pagination {
        width: 100%;
        justify-content: space-between;
      }
    }
  `
})
export class PortalAdminAccountControlsPageComponent {
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
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load business account details.'))
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
        error: (error) => this.toast.error(extractApiError(error, 'Unable to load account controls.'))
      });
  }

  private resolvePageSize(pageSize: unknown): number {
    const parsed = Number(pageSize);
    return parsed === 10 || parsed === 20 || parsed === 50 || parsed === 80 || parsed === 100 ? parsed : 10;
  }
}
