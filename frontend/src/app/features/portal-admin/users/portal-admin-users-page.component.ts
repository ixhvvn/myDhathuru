import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { PortalAdminBusinessUser } from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';

@Component({
  selector: 'app-portal-admin-users-page',
  standalone: true,
  imports: [CommonModule, DatePipe, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppEmptyStateComponent, AppLoaderComponent],
  template: `
    <section class="page-head">
      <h1>Business Users</h1>
      <p>View business admins and staff across all tenants.</p>
    </section>

    <app-card class="filter-card">
      <form [formGroup]="filterForm" class="filters" (ngSubmit)="applyFilters()">
        <label>
          Search
          <input type="text" formControlName="search" placeholder="Name, email, company">
        </label>
        <label>
          Tenant ID (optional)
          <input type="text" formControlName="tenantId" placeholder="Tenant UUID">
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
        title="No business users found"
        description="No records match your current filters.">
      </app-empty-state>

      <div class="table-wrap" *ngIf="rows().length > 0">
        <table>
          <thead>
            <tr>
              <th>User</th>
              <th>Company</th>
              <th>Role</th>
              <th>Status</th>
              <th>Last Login</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let row of rows()">
              <td>
                <strong>{{ row.fullName }}</strong>
                <small>{{ row.email }}</small>
              </td>
              <td>{{ row.companyName }}</td>
              <td>{{ row.role }}</td>
              <td>
                <span class="status" [attr.data-active]="row.isActive">{{ row.isActive ? 'Active' : 'Inactive' }}</span>
              </td>
              <td>{{ row.lastLoginAt ? (row.lastLoginAt | date:'yyyy-MM-dd HH:mm') : '-' }}</td>
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
      min-width: 760px;
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
      text-align: left;
    }
    td strong {
      display: block;
      color: #2f4369;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .82rem;
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
    .status[data-active='true'] {
      color: #2f9870;
      border-color: rgba(124, 215, 180, .5);
      background: rgba(208, 245, 225, .74);
    }
    .status[data-active='false'] {
      color: #b44c6b;
      border-color: rgba(231, 154, 178, .5);
      background: rgba(255, 219, 230, .74);
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
    @media (max-width: 980px) {
      .filters > label {
        flex: 1 1 100%;
        min-width: 0;
      }
      .actions { width: 100%; justify-content: flex-start; }
    }
  `
})
export class PortalAdminUsersPageComponent {
  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly rows = signal<PortalAdminBusinessUser[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);

  readonly filterForm = this.fb.nonNullable.group({
    search: [''],
    tenantId: [''],
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
    this.filterForm.reset({ search: '', tenantId: '', pageSize: 10 });
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

    this.api.getUsers({
      search: filter.search.trim(),
      tenantId: filter.tenantId.trim() || undefined,
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
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load business users.'))
    });
  }

  private resolvePageSize(pageSize: unknown): number {
    const parsed = Number(pageSize);
    return parsed === 10 || parsed === 20 || parsed === 50 || parsed === 80 || parsed === 100 ? parsed : 10;
  }
}

