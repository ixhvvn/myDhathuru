import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { SignupRequestCounts, SignupRequestDetail, SignupRequestListItem } from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';

@Component({
  selector: 'app-portal-admin-signup-requests-page',
  standalone: true,
  imports: [CommonModule, DatePipe, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppLoaderComponent, AppEmptyStateComponent],
  template: `
    <section class="page-head">
      <h1>Signup Requests</h1>
      <p>Review pending business registrations and approve or reject with full traceability.</p>
    </section>

    <section class="count-grid">
      <app-card class="count pending"><span>Pending</span><strong>{{ counts().pending }}</strong></app-card>
      <app-card class="count accepted"><span>Accepted</span><strong>{{ counts().accepted }}</strong></app-card>
      <app-card class="count rejected"><span>Rejected</span><strong>{{ counts().rejected }}</strong></app-card>
    </section>

    <app-card class="filter-card">
      <form [formGroup]="filterForm" class="filters" (ngSubmit)="applyFilters()">
        <label>
          Search
          <input type="text" formControlName="search" placeholder="Company or requester">
        </label>
        <label>
          Status
          <select formControlName="status">
            <option value="">All statuses</option>
            <option value="Pending">Pending</option>
            <option value="Accepted">Accepted</option>
            <option value="Rejected">Rejected</option>
          </select>
        </label>
        <label>
          From
          <input type="date" formControlName="fromDate">
        </label>
        <label>
          To
          <input type="date" formControlName="toDate">
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
        title="No signup requests found"
        description="Try adjusting the filters or wait for new business signup submissions.">
      </app-empty-state>

      <div class="table-wrap" *ngIf="rows().length > 0">
        <table>
          <thead>
            <tr>
              <th>Request Date</th>
              <th>Company</th>
              <th>Requester</th>
              <th>Company Email</th>
              <th>Status</th>
              <th class="action-col">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let row of rows()">
              <td>{{ row.requestDate | date:'yyyy-MM-dd HH:mm' }}</td>
              <td>
                <strong>{{ row.companyName }}</strong>
                <small>{{ row.businessRegistrationNumber }}</small>
              </td>
              <td>
                <strong>{{ row.requestedByName }}</strong>
                <small>{{ row.requestedByEmail }}</small>
              </td>
              <td>{{ row.companyEmail }}</td>
              <td><span class="status" [attr.data-status]="row.status">{{ row.status }}</span></td>
              <td class="actions-cell">
                <app-button size="sm" variant="secondary" (clicked)="viewRequest(row.id)">View</app-button>
                <app-button size="sm" variant="success" [disabled]="row.status !== 'Pending'" (clicked)="approve(row)">Accept</app-button>
                <app-button size="sm" variant="danger" [disabled]="row.status !== 'Pending'" (clicked)="openRejectModal(row)">Reject</app-button>
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

    <div class="modal-backdrop" *ngIf="detail() || rejecting()"></div>

    <section class="modal" *ngIf="detail() as selected">
      <h2>Signup Request Details</h2>
      <dl>
        <div><dt>Company Name</dt><dd>{{ selected.companyName }}</dd></div>
        <div><dt>Company Email</dt><dd>{{ selected.companyEmail }}</dd></div>
        <div><dt>Company Phone</dt><dd>{{ selected.companyPhone }}</dd></div>
        <div><dt>TIN Number</dt><dd>{{ selected.tinNumber }}</dd></div>
        <div><dt>Business Registration</dt><dd>{{ selected.businessRegistrationNumber }}</dd></div>
        <div><dt>Requested By</dt><dd>{{ selected.requestedByName }} ({{ selected.requestedByEmail }})</dd></div>
        <div><dt>Status</dt><dd>{{ selected.status }}</dd></div>
        <div *ngIf="selected.rejectionReason"><dt>Rejection Reason</dt><dd>{{ selected.rejectionReason }}</dd></div>
        <div *ngIf="selected.reviewedByUserName"><dt>Reviewed By</dt><dd>{{ selected.reviewedByUserName }} · {{ selected.reviewedAt | date:'yyyy-MM-dd HH:mm' }}</dd></div>
      </dl>
      <div class="modal-actions">
        <app-button variant="secondary" (clicked)="detail.set(null)">Close</app-button>
      </div>
    </section>

    <section class="modal" *ngIf="rejecting() as request">
      <h2>Reject Signup Request</h2>
      <p>Enter a clear rejection reason. This will be emailed to {{ request.companyEmail }}.</p>
      <form [formGroup]="rejectForm" (ngSubmit)="reject()">
        <label>
          Rejection Reason
          <textarea formControlName="reason" rows="4" placeholder="Reason for rejection"></textarea>
        </label>
        <small class="error" *ngIf="rejectForm.controls.reason.touched && rejectForm.controls.reason.invalid">
          Rejection reason is required.
        </small>
        <div class="modal-actions">
          <app-button type="button" variant="secondary" (clicked)="cancelReject()">Cancel</app-button>
          <app-button type="submit" variant="danger" [loading]="actionLoading()">Reject</app-button>
        </div>
      </form>
    </section>
  `,
  styles: `
    .page-head h1 { margin: 0; font-family: var(--font-heading); font-size: 1.45rem; color: #2f4268; font-weight: 600; }
    .page-head p { margin: .32rem 0 0; color: #61739a; }
    .count-grid {
      margin-top: .85rem;
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: .68rem;
    }
    .count { --card-padding: .8rem .9rem; display: grid; gap: .2rem; }
    .count span { font-size: .78rem; color: #5c709d; font-family: var(--font-heading); font-weight: 600; text-transform: uppercase; letter-spacing: .04em; }
    .count strong { font-size: 1.45rem; color: #2d4068; font-family: var(--font-heading); font-weight: 600; }
    .count.pending { background: linear-gradient(160deg, rgba(255, 239, 213, .72), rgba(255, 229, 184, .7)); }
    .count.accepted { background: linear-gradient(160deg, rgba(218, 247, 232, .78), rgba(199, 240, 216, .74)); }
    .count.rejected { background: linear-gradient(160deg, rgba(255, 224, 234, .78), rgba(255, 211, 224, .72)); }
    .filter-card {
      margin-top: .75rem;
      --card-padding: .8rem;
    }
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
      color: #5d7099;
      font-size: .78rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    input, select, textarea {
      border-radius: 10px;
      border: 1px solid #ccdaf6;
      background: #fff;
      color: #34486c;
      padding: .56rem .65rem;
      font-size: .88rem;
    }
    input:focus, select:focus, textarea:focus {
      outline: none;
      border-color: #7f8df6;
      box-shadow: 0 0 0 3px rgba(127,141,246,.15);
    }
    .actions {
      display: flex;
      gap: .4rem;
      align-self: end;
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
      min-width: 960px;
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
      font-size: .82rem;
      font-weight: 600;
    }
    td small {
      color: #63779f;
      font-size: .75rem;
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
    .status[data-status='Pending'] { color: #a3711f; border-color: rgba(233, 192, 112, .5); background: rgba(255, 234, 196, .7); }
    .status[data-status='Accepted'] { color: #2f9870; border-color: rgba(124, 215, 180, .5); background: rgba(208, 245, 225, .74); }
    .status[data-status='Rejected'] { color: #b44c6b; border-color: rgba(231, 154, 178, .5); background: rgba(255, 219, 230, .74); }
    .action-col {
      width: 1%;
      min-width: 118px;
      white-space: nowrap;
    }
    .actions-cell {
      display: grid;
      gap: .34rem;
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
      line-height: 1.45;
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
      line-height: 1.4;
    }
    .error {
      color: #c04d71;
      font-size: .76rem;
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
    @media (max-width: 760px) {
      .count-grid {
        grid-template-columns: 1fr;
      }
      .filters > label {
        flex: 1 1 100%;
        min-width: 0;
      }
      .actions { width: 100%; justify-content: flex-start; }
    }
  `
})
export class PortalAdminSignupRequestsPageComponent {
  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly actionLoading = signal(false);
  readonly counts = signal<SignupRequestCounts>({ pending: 0, accepted: 0, rejected: 0 });
  readonly rows = signal<SignupRequestListItem[]>([]);
  readonly detail = signal<SignupRequestDetail | null>(null);
  readonly rejecting = signal<SignupRequestListItem | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);

  readonly filterForm = this.fb.nonNullable.group({
    search: [''],
    status: [''],
    fromDate: [''],
    toDate: [''],
    pageSize: [10]
  });

  readonly rejectForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(500)]]
  });

  constructor() {
    this.load();
    this.refreshCounts();
  }

  applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  resetFilters(): void {
    this.filterForm.reset({
      search: '',
      status: '',
      fromDate: '',
      toDate: '',
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

  viewRequest(id: string): void {
    this.api.getSignupRequestById(id).subscribe({
      next: (result) => this.detail.set(result),
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load request details.'))
    });
  }

  approve(row: SignupRequestListItem): void {
    const confirmed = typeof window !== 'undefined'
      ? window.confirm(`Approve signup request for ${row.companyName}?`)
      : true;

    if (!confirmed) {
      return;
    }

    this.actionLoading.set(true);
    this.api.approveSignupRequest(row.id)
      .pipe(finalize(() => this.actionLoading.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Signup request approved.');
          this.load();
          this.refreshCounts();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to approve request.'))
      });
  }

  openRejectModal(row: SignupRequestListItem): void {
    this.rejectForm.reset({ reason: '' });
    this.rejecting.set(row);
  }

  cancelReject(): void {
    if (this.actionLoading()) {
      return;
    }
    this.rejecting.set(null);
  }

  reject(): void {
    const row = this.rejecting();
    if (!row) {
      return;
    }
    if (this.rejectForm.invalid) {
      this.rejectForm.markAllAsTouched();
      return;
    }

    this.actionLoading.set(true);
    this.api.rejectSignupRequest(row.id, this.rejectForm.getRawValue().reason.trim())
      .pipe(finalize(() => this.actionLoading.set(false)))
      .subscribe({
        next: () => {
          this.rejecting.set(null);
          this.toast.success('Signup request rejected.');
          this.load();
          this.refreshCounts();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to reject request.'))
      });
  }

  private load(): void {
    this.loading.set(true);
    const filter = this.filterForm.getRawValue();
    const selectedPageSize = this.resolvePageSize(filter.pageSize);
    this.pageSize.set(selectedPageSize);

    this.api.getSignupRequests({
      search: filter.search.trim(),
      status: filter.status || undefined,
      fromDate: filter.fromDate || undefined,
      toDate: filter.toDate || undefined,
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
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load signup requests.'))
    });
  }

  private refreshCounts(): void {
    this.api.getSignupRequestCounts().subscribe({
      next: (result) => this.counts.set(result),
      error: () => {
        // Keep previous counts on failure.
      }
    });
  }

  private resolvePageSize(pageSize: unknown): number {
    const parsed = Number(pageSize);
    return parsed === 10 || parsed === 20 || parsed === 50 || parsed === 80 || parsed === 100 ? parsed : 10;
  }
}

