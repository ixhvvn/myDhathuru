import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { PortalAdminAuditLog } from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';

@Component({
  selector: 'app-portal-admin-audit-logs-page',
  standalone: true,
  imports: [CommonModule, DatePipe, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppEmptyStateComponent, AppLoaderComponent],
  template: `
    <section class="page-head">
      <h1>Audit Logs</h1>
      <p>Sensitive portal-admin actions for approvals, account controls, and credential changes.</p>
    </section>

    <app-card class="filter-card">
      <form [formGroup]="filterForm" class="filters" (ngSubmit)="applyFilters()">
        <label>
          Search
          <input type="text" formControlName="search" placeholder="Target, action, user">
        </label>
        <label>
          Action Type
          <select formControlName="actionType">
            <option value="">All actions</option>
            <option value="SignupRequestApproved">Signup Approved</option>
            <option value="SignupRequestRejected">Signup Rejected</option>
            <option value="BusinessDisabled">Business Disabled</option>
            <option value="BusinessEnabled">Business Enabled</option>
            <option value="BusinessLoginUpdated">Business Login Updated</option>
            <option value="BusinessPasswordResetSent">Business Reset Link Sent</option>
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
        title="No audit logs found"
        description="No records match the selected filter.">
      </app-empty-state>

      <div class="table-wrap" *ngIf="rows().length > 0">
        <table>
          <thead>
            <tr>
              <th>Time</th>
              <th>Action</th>
              <th>Target</th>
              <th>Performed By</th>
              <th>Details</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let row of rows()">
              <td>{{ row.performedAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
              <td><span class="action-chip">{{ row.actionType }}</span></td>
              <td>{{ row.targetType }} {{ row.targetName ? ('- ' + row.targetName) : '' }}</td>
              <td>{{ row.performedByName || 'System' }}</td>
              <td>{{ row.details || '-' }}</td>
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
  `,
  styles: `
    .page-head h1 { margin: 0; font-family: var(--font-heading); color: #2f4269; font-size: 1.45rem; font-weight: 600; }
    .page-head p { margin: .32rem 0 0; color: #61739a; }
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
    input,
    select {
      border: 1px solid #ccdaf5;
      border-radius: 10px;
      background: #fff;
      padding: .56rem .65rem;
      font-size: .87rem;
      color: #34486d;
    }
    input:focus,
    select:focus {
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
      min-width: 980px;
      border-collapse: collapse;
      font-size: .84rem;
    }
    th, td {
      padding: .5rem .58rem;
      border-bottom: 1px solid #e0e8fb;
      color: #415680;
      vertical-align: top;
    }
    td:last-child {
      white-space: normal;
      word-break: break-word;
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
    .action-chip {
      display: inline-flex;
      padding: .2rem .5rem;
      border-radius: 999px;
      background: rgba(120, 136, 246, .17);
      color: #4b62ac;
      border: 1px solid rgba(127, 143, 229, .38);
      font-family: var(--font-heading);
      font-size: .72rem;
      font-weight: 600;
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
    }
  `
})
export class PortalAdminAuditLogsPageComponent {
  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly rows = signal<PortalAdminAuditLog[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);

  readonly filterForm = this.fb.nonNullable.group({
    search: [''],
    actionType: [''],
    fromDate: [''],
    toDate: [''],
    pageSize: [10]
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
      actionType: '',
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

  private load(): void {
    this.loading.set(true);
    const filter = this.filterForm.getRawValue();
    const selectedPageSize = this.resolvePageSize(filter.pageSize);
    this.pageSize.set(selectedPageSize);

    this.api.getAuditLogs({
      search: filter.search.trim(),
      actionType: filter.actionType.trim() || undefined,
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
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load audit logs.'))
    });
  }

  private resolvePageSize(pageSize: unknown): number {
    const parsed = Number(pageSize);
    return parsed === 10 || parsed === 20 || parsed === 50 || parsed === 80 || parsed === 100 ? parsed : 10;
  }
}
