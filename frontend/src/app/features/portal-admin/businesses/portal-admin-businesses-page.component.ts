import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { PortalAdminBusinessDetail, PortalAdminBusinessListItem } from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
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
      <p>Review tenant business profiles, operational size, and recent platform activity.</p>
    </section>

    <section class="summary-grid">
      <app-card class="summary-card summary-indigo">
        <span>Businesses Shown</span>
        <strong>{{ rows().length }}</strong>
        <small>Visible on this page</small>
      </app-card>
      <app-card class="summary-card summary-green">
        <span>Active Businesses</span>
        <strong>{{ activeCount() }}</strong>
        <small>Businesses with access enabled</small>
      </app-card>
      <app-card class="summary-card summary-cyan">
        <span>Total Staff</span>
        <strong>{{ visibleStaffCount() }}</strong>
        <small>Across visible businesses</small>
      </app-card>
      <app-card class="summary-card summary-peach">
        <span>Total Vessels</span>
        <strong>{{ visibleVesselCount() }}</strong>
        <small>Across visible businesses</small>
      </app-card>
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
        title="No businesses found"
        description="No records match the selected filter.">
      </app-empty-state>

      <div class="table-wrap" *ngIf="rows().length > 0">
        <table>
          <thead>
            <tr>
              <th class="company-col">Company</th>
              <th class="status-col">Status</th>
              <th class="count-col">Staff</th>
              <th class="count-col">Vessels</th>
              <th class="created-col">Created</th>
              <th class="activity-col">Last Activity</th>
              <th class="action-col">Details</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let row of rows()">
              <td class="company-cell">
                <strong>{{ row.companyName }}</strong>
                <small>{{ row.companyEmail }}</small>
                <small>{{ row.companyPhone }}</small>
              </td>
              <td class="status-cell">
                <span class="status" [attr.data-status]="row.status">{{ row.status }}</span>
              </td>
              <td class="count-cell">{{ row.staffCount }}</td>
              <td class="count-cell">{{ row.vesselCount }}</td>
              <td class="created-cell">{{ row.createdAt | date:'yyyy-MM-dd' }}</td>
              <td class="activity-cell">
                <ng-container *ngIf="row.lastActivityAt; else noLastActivity">
                  <span>{{ row.lastActivityAt | date:'yyyy-MM-dd' }}</span>
                  <small>{{ row.lastActivityAt | date:'HH:mm' }}</small>
                </ng-container>
                <ng-template #noLastActivity>-</ng-template>
              </td>
              <td class="actions-cell">
                <app-button size="sm" variant="secondary" (clicked)="viewDetails(row.tenantId)">View</app-button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="results-footer" *ngIf="rows().length > 0">
        <div class="results-meta">
          Total {{ totalCount() }} business{{ totalCount() === 1 ? '' : 'es' }}
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

    <div class="modal-backdrop" *ngIf="detail()"></div>

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
  `,
  styles: `
    .page-head h1 { margin: 0; font-family: var(--font-heading); color: #2f4269; font-size: 1.46rem; font-weight: 600; }
    .page-head p { margin: .3rem 0 0; color: #61739a; }
    .summary-grid {
      margin-top: .78rem;
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: .68rem;
    }
    .summary-card {
      --card-padding: .78rem .86rem;
      --card-shadow: none;
      --card-hover-shadow: none;
      --card-hover-transform: none;
      --card-shimmer-display: none;
      display: grid;
      gap: .18rem;
    }
    .summary-card span {
      color: #5f73a0;
      text-transform: uppercase;
      letter-spacing: .04em;
      font-size: .74rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .summary-card strong {
      color: #2b3f68;
      font-size: 1.34rem;
      line-height: 1.2;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .summary-card small {
      color: #697ca5;
      font-size: .75rem;
      line-height: 1.3;
    }
    .summary-indigo { --card-bg: linear-gradient(145deg, rgba(236,241,255,.94), rgba(223,232,255,.9)); }
    .summary-green { --card-bg: linear-gradient(145deg, rgba(229,249,238,.94), rgba(213,242,225,.9)); }
    .summary-cyan { --card-bg: linear-gradient(145deg, rgba(227,248,255,.94), rgba(214,241,255,.9)); }
    .summary-peach { --card-bg: linear-gradient(145deg, rgba(255,242,228,.94), rgba(252,232,213,.9)); }
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
    .company-col { width: 32%; }
    .status-col { width: 9%; text-align: center; }
    .count-col { width: 7%; text-align: center; }
    .created-col { width: 11%; text-align: center; }
    .activity-col { width: 12%; text-align: center; }
    .action-col {
      width: 9%;
      min-width: 110px;
      text-align: center;
    }
    .company-cell {
      min-width: 0;
    }
    .company-cell strong {
      display: block;
      color: #2f4369;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .82rem;
      line-height: 1.25;
    }
    .company-cell small {
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
    .count-cell,
    .created-cell {
      text-align: center;
      white-space: nowrap;
    }
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
       text-align: center;
       vertical-align: middle;
       white-space: nowrap;
     }
    .actions-cell app-button { display: inline-flex; }
    :host ::ng-deep .actions-cell app-button .app-btn {
      min-height: 30px;
      padding: .34rem .56rem;
      font-size: .72rem;
      white-space: nowrap;
      border-radius: 11px;
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
    .modal-actions {
      margin-top: .62rem;
      display: flex;
      justify-content: flex-end;
      gap: .46rem;
    }
    @media (max-width: 1240px) {
      .summary-grid {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
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
        min-width: 120px;
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
export class PortalAdminBusinessesPageComponent {
  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly rows = signal<PortalAdminBusinessListItem[]>([]);
  readonly detail = signal<PortalAdminBusinessDetail | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);
  readonly activeCount = computed(() => this.rows().filter((row) => row.status === 'Active').length);
  readonly visibleStaffCount = computed(() => this.rows().reduce((total, row) => total + row.staffCount, 0));
  readonly visibleVesselCount = computed(() => this.rows().reduce((total, row) => total + row.vesselCount, 0));

  readonly filterForm = this.fb.nonNullable.group({
    search: [''],
    status: [''],
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

