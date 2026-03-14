import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { PortalAdminBillingInvoiceDetail, PortalAdminBillingInvoiceListItem } from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';

@Component({
  selector: 'app-portal-admin-billing-invoices-page',
  standalone: true,
  imports: [CommonModule, DatePipe, ReactiveFormsModule, AppButtonComponent, AppCardComponent, AppEmptyStateComponent, AppLoaderComponent],
  template: `
    <section class="page-head">
      <h1>Billing Invoices</h1>
      <p>Review generated platform invoices, export PDF copies, and trigger invoice email delivery.</p>
    </section>

    <app-card class="filter-card">
      <form [formGroup]="filterForm" class="filters" (ngSubmit)="applyFilters()">
        <label>
          Billing Month
          <input type="month" formControlName="billingMonth">
        </label>
        <label>
          Currency
          <select formControlName="currency">
            <option value="">All Currencies</option>
            <option value="MVR">MVR</option>
            <option value="USD">USD</option>
          </select>
        </label>
        <label>
          Status
          <select formControlName="status">
            <option value="">All Statuses</option>
            <option value="Draft">Draft</option>
            <option value="Issued">Issued</option>
            <option value="Emailed">Emailed</option>
            <option value="EmailFailed">EmailFailed</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </label>
        <label>
          Search
          <input type="text" formControlName="search" placeholder="Invoice number or business">
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
        title="No billing invoices found"
        description="No invoices match the selected filters.">
      </app-empty-state>

      <div class="table-wrap" *ngIf="rows().length > 0">
        <table>
          <thead>
            <tr>
              <th>Invoice No</th>
              <th>Billing Month</th>
              <th>Business</th>
              <th>Total</th>
              <th>Status</th>
              <th>Created</th>
              <th>Emailed</th>
              <th class="action-col">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let row of rows()">
              <td>{{ row.invoiceNumber }}</td>
              <td>{{ row.billingMonth | date:'yyyy-MM' }}</td>
              <td>{{ row.companyName }}</td>
              <td>{{ row.currency }} {{ row.total | number:'1.2-2' }}</td>
              <td><span class="status" [attr.data-status]="row.status">{{ row.status }}</span></td>
              <td>{{ row.createdAt | date:'yyyy-MM-dd HH:mm' }}</td>
              <td>{{ row.sentAt ? (row.sentAt | date:'yyyy-MM-dd HH:mm') : '-' }}</td>
              <td class="actions-cell">
                <app-button size="sm" variant="secondary" (clicked)="viewInvoice(row.id)">View</app-button>
                <app-button size="sm" variant="secondary" (clicked)="downloadPdf(row)">PDF</app-button>
                <app-button size="sm" variant="warning" (clicked)="sendEmail(row)">Send Email</app-button>
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

    <div class="modal-backdrop" *ngIf="selectedInvoice()"></div>
    <section class="modal" *ngIf="selectedInvoice() as invoice">
      <h2>{{ invoice.invoiceNumber }}</h2>
      <p>{{ invoice.companyName }} - {{ invoice.currency }} {{ invoice.total | number:'1.2-2' }}</p>

      <div class="meta-grid">
        <article><strong>Billing Month</strong><span>{{ invoice.billingMonth | date:'yyyy-MM' }}</span></article>
        <article><strong>Invoice Date</strong><span>{{ invoice.invoiceDate | date:'yyyy-MM-dd' }}</span></article>
        <article><strong>Due Date</strong><span>{{ invoice.dueDate | date:'yyyy-MM-dd' }}</span></article>
        <article><strong>Status</strong><span>{{ invoice.status }}</span></article>
        <article><strong>Business Email</strong><span>{{ invoice.companyEmail }}</span></article>
        <article><strong>Admin Email</strong><span>{{ invoice.companyAdminEmail || '-' }}</span></article>
      </div>

      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Description</th>
              <th>Qty</th>
              <th>Rate</th>
              <th>Amount</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let line of invoice.lineItems">
              <td>{{ line.description }}</td>
              <td>{{ line.quantity }}</td>
              <td>{{ invoice.currency }} {{ line.rate | number:'1.2-2' }}</td>
              <td>{{ invoice.currency }} {{ line.amount | number:'1.2-2' }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <h3>Email Log</h3>
      <app-empty-state
        *ngIf="invoice.emailLogs.length === 0"
        title="No email attempts yet"
        description="Use Send Email to deliver this invoice.">
      </app-empty-state>
      <ul class="email-log" *ngIf="invoice.emailLogs.length > 0">
        <li *ngFor="let log of invoice.emailLogs">
          <div>
            <strong>{{ log.status }}</strong>
          <p>{{ log.toEmail }} <span *ngIf="log.ccEmail">(CC {{ log.ccEmail }})</span></p>
            <small *ngIf="log.errorMessage">{{ log.errorMessage }}</small>
          </div>
          <span>{{ log.attemptedAt | date:'yyyy-MM-dd HH:mm' }}</span>
        </li>
      </ul>

      <div class="modal-actions">
        <app-button variant="secondary" (clicked)="selectedInvoice.set(null)">Close</app-button>
      </div>
    </section>
  `,
  styles: `
    .page-head h1 { margin: 0; font-family: var(--font-heading); font-size: 1.48rem; color: #2f4269; font-weight: 600; }
    .page-head p { margin: .3rem 0 0; color: #62749c; }
    .filter-card { margin-top: .8rem; --card-padding: .84rem; }
    .results-card { margin-top: .8rem; }
    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: .58rem;
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
    label { display: grid; gap: .2rem; color: #5f729c; font-size: .78rem; font-family: var(--font-heading); font-weight: 600; }
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
    .actions {
      display: flex;
      gap: .42rem;
      flex-wrap: wrap;
      min-width: 140px;
      justify-content: flex-end;
      margin-left: auto;
    }
    .table-wrap { border: 1px solid #d9e3fa; border-radius: 14px; overflow: auto; }
    .results-meta { margin-top: .56rem; color: #5f739d; font-size: .83rem; }
    table { width: 100%; min-width: 980px; border-collapse: collapse; font-size: .83rem; }
    th, td { padding: .52rem .58rem; text-align: left; border-bottom: 1px solid #e2e9fc; color: #42567f; vertical-align: top; }
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
    .action-col {
      width: 1%;
      min-width: 250px;
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
      font-size: .83rem;
      color: #61749d;
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
      width: min(960px, calc(100vw - 1.1rem));
      max-height: calc(100dvh - 1.1rem);
      overflow: auto;
      border-radius: 18px;
      border: 1px solid #d5e1fa;
      background: #f9fbff;
      box-shadow: 0 30px 70px rgba(56, 73, 118, .34);
      padding: .95rem;
    }
    .modal h2 { margin: 0; color: #2f4269; font-family: var(--font-heading); font-size: 1.12rem; font-weight: 600; }
    .modal > p { margin: .26rem 0 .62rem; color: #5f739d; font-size: .86rem; }
    .meta-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .4rem; margin-bottom: .62rem; }
    .meta-grid article {
      border: 1px solid #dce6fb;
      border-radius: 10px;
      background: #fff;
      padding: .45rem .52rem;
      display: grid;
      gap: .15rem;
    }
    .meta-grid strong { color: #60739e; font-size: .73rem; font-family: var(--font-heading); text-transform: uppercase; letter-spacing: .03em; }
    .meta-grid span { color: #33496d; font-size: .84rem; }
    .modal h3 { margin: .64rem 0 .42rem; color: #34486f; font-family: var(--font-heading); font-size: .96rem; font-weight: 600; }
    .email-log { margin: 0; padding: 0; list-style: none; display: grid; gap: .42rem; }
    .email-log li {
      border: 1px solid #dce5fa;
      border-radius: 11px;
      background: linear-gradient(145deg, #f8fbff, #eef4ff);
      padding: .45rem .54rem;
      display: flex;
      justify-content: space-between;
      gap: .6rem;
    }
    .email-log strong { display: block; color: #32486f; font-family: var(--font-heading); font-size: .82rem; }
    .email-log p { margin: .16rem 0 0; color: #60739c; font-size: .78rem; }
    .email-log small { display: block; margin-top: .18rem; color: #b34d6d; font-size: .74rem; }
    .email-log span { color: #6579a3; font-size: .74rem; white-space: nowrap; align-self: start; }
    .modal-actions { margin-top: .64rem; display: flex; justify-content: flex-end; gap: .46rem; }
    @media (max-width: 1240px) {
      .filters > label {
        flex: 1 1 calc(50% - .58rem);
      }
      .actions {
        width: 100%;
        margin-left: 0;
        justify-content: flex-start;
      }
      .meta-grid { grid-template-columns: 1fr 1fr; }
    }
    @media (max-width: 760px) {
      .filters > label {
        flex: 1 1 100%;
        min-width: 0;
      }
      .actions { width: 100%; }
      .meta-grid { grid-template-columns: 1fr; }
    }
  `
})
export class PortalAdminBillingInvoicesPageComponent {
  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly rows = signal<PortalAdminBillingInvoiceListItem[]>([]);
  readonly selectedInvoice = signal<PortalAdminBillingInvoiceDetail | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);

  readonly filterForm = this.fb.nonNullable.group({
    billingMonth: [''],
    currency: [''],
    status: [''],
    search: [''],
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
      billingMonth: '',
      currency: '',
      status: '',
      search: '',
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

  viewInvoice(invoiceId: string): void {
    this.api.getBillingInvoiceById(invoiceId).subscribe({
      next: (result) => this.selectedInvoice.set(result),
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load invoice details.'))
    });
  }

  downloadPdf(invoice: PortalAdminBillingInvoiceListItem): void {
    this.api.downloadBillingInvoicePdf(invoice.id).subscribe({
      next: (file) => {
        this.download(file, `admin-invoice-${invoice.invoiceNumber}.pdf`);
        this.toast.success('Invoice PDF exported.');
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to export invoice PDF.'))
    });
  }

  sendEmail(invoice: PortalAdminBillingInvoiceListItem): void {
    this.api.sendBillingInvoiceEmail(invoice.id, {}).subscribe({
      next: () => {
        this.toast.success('Invoice email sent.');
        this.load();
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to send invoice email.'))
    });
  }

  private load(): void {
    this.loading.set(true);
    const filter = this.filterForm.getRawValue();
    const selectedPageSize = this.resolvePageSize(filter.pageSize);
    this.pageSize.set(selectedPageSize);
    const billingMonth = filter.billingMonth
      ? `${filter.billingMonth}-01`
      : undefined;

    this.api.getBillingInvoices({
      billingMonth,
      currency: filter.currency || undefined,
      status: filter.status || undefined,
      search: filter.search.trim(),
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
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load billing invoices.'))
    });
  }

  private resolvePageSize(pageSize: unknown): number {
    const parsed = Number(pageSize);
    return parsed === 10 || parsed === 20 || parsed === 50 || parsed === 80 || parsed === 100 ? parsed : 10;
  }

  private download(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}



