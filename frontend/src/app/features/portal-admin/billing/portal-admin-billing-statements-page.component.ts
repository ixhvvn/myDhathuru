import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { PortalAdminBillingBusinessOption, PortalAdminBillingYearlyStatement } from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';

@Component({
  selector: 'app-portal-admin-billing-statements-page',
  standalone: true,
  imports: [CommonModule, DatePipe, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppEmptyStateComponent, AppLoaderComponent],
  template: `
    <section class="page-head">
      <h1>Account Statements</h1>
      <p>View full-year billing invoices per business with monthly and yearly totals.</p>
    </section>

    <app-card class="filter-card">
      <form class="filters" [formGroup]="filterForm" (ngSubmit)="loadStatement()">
        <label>
          Business
          <select formControlName="tenantId">
            <option value="">Select business</option>
            <option *ngFor="let business of businesses()" [value]="business.tenantId">{{ business.companyName }}</option>
          </select>
        </label>
        <label>
          Year
          <input type="number" formControlName="year" min="2000" max="2100" step="1">
        </label>
        <div class="actions">
          <app-button type="submit" [loading]="loadingStatement()">Load Statement</app-button>
          <app-button type="button" variant="secondary" [disabled]="loadingStatement()" (clicked)="resetFilters()">Reset</app-button>
        </div>
      </form>
    </app-card>

    <app-loader *ngIf="loadingBusinesses() || loadingStatement()"></app-loader>

    <app-empty-state
      *ngIf="!loadingBusinesses() && businesses().length === 0"
      title="No businesses available"
      description="Approve a signup request first, then statements will be available.">
    </app-empty-state>

    <ng-container *ngIf="!loadingBusinesses() && !loadingStatement() && statement() as data">
      <app-card class="summary-card">
        <div class="summary-grid">
          <article>
            <label>Business</label>
            <strong>{{ data.companyName }}</strong>
          </article>
          <article>
            <label>Year</label>
            <strong>{{ data.year }}</strong>
          </article>
          <article>
            <label>Total Invoices</label>
            <strong>{{ data.totalInvoices }}</strong>
          </article>
          <article>
            <label>Total Invoiced</label>
            <strong>MVR {{ data.totalInvoiced.mvr | number:'1.2-2' }}</strong>
            <small>USD {{ data.totalInvoiced.usd | number:'1.2-2' }}</small>
          </article>
          <article>
            <label>Emailed</label>
            <strong>{{ data.emailedInvoices }}</strong>
          </article>
          <article>
            <label>Pending</label>
            <strong>{{ data.pendingInvoices }}</strong>
          </article>
        </div>
      </app-card>

      <app-card class="table-card">
        <div class="section-head">
          <h2>Monthly Breakdown</h2>
        </div>
        <app-empty-state
          *ngIf="data.months.length === 0"
          title="No yearly invoice data"
          description="No invoices were generated for the selected business and year.">
        </app-empty-state>

        <div class="table-wrap" *ngIf="data.months.length > 0">
          <table>
            <thead>
              <tr>
                <th>Month</th>
                <th>Invoices</th>
                <th>Total (MVR)</th>
                <th>Total (USD)</th>
                <th>Emailed</th>
                <th>Pending</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let row of data.months">
                <td>{{ row.label }}</td>
                <td>{{ row.invoiceCount }}</td>
                <td>MVR {{ row.totalMvr | number:'1.2-2' }}</td>
                <td>USD {{ row.totalUsd | number:'1.2-2' }}</td>
                <td>{{ row.emailedCount }}</td>
                <td>{{ row.pendingCount }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </app-card>

      <app-card class="table-card">
        <div class="section-head">
          <h2>Invoices</h2>
        </div>
        <app-empty-state
          *ngIf="data.invoices.length === 0"
          title="No invoices found"
          description="No invoice rows exist for the selected business and year.">
        </app-empty-state>

        <div class="table-wrap" *ngIf="data.invoices.length > 0">
          <table>
            <thead>
              <tr>
                <th>Invoice No</th>
                <th>Billing Month</th>
                <th>Invoice Date</th>
                <th>Due Date</th>
                <th>Total</th>
                <th>Status</th>
                <th>Sent At</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let row of data.invoices">
                <td>{{ row.invoiceNumber }}</td>
                <td>{{ row.billingMonth | date:'yyyy-MM' }}</td>
                <td>{{ row.invoiceDate | date:'yyyy-MM-dd' }}</td>
                <td>{{ row.dueDate | date:'yyyy-MM-dd' }}</td>
                <td>{{ row.currency }} {{ row.total | number:'1.2-2' }}</td>
                <td><span class="status" [attr.data-status]="row.status">{{ row.status }}</span></td>
                <td>{{ row.sentAt ? (row.sentAt | date:'yyyy-MM-dd HH:mm') : '-' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </app-card>
    </ng-container>
  `,
  styles: `
    .page-head h1 { margin: 0; color: #2f4269; font-family: var(--font-heading); font-size: 1.5rem; font-weight: 600; }
    .page-head p { margin: .3rem 0 0; color: #63759d; }
    .filter-card { margin-top: .8rem; --card-padding: .86rem; }
    .filters {
      display: grid;
      grid-template-columns: 2fr 1fr auto;
      gap: .58rem;
      align-items: end;
    }
    label {
      display: grid;
      gap: .2rem;
      color: #5f729c;
      font-size: .78rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    input, select {
      border: 1px solid #cddaf5;
      border-radius: 11px;
      background: #fff;
      color: #34496e;
      padding: .55rem .63rem;
      font-size: .86rem;
      min-height: 41px;
    }
    input:focus, select:focus {
      outline: none;
      border-color: #7f8df5;
      box-shadow: 0 0 0 3px rgba(126, 140, 245, .16);
    }
    .actions { display: flex; gap: .42rem; flex-wrap: wrap; }
    .summary-card { margin-top: .72rem; }
    .summary-grid {
      display: grid;
      grid-template-columns: repeat(6, minmax(0, 1fr));
      gap: .52rem;
    }
    .summary-grid article {
      border: 1px solid #dbe5fa;
      border-radius: 12px;
      background: linear-gradient(145deg, rgba(247, 251, 255, .94), rgba(237, 245, 255, .86));
      padding: .58rem .64rem;
      display: grid;
      gap: .18rem;
    }
    .summary-grid label {
      font-size: .72rem;
      text-transform: uppercase;
      letter-spacing: .04em;
      color: #6074a2;
    }
    .summary-grid strong {
      color: #2e4269;
      font-family: var(--font-heading);
      font-size: .98rem;
      font-weight: 600;
      line-height: 1.2;
    }
    .summary-grid small {
      color: #5f739e;
      font-size: .78rem;
    }
    .table-card { margin-top: .72rem; }
    .section-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: .62rem;
      margin-bottom: .52rem;
    }
    .section-head h2 {
      margin: 0;
      color: #34486f;
      font-family: var(--font-heading);
      font-size: 1rem;
      font-weight: 600;
    }
    .table-wrap { border: 1px solid #d9e3fa; border-radius: 14px; overflow: auto; }
    table { width: 100%; min-width: 860px; border-collapse: collapse; font-size: .83rem; }
    th, td { padding: .52rem .58rem; text-align: left; border-bottom: 1px solid #e2e9fc; color: #42567f; vertical-align: middle; }
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
      font-family: var(--font-heading);
      font-size: .72rem;
      font-weight: 600;
    }
    .status[data-status='Issued'],
    .status[data-status='Draft'] { color: #a37320; border-color: rgba(225, 187, 110, .5); background: rgba(255, 235, 196, .72); }
    .status[data-status='Emailed'] { color: #2f9870; border-color: rgba(123, 215, 179, .5); background: rgba(209, 245, 224, .75); }
    .status[data-status='EmailFailed'],
    .status[data-status='Cancelled'] { color: #b54d6c; border-color: rgba(231, 155, 179, .5); background: rgba(255, 220, 232, .75); }
    @media (max-width: 1550px) {
      .summary-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); }
    }
    @media (max-width: 900px) {
      .filters { grid-template-columns: 1fr; }
      .summary-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    @media (max-width: 680px) {
      .summary-grid { grid-template-columns: 1fr; }
    }
  `
})
export class PortalAdminBillingStatementsPageComponent {
  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loadingBusinesses = signal(true);
  readonly loadingStatement = signal(false);
  readonly businesses = signal<PortalAdminBillingBusinessOption[]>([]);
  readonly statement = signal<PortalAdminBillingYearlyStatement | null>(null);

  readonly filterForm = this.fb.nonNullable.group({
    tenantId: [''],
    year: [new Date().getFullYear()]
  });

  constructor() {
    this.loadBusinesses();
  }

  loadStatement(): void {
    const value = this.filterForm.getRawValue();
    const year = Number(value.year);
    if (!value.tenantId) {
      this.toast.error('Select a business to load account statement.');
      return;
    }

    if (!Number.isFinite(year) || year < 2000 || year > 2100) {
      this.toast.error('Year must be between 2000 and 2100.');
      return;
    }

    this.loadingStatement.set(true);
    this.api.getBillingYearlyStatement(value.tenantId, year)
      .pipe(finalize(() => this.loadingStatement.set(false)))
      .subscribe({
        next: (result) => this.statement.set(result),
        error: (error) => this.toast.error(extractApiError(error, 'Unable to load account statement.'))
      });
  }

  resetFilters(): void {
    const firstBusiness = this.businesses()[0];
    this.filterForm.setValue({
      tenantId: firstBusiness?.tenantId ?? '',
      year: new Date().getFullYear()
    });

    if (firstBusiness) {
      this.loadStatement();
      return;
    }

    this.statement.set(null);
  }

  private loadBusinesses(): void {
    this.loadingBusinesses.set(true);
    this.api.getBillingBusinessOptions()
      .pipe(finalize(() => this.loadingBusinesses.set(false)))
      .subscribe({
        next: (result) => {
          this.businesses.set(result);
          if (result.length === 0) {
            this.statement.set(null);
            return;
          }

          const selectedTenantId = this.filterForm.controls.tenantId.value;
          const hasSelectedTenant = result.some(x => x.tenantId === selectedTenantId);
          if (!hasSelectedTenant) {
            this.filterForm.patchValue({ tenantId: result[0].tenantId }, { emitEvent: false });
          }

          this.loadStatement();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to load businesses.'))
      });
  }
}

