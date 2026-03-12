import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { PortalAdminBillingDashboard, PortalAdminBillingInvoiceListItem } from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';

@Component({
  selector: 'app-portal-admin-billing-dashboard-page',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, AppButtonComponent, AppCardComponent, AppEmptyStateComponent, AppLoaderComponent],
  template: `
    <section class="page-head">
      <h1>Billing Dashboard</h1>
      <p>Super admin billing performance, invoice delivery status, and quick monthly actions.</p>
    </section>

    <app-loader *ngIf="loading()"></app-loader>

    <ng-container *ngIf="!loading() && dashboard() as data">
      <section class="kpi-grid">
        <app-card class="kpi kpi-indigo">
          <h3>Invoices This Month</h3>
          <strong>{{ data.invoicesGeneratedThisMonth }}</strong>
        </app-card>
        <app-card class="kpi kpi-teal">
          <h3>Total Billed (MVR)</h3>
          <strong>MVR {{ data.totalBilledMvrThisMonth | number:'1.2-2' }}</strong>
        </app-card>
        <app-card class="kpi kpi-cyan">
          <h3>Total Billed (USD)</h3>
          <strong>USD {{ data.totalBilledUsdThisMonth | number:'1.2-2' }}</strong>
        </app-card>
        <app-card class="kpi kpi-green">
          <h3>Emailed This Month</h3>
          <strong>{{ data.totalEmailedThisMonth }}</strong>
        </app-card>
        <app-card class="kpi kpi-rose">
          <h3>Pending Emails</h3>
          <strong>{{ data.pendingEmailCount }}</strong>
        </app-card>
      </section>

      <section class="quick-grid">
        <app-card class="quick-actions">
          <h2>Quick Actions</h2>
          <p>Generate invoices, manage custom rates, and update billing controls.</p>
          <div class="actions">
            <a routerLink="/portal-admin/billing/generate">
              <app-button>Generate Monthly Invoices</app-button>
            </a>
            <a routerLink="/portal-admin/billing/invoices">
              <app-button variant="secondary">View All Invoices</app-button>
            </a>
            <a routerLink="/portal-admin/billing/custom-rates">
              <app-button variant="secondary">Manage Custom Rates</app-button>
            </a>
            <a routerLink="/portal-admin/billing/statements">
              <app-button variant="secondary">Account Statements</app-button>
            </a>
            <a routerLink="/portal-admin/billing/settings">
              <app-button variant="secondary">Billing Settings</app-button>
            </a>
            <app-button
              variant="danger"
              [loading]="actionLoading()"
              [disabled]="actionLoading()"
              (clicked)="resetAllInvoices()">
              Reset All Invoices
            </app-button>
          </div>
        </app-card>

        <app-card class="recent-invoices">
          <div class="section-head">
            <h2>Recent Invoices</h2>
            <a routerLink="/portal-admin/billing/invoices">Open full list</a>
          </div>

          <app-empty-state
            *ngIf="data.recentInvoices.length === 0"
            title="No billing invoices yet"
            description="Generate monthly invoices to start billing history.">
          </app-empty-state>

          <div class="table-wrap" *ngIf="data.recentInvoices.length > 0">
            <table>
              <thead>
                <tr>
                  <th>Invoice No</th>
                  <th>Business</th>
                  <th>Billing Month</th>
                  <th>Total</th>
                  <th>Status</th>
                  <th class="action-col">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let invoice of data.recentInvoices">
                  <td>{{ invoice.invoiceNumber }}</td>
                  <td>{{ invoice.companyName }}</td>
                  <td>{{ invoice.billingMonth | date:'yyyy-MM' }}</td>
                  <td>{{ invoice.currency }} {{ invoice.total | number:'1.2-2' }}</td>
                  <td>
                    <span class="status" [attr.data-status]="invoice.status">{{ invoice.status }}</span>
                  </td>
                  <td class="actions-cell">
                    <app-button size="sm" variant="secondary" (clicked)="downloadPdf(invoice)">PDF</app-button>
                    <app-button size="sm" variant="warning" (clicked)="sendEmail(invoice)">Send Email</app-button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </app-card>
      </section>
    </ng-container>
  `,
  styles: `
    .page-head h1 { margin: 0; color: #2f4269; font-family: var(--font-heading); font-size: 1.5rem; font-weight: 600; }
    .page-head p { margin: .3rem 0 0; color: #63759d; }
    .kpi-grid {
      margin-top: .85rem;
      display: grid;
      grid-template-columns: repeat(5, minmax(0, 1fr));
      gap: .68rem;
    }
    .kpi {
      --card-padding: .78rem .86rem;
      display: grid;
      gap: .18rem;
    }
    .kpi h3 {
      margin: 0;
      color: #5f73a0;
      text-transform: uppercase;
      letter-spacing: .04em;
      font-size: .74rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .kpi strong {
      color: #2b3f68;
      font-size: 1.36rem;
      line-height: 1.2;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .kpi-indigo { background: linear-gradient(145deg, rgba(236,241,255,.94), rgba(223,232,255,.9)); }
    .kpi-teal { background: linear-gradient(145deg, rgba(226,252,245,.94), rgba(209,244,233,.9)); }
    .kpi-cyan { background: linear-gradient(145deg, rgba(227,248,255,.94), rgba(214,241,255,.9)); }
    .kpi-green { background: linear-gradient(145deg, rgba(229,249,238,.94), rgba(213,242,225,.9)); }
    .kpi-rose { background: linear-gradient(145deg, rgba(255,237,244,.94), rgba(252,225,236,.9)); }
    .quick-grid {
      margin-top: .85rem;
      display: grid;
      grid-template-columns: .9fr 1.1fr;
      gap: .72rem;
    }
    .quick-actions h2,
    .recent-invoices h2 {
      margin: 0;
      color: #33486f;
      font-family: var(--font-heading);
      font-size: 1.05rem;
      font-weight: 600;
    }
    .quick-actions p {
      margin: .32rem 0 .72rem;
      color: #63769f;
      font-size: .84rem;
      line-height: 1.4;
    }
    .actions {
      display: grid;
      gap: .46rem;
      min-width: 0;
    }
    .actions a {
      text-decoration: none;
    }
    .section-head {
      display: flex;
      justify-content: space-between;
      gap: .7rem;
      align-items: center;
      margin-bottom: .55rem;
    }
    .section-head a {
      color: #6075a2;
      font-family: var(--font-heading);
      font-size: .8rem;
      text-decoration: none;
    }
    .table-wrap {
      border: 1px solid #d9e3fa;
      border-radius: 14px;
      overflow: auto;
    }
    table {
      width: 100%;
      min-width: 860px;
      border-collapse: collapse;
      font-size: .83rem;
    }
    th, td {
      padding: .52rem .6rem;
      border-bottom: 1px solid #e2e9fb;
      color: #42567e;
      text-align: left;
      vertical-align: middle;
    }
    th {
      background: #f3f7ff;
      text-transform: uppercase;
      letter-spacing: .04em;
      color: #5f75a2;
      font-size: .73rem;
      font-family: var(--font-heading);
      font-weight: 600;
      white-space: nowrap;
    }
    .status {
      display: inline-flex;
      padding: .2rem .45rem;
      border-radius: 999px;
      border: 1px solid transparent;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .72rem;
    }
    .status[data-status='Issued'],
    .status[data-status='Draft'] {
      color: #7b5f1f;
      border-color: rgba(221, 184, 106, .45);
      background: rgba(255, 234, 195, .72);
    }
    .status[data-status='Emailed'] {
      color: #2f9670;
      border-color: rgba(126, 214, 181, .45);
      background: rgba(206, 244, 224, .75);
    }
    .status[data-status='EmailFailed'],
    .status[data-status='Cancelled'] {
      color: #b34b6a;
      border-color: rgba(228, 151, 175, .48);
      background: rgba(255, 220, 231, .75);
    }
    .action-col {
      width: 1%;
      min-width: 190px;
      white-space: nowrap;
    }
    .actions-cell {
      display: flex;
      flex-direction: row;
      align-items: center;
      gap: .35rem;
      justify-content: flex-start;
      flex-wrap: nowrap;
      white-space: normal;
      min-width: 0;
    }
    @media (max-width: 1500px) {
      .kpi-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .quick-grid { grid-template-columns: 1fr; }
    }
    @media (max-width: 700px) {
      .kpi-grid { grid-template-columns: 1fr; }
      .actions-cell { flex-wrap: nowrap; }
    }
  `
})
export class PortalAdminBillingDashboardPageComponent {
  private readonly api = inject(PortalAdminApiService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly dashboard = signal<PortalAdminBillingDashboard | null>(null);
  readonly actionLoading = signal(false);

  constructor() {
    this.load();
  }

  downloadPdf(invoice: PortalAdminBillingInvoiceListItem): void {
    this.actionLoading.set(true);
    this.api.downloadBillingInvoicePdf(invoice.id)
      .pipe(finalize(() => this.actionLoading.set(false)))
      .subscribe({
        next: (file) => {
          this.download(file, `admin-invoice-${invoice.invoiceNumber}.pdf`);
          this.toast.success('Billing invoice PDF downloaded.');
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to export invoice PDF.'))
      });
  }

  sendEmail(invoice: PortalAdminBillingInvoiceListItem): void {
    this.actionLoading.set(true);
    this.api.sendBillingInvoiceEmail(invoice.id, {})
      .pipe(finalize(() => this.actionLoading.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Invoice email sent.');
          this.load();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to send invoice email.'))
      });
  }

  resetAllInvoices(): void {
    const confirmed = window.confirm('Delete all billing invoices? This action is permanent and cannot be undone.');
    if (!confirmed) {
      return;
    }

    this.actionLoading.set(true);
    this.api.resetAllBillingInvoices()
      .pipe(finalize(() => this.actionLoading.set(false)))
      .subscribe({
        next: (result) => {
          const message = result.deletedCount > 0
            ? `${result.deletedCount} invoice(s) deleted from billing records.`
            : 'No invoices found to delete.';
          this.toast.success(message);
          this.load();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to reset billing invoices.'))
      });
  }

  private load(): void {
    this.loading.set(true);
    this.api.getBillingDashboard()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => this.dashboard.set(result),
        error: (error) => this.toast.error(extractApiError(error, 'Unable to load billing dashboard.'))
      });
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


