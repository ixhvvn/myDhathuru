import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Observable, finalize } from 'rxjs';
import {
  Customer,
  ReportDatePreset,
  ReportExportRequest,
  ReportMeta,
  ReportType,
  SalesByVesselReport,
  SalesSummaryReport,
  SalesTransactionsReport
} from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppStatusChipComponent } from '../../../shared/components/app-status-chip/app-status-chip.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { PortalApiService } from '../../services/portal-api.service';

interface ReportTypeOption
{
  value: ReportType;
  label: string;
}

interface ReportPresetOption
{
  value: ReportDatePreset;
  label: string;
}

@Component({
  selector: 'app-reports-page',
  standalone: true,
  imports: [
    CommonModule,
    AppButtonComponent,
    AppCardComponent,
    AppCurrencyPipe,
    AppDataTableComponent,
    AppEmptyStateComponent,
    AppPageHeaderComponent,
    AppStatusChipComponent
  ],
  template: `
    <section class="reports-page">
      <app-page-header
        title="Reports"
        subtitle="Generate tenant-scoped sales analytics and export landscape PDF or Excel reports from real database data.">
      </app-page-header>

      <app-card class="filters-card">
        <div class="filters-grid">
          <label>
            Report Type
            <select [value]="selectedReportType()" (change)="onReportTypeChange($event)">
              <option *ngFor="let type of reportTypes" [value]="type.value">{{ type.label }}</option>
            </select>
          </label>

          <label>
            Date Range
            <select [value]="selectedDatePreset()" (change)="onDatePresetChange($event)">
              <option *ngFor="let preset of datePresets" [value]="preset.value">{{ preset.label }}</option>
            </select>
          </label>

          <label>
            Search Customer
            <input
              type="text"
              [value]="customerSearch()"
              (input)="onCustomerSearchInput($event)"
              placeholder="Search customer by name, email, phone">
          </label>

          <label>
            Customer
            <select [value]="selectedCustomerId()" (change)="onCustomerChange($event)">
              <option value="">All Customers</option>
              <option *ngFor="let customer of filteredCustomers()" [value]="customer.id">{{ customer.name }}</option>
            </select>
          </label>

          <label *ngIf="selectedDatePreset() === reportDatePreset.CustomRange">
            Start Date
            <input type="date" [value]="customStartDate()" (input)="onCustomStartInput($event)">
          </label>

          <label *ngIf="selectedDatePreset() === reportDatePreset.CustomRange">
            End Date
            <input type="date" [value]="customEndDate()" (input)="onCustomEndInput($event)">
          </label>
        </div>

        <p class="validation-error" *ngIf="validationError()">
          {{ validationError() }}
        </p>

        <div class="actions-row">
          <app-button
            variant="secondary"
            [loading]="exportLoading() === 'excel'"
            [disabled]="previewLoading() || exportLoading() !== null"
            (clicked)="exportReport('excel')">Export Excel</app-button>
          <app-button
            variant="secondary"
            [loading]="exportLoading() === 'pdf'"
            [disabled]="previewLoading() || exportLoading() !== null"
            (clicked)="exportReport('pdf')">Export PDF</app-button>
          <app-button
            [loading]="previewLoading()"
            [disabled]="exportLoading() !== null"
            (clicked)="generateReport()">Generate Report</app-button>
        </div>
      </app-card>

      <app-card class="results-card">
        <div class="results-head">
          <div>
            <h3>{{ activeReportTitle() }} Preview</h3>
            <p *ngIf="activeMeta() as meta">
              Generated: {{ meta.generatedAtUtc | date: 'medium': '+0500' }} MVT |
              Range: {{ meta.rangeStartUtc | date: 'mediumDate': '+0500' }} - {{ meta.rangeEndUtc | date: 'mediumDate': '+0500' }} |
              Customer: {{ meta.customerFilterLabel }}
            </p>
            <p *ngIf="!activeMeta()">Select filters and click Generate Report.</p>
          </div>
          <span class="count-chip" *ngIf="hasGenerated()">{{ totalRowsLabel() }}</span>
        </div>

        <ng-container *ngIf="hasGenerated(); else notGeneratedTpl">
          <ng-container *ngIf="selectedReportType() === reportType.SalesSummary">
            <div class="metric-grid" *ngIf="salesSummary() as report">
              <article>
                <span>Total Invoices</span>
                <strong>{{ report.totalInvoices }}</strong>
              </article>
              <article>
                <span>Total Sales</span>
                <strong>MVR {{ report.totalSales.mvr | number: '1.2-2' }}</strong>
                <strong>USD {{ report.totalSales.usd | number: '1.2-2' }}</strong>
              </article>
              <article>
                <span>Total Received</span>
                <strong>MVR {{ report.totalReceived.mvr | number: '1.2-2' }}</strong>
                <strong>USD {{ report.totalReceived.usd | number: '1.2-2' }}</strong>
              </article>
              <article>
                <span>Total Pending</span>
                <strong>MVR {{ report.totalPending.mvr | number: '1.2-2' }}</strong>
                <strong>USD {{ report.totalPending.usd | number: '1.2-2' }}</strong>
              </article>
              <article>
                <span>Total Customers</span>
                <strong>{{ report.totalCustomers }}</strong>
              </article>
              <article *ngIf="taxApplicable()">
                <span>Total Tax</span>
                <strong>MVR {{ report.totalTax.mvr | number: '1.2-2' }}</strong>
                <strong>USD {{ report.totalTax.usd | number: '1.2-2' }}</strong>
              </article>
            </div>

            <app-data-table
              [hasData]="(salesSummary()?.rows?.length ?? 0) > 0"
              emptyTitle="No Sales Summary Data"
              emptyDescription="No invoices were found for the selected date range and customer filter.">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>No. of Invoices</th>
                  <th>Sales (MVR)</th>
                  <th>Sales (USD)</th>
                  <th>Received (MVR)</th>
                  <th>Received (USD)</th>
                  <th>Pending (MVR)</th>
                  <th>Pending (USD)</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let row of salesSummary()?.rows">
                  <td>{{ row.date }}</td>
                  <td>{{ row.invoiceCount }}</td>
                  <td>{{ row.salesMvr | number: '1.2-2' }}</td>
                  <td>{{ row.salesUsd | number: '1.2-2' }}</td>
                  <td>{{ row.receivedMvr | number: '1.2-2' }}</td>
                  <td>{{ row.receivedUsd | number: '1.2-2' }}</td>
                  <td>{{ row.pendingMvr | number: '1.2-2' }}</td>
                  <td>{{ row.pendingUsd | number: '1.2-2' }}</td>
                </tr>
                <tr class="totals-row" *ngIf="salesSummary() as report">
                  <td>TOTAL</td>
                  <td>{{ report.totalInvoices }}</td>
                  <td>{{ report.totalSales.mvr | number: '1.2-2' }}</td>
                  <td>{{ report.totalSales.usd | number: '1.2-2' }}</td>
                  <td>{{ report.totalReceived.mvr | number: '1.2-2' }}</td>
                  <td>{{ report.totalReceived.usd | number: '1.2-2' }}</td>
                  <td>{{ report.totalPending.mvr | number: '1.2-2' }}</td>
                  <td>{{ report.totalPending.usd | number: '1.2-2' }}</td>
                </tr>
              </tbody>
            </app-data-table>
          </ng-container>

          <ng-container *ngIf="selectedReportType() === reportType.SalesTransactions">
            <div class="metric-grid compact" *ngIf="salesTransactions() as report">
              <article>
                <span>Transactions</span>
                <strong>{{ report.totalTransactions }}</strong>
              </article>
              <article>
                <span>Total Sales</span>
                <strong>MVR {{ report.totalSales.mvr | number: '1.2-2' }}</strong>
                <strong>USD {{ report.totalSales.usd | number: '1.2-2' }}</strong>
              </article>
              <article>
                <span>Total Received</span>
                <strong>MVR {{ report.totalReceived.mvr | number: '1.2-2' }}</strong>
                <strong>USD {{ report.totalReceived.usd | number: '1.2-2' }}</strong>
              </article>
              <article>
                <span>Total Pending</span>
                <strong>MVR {{ report.totalPending.mvr | number: '1.2-2' }}</strong>
                <strong>USD {{ report.totalPending.usd | number: '1.2-2' }}</strong>
              </article>
            </div>

            <app-data-table
              [hasData]="(salesTransactions()?.rows?.length ?? 0) > 0"
              emptyTitle="No Sales Transactions Data"
              emptyDescription="No invoice transactions were found for the selected filters.">
              <thead>
                <tr>
                  <th>Invoice No</th>
                  <th>Date Issued</th>
                  <th>Customer</th>
                  <th>Vessel</th>
                  <th>Description</th>
                  <th>Currency</th>
                  <th>Amount</th>
                  <th>Status</th>
                  <th>Method</th>
                  <th>Received On</th>
                  <th>Balance</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let row of salesTransactions()?.rows">
                  <td>{{ row.invoiceNo }}</td>
                  <td>{{ row.dateIssued }}</td>
                  <td>{{ row.customer }}</td>
                  <td>{{ row.vessel }}</td>
                  <td class="wrap-cell">{{ row.description }}</td>
                  <td>{{ row.currency }}</td>
                  <td>{{ row.amount | appCurrency: row.currency }}</td>
                  <td>
                    <app-status-chip [label]="row.paymentStatus" [variant]="statusVariant(row.paymentStatus)"></app-status-chip>
                  </td>
                  <td>{{ row.paymentMethod || '-' }}</td>
                  <td>{{ row.receivedOn || '-' }}</td>
                  <td>{{ row.balance | appCurrency: row.currency }}</td>
                </tr>
                <tr class="totals-row" *ngIf="salesTransactions() as report">
                  <td>TOTAL</td>
                  <td colspan="5"></td>
                  <td>MVR {{ report.totalSales.mvr | number: '1.2-2' }} / USD {{ report.totalSales.usd | number: '1.2-2' }}</td>
                  <td colspan="2"></td>
                  <td></td>
                  <td>MVR {{ report.totalPending.mvr | number: '1.2-2' }} / USD {{ report.totalPending.usd | number: '1.2-2' }}</td>
                </tr>
              </tbody>
            </app-data-table>
          </ng-container>

          <ng-container *ngIf="selectedReportType() === reportType.SalesByVessel">
            <div class="metric-grid compact" *ngIf="salesByVessel() as report">
              <article>
                <span>Transactions</span>
                <strong>{{ report.totalTransactions }}</strong>
              </article>
              <article>
                <span>Total Sales</span>
                <strong>MVR {{ report.totalSales.mvr | number: '1.2-2' }}</strong>
                <strong>USD {{ report.totalSales.usd | number: '1.2-2' }}</strong>
              </article>
              <article>
                <span>Total Received</span>
                <strong>MVR {{ report.totalReceived.mvr | number: '1.2-2' }}</strong>
                <strong>USD {{ report.totalReceived.usd | number: '1.2-2' }}</strong>
              </article>
              <article>
                <span>Total Pending</span>
                <strong>MVR {{ report.totalPending.mvr | number: '1.2-2' }}</strong>
                <strong>USD {{ report.totalPending.usd | number: '1.2-2' }}</strong>
              </article>
            </div>

            <app-data-table
              [hasData]="(salesByVessel()?.rows?.length ?? 0) > 0"
              emptyTitle="No Vessel Sales Data"
              emptyDescription="No vessel-grouped sales were found for the selected filters.">
              <thead>
                <tr>
                  <th>Vessel</th>
                  <th>Currency</th>
                  <th>No. of Transactions</th>
                  <th>Total Sales</th>
                  <th>Total Received</th>
                  <th>Pending Amount</th>
                  <th>% of Currency Sales</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let row of salesByVessel()?.rows">
                  <td>{{ row.vessel }}</td>
                  <td>{{ row.currency }}</td>
                  <td>{{ row.transactionCount }}</td>
                  <td>{{ row.totalSales | appCurrency: row.currency }}</td>
                  <td>{{ row.totalReceived | appCurrency: row.currency }}</td>
                  <td>{{ row.pendingAmount | appCurrency: row.currency }}</td>
                  <td>{{ row.percentageOfCurrencySales | number: '1.2-2' }}%</td>
                </tr>
                <tr class="totals-row" *ngIf="salesByVessel() as report">
                  <td>TOTAL</td>
                  <td>-</td>
                  <td>{{ report.totalTransactions }}</td>
                  <td>MVR {{ report.totalSales.mvr | number: '1.2-2' }} / USD {{ report.totalSales.usd | number: '1.2-2' }}</td>
                  <td>MVR {{ report.totalReceived.mvr | number: '1.2-2' }} / USD {{ report.totalReceived.usd | number: '1.2-2' }}</td>
                  <td>MVR {{ report.totalPending.mvr | number: '1.2-2' }} / USD {{ report.totalPending.usd | number: '1.2-2' }}</td>
                  <td>-</td>
                </tr>
              </tbody>
            </app-data-table>
          </ng-container>
        </ng-container>

        <ng-template #notGeneratedTpl>
          <app-empty-state
            title="Report Not Generated"
            description="Configure filters and click Generate Report to preview live tenant data.">
          </app-empty-state>
        </ng-template>
      </app-card>
    </section>
  `,
  styles: `
    .reports-page {
      display: grid;
      gap: 1rem;
    }
    .filters-card,
    .results-card {
      --card-bg: linear-gradient(158deg, rgba(255,255,255,.94), rgba(246,250,255,.9));
      --card-border: rgba(255,255,255,.86);
      --card-padding: 1rem;
    }
    .filters-grid {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: .72rem;
    }
    label {
      display: grid;
      gap: .25rem;
      font-size: .8rem;
      color: var(--text-muted);
      font-weight: 600;
    }
    select,
    input {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .56rem .62rem;
      min-height: 42px;
      background: rgba(255,255,255,.92);
    }
    .validation-error {
      margin: .62rem 0 0;
      color: #b33a54;
      background: rgba(227, 127, 151, .13);
      border: 1px solid rgba(218, 118, 145, .34);
      border-radius: 11px;
      padding: .5rem .62rem;
      font-size: .81rem;
      font-weight: 600;
    }
    .actions-row {
      margin-top: .78rem;
      display: flex;
      justify-content: flex-end;
      gap: .5rem;
      flex-wrap: wrap;
    }
    .results-head {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: .85rem;
      margin-bottom: .75rem;
    }
    .results-head h3 {
      margin: 0;
      color: #30436d;
      font-size: 1.08rem;
    }
    .results-head p {
      margin: .24rem 0 0;
      font-size: .81rem;
      color: #66799f;
      line-height: 1.35;
    }
    .count-chip {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border-radius: 999px;
      border: 1px solid rgba(143, 164, 240, .5);
      background: linear-gradient(135deg, rgba(234, 240, 255, .95), rgba(228, 245, 255, .9));
      color: #4f6392;
      padding: .25rem .58rem;
      font-size: .75rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .metric-grid {
      display: grid;
      grid-template-columns: repeat(6, minmax(0, 1fr));
      gap: .55rem;
      margin-bottom: .8rem;
    }
    .metric-grid.compact {
      grid-template-columns: repeat(4, minmax(0, 1fr));
    }
    .metric-grid article {
      border: 1px solid rgba(211, 222, 247, .76);
      border-radius: 14px;
      padding: .62rem .66rem;
      background: linear-gradient(145deg, rgba(255,255,255,.92), rgba(242,247,255,.88));
      box-shadow: inset 0 1px 0 rgba(255,255,255,.82);
      display: grid;
      gap: .22rem;
    }
    .metric-grid span {
      color: #5f729d;
      font-size: .74rem;
      text-transform: uppercase;
      letter-spacing: .04em;
    }
    .metric-grid strong {
      color: #2f426b;
      font-family: var(--font-heading);
      font-size: .96rem;
      line-height: 1.15;
    }
    .totals-row td {
      font-weight: 700;
      background: rgba(236, 242, 255, .74);
      color: #33486f;
    }
    .wrap-cell {
      max-width: 260px;
      white-space: normal;
      line-height: 1.35;
    }
    @media (max-width: 1300px) {
      .filters-grid {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .metric-grid {
        grid-template-columns: repeat(3, minmax(0, 1fr));
      }
      .metric-grid.compact {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
    }
    @media (max-width: 760px) {
      .filters-grid {
        grid-template-columns: 1fr;
      }
      .actions-row {
        justify-content: stretch;
      }
      .actions-row app-button {
        display: block;
        width: 100%;
      }
      .results-head {
        flex-direction: column;
      }
      .metric-grid,
      .metric-grid.compact {
        grid-template-columns: 1fr;
      }
      .wrap-cell {
        max-width: 190px;
      }
    }
  `
})
export class ReportsPageComponent implements OnInit {
  readonly reportType = ReportType;
  readonly reportDatePreset = ReportDatePreset;

  readonly reportTypes: ReadonlyArray<ReportTypeOption> = [
    { value: ReportType.SalesSummary, label: 'Sales Summary Report' },
    { value: ReportType.SalesTransactions, label: 'Sales Transactions Report' },
    { value: ReportType.SalesByVessel, label: 'Sales By Vessel Report' }
  ];

  readonly datePresets: ReadonlyArray<ReportPresetOption> = [
    { value: ReportDatePreset.Today, label: 'Today' },
    { value: ReportDatePreset.Yesterday, label: 'Yesterday' },
    { value: ReportDatePreset.Last7Days, label: 'Last 7 Days' },
    { value: ReportDatePreset.LastWeek, label: 'Last Week' },
    { value: ReportDatePreset.Last30Days, label: 'Last 30 Days' },
    { value: ReportDatePreset.LastMonth, label: 'Last Month' },
    { value: ReportDatePreset.ThisYear, label: 'This Year' },
    { value: ReportDatePreset.CustomRange, label: 'Custom Range' }
  ];

  readonly selectedReportType = signal<ReportType>(ReportType.SalesSummary);
  readonly selectedDatePreset = signal<ReportDatePreset>(ReportDatePreset.Today);
  readonly customStartDate = signal('');
  readonly customEndDate = signal('');
  readonly customerSearch = signal('');
  readonly selectedCustomerId = signal('');
  readonly validationError = signal<string | null>(null);
  readonly previewLoading = signal(false);
  readonly exportLoading = signal<'excel' | 'pdf' | null>(null);
  readonly hasGenerated = signal(false);
  readonly taxApplicable = signal(true);

  readonly customers = signal<Customer[]>([]);
  readonly salesSummary = signal<SalesSummaryReport | null>(null);
  readonly salesTransactions = signal<SalesTransactionsReport | null>(null);
  readonly salesByVessel = signal<SalesByVesselReport | null>(null);

  readonly filteredCustomers = computed(() =>
  {
    const search = this.customerSearch().trim().toLowerCase();
    if (!search)
    {
      return this.customers();
    }

    return this.customers().filter((customer) =>
      customer.name.toLowerCase().includes(search)
      || (customer.email ?? '').toLowerCase().includes(search)
      || (customer.phone ?? '').toLowerCase().includes(search));
  });

  readonly activeMeta = computed<ReportMeta | null>(() =>
  {
    if (this.selectedReportType() === ReportType.SalesSummary)
    {
      return this.salesSummary()?.meta ?? null;
    }

    if (this.selectedReportType() === ReportType.SalesTransactions)
    {
      return this.salesTransactions()?.meta ?? null;
    }

    return this.salesByVessel()?.meta ?? null;
  });

  readonly activeReportTitle = computed(() =>
    this.reportTypes.find((x) => x.value === this.selectedReportType())?.label ?? 'Report');

  readonly totalRowsLabel = computed(() =>
  {
    if (this.selectedReportType() === ReportType.SalesSummary)
    {
      return `${this.salesSummary()?.rows.length ?? 0} day rows`;
    }

    if (this.selectedReportType() === ReportType.SalesTransactions)
    {
      return `${this.salesTransactions()?.rows.length ?? 0} transactions`;
    }

    return `${this.salesByVessel()?.rows.length ?? 0} vessels`;
  });

  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);

  ngOnInit(): void {
    const today = this.todayIso();
    this.customStartDate.set(today);
    this.customEndDate.set(today);
    this.loadSettings();
    this.loadCustomers();
  }

  onReportTypeChange(event: Event): void {
    this.selectedReportType.set(Number((event.target as HTMLSelectElement).value));
    this.invalidatePreview();
  }

  onDatePresetChange(event: Event): void {
    const preset = Number((event.target as HTMLSelectElement).value) as ReportDatePreset;
    this.selectedDatePreset.set(preset);
    this.validationError.set(null);

    if (preset === ReportDatePreset.CustomRange && (!this.customStartDate() || !this.customEndDate()))
    {
      const today = this.todayIso();
      this.customStartDate.set(today);
      this.customEndDate.set(today);
    }

    this.invalidatePreview();
  }

  onCustomerSearchInput(event: Event): void {
    this.customerSearch.set((event.target as HTMLInputElement).value);
  }

  onCustomerChange(event: Event): void {
    this.selectedCustomerId.set((event.target as HTMLSelectElement).value);
    this.invalidatePreview();
  }

  onCustomStartInput(event: Event): void {
    this.customStartDate.set((event.target as HTMLInputElement).value);
    this.invalidatePreview();
  }

  onCustomEndInput(event: Event): void {
    this.customEndDate.set((event.target as HTMLInputElement).value);
    this.invalidatePreview();
  }

  generateReport(): void {
    if (this.previewLoading() || this.exportLoading() !== null)
    {
      return;
    }

    if (!this.validateFilters())
    {
      return;
    }

    const reportType = this.selectedReportType();
    this.previewLoading.set(true);
    const params = this.buildFilterParams();
    let request$: Observable<SalesSummaryReport | SalesTransactionsReport | SalesByVesselReport> | null = null;

    switch (reportType)
    {
      case ReportType.SalesSummary:
        request$ = this.api.getSalesSummaryReport(params);
        break;
      case ReportType.SalesTransactions:
        request$ = this.api.getSalesTransactionsReport(params);
        break;
      case ReportType.SalesByVessel:
        request$ = this.api.getSalesByVesselReport(params);
        break;
      default:
        this.previewLoading.set(false);
        this.toast.error('Unsupported report type.');
        break;
    }

    if (!request$)
    {
      return;
    }

    request$
      .pipe(finalize(() => this.previewLoading.set(false)))
      .subscribe({
        next: (report) =>
        {
          if (!report)
          {
            this.hasGenerated.set(false);
            this.toast.error('No report data was returned for the selected filters.');
            return;
          }

          switch (reportType)
          {
            case ReportType.SalesSummary:
              this.salesSummary.set(report as SalesSummaryReport);
              this.salesTransactions.set(null);
              this.salesByVessel.set(null);
              break;
            case ReportType.SalesTransactions:
              this.salesTransactions.set(report as SalesTransactionsReport);
              this.salesSummary.set(null);
              this.salesByVessel.set(null);
              break;
            case ReportType.SalesByVessel:
              this.salesByVessel.set(report as SalesByVesselReport);
              this.salesSummary.set(null);
              this.salesTransactions.set(null);
              break;
          }

          this.hasGenerated.set(true);
        },
        error: (error) =>
        {
          this.toast.error(this.readError(error, this.reportErrorMessage(reportType)));
        }
      });
  }

  exportReport(format: 'excel' | 'pdf'): void {
    if (this.previewLoading() || this.exportLoading() !== null)
    {
      return;
    }

    if (!this.validateFilters())
    {
      return;
    }

    this.exportLoading.set(format);
    const payload = this.buildExportPayload();
    const request$ = format === 'excel'
      ? this.api.exportReportExcel(payload)
      : this.api.exportReportPdf(payload);
    let downloaded = false;

    request$
      .pipe(finalize(() => this.exportLoading.set(null)))
      .subscribe({
        next: (file) =>
        {
          try
          {
            this.download(file, `${this.reportSlug()}-${this.todayIso()}.${format === 'excel' ? 'xlsx' : 'pdf'}`);
            downloaded = true;
            this.toast.success(`Report exported to ${format.toUpperCase()}.`);
          }
          catch
          {
            this.toast.error(`Failed to download ${format.toUpperCase()} file.`);
          }
        },
        error: (error) =>
        {
          this.toast.error(this.readError(error, `Failed to export report ${format.toUpperCase()}.`));
        },
        complete: () =>
        {
          if (!downloaded)
          {
            this.toast.error(`No ${format.toUpperCase()} file was returned by the server.`);
          }
        }
      });
  }

  statusVariant(status: string): 'green' | 'amber' | 'red' | 'gray' {
    if (status === 'Paid')
    {
      return 'green';
    }

    if (status === 'Partial')
    {
      return 'amber';
    }

    if (status === 'Unpaid')
    {
      return 'red';
    }

    return 'gray';
  }

  private loadCustomers(): void {
    this.api.getCustomers({ pageNumber: 1, pageSize: 500, sortDirection: 'asc' }).subscribe({
      next: (result) => this.customers.set(result.items),
      error: (error) => this.toast.error(this.readError(error, 'Failed to load customers for report filters.'))
    });
  }

  private loadSettings(): void {
    this.api.getSettings().subscribe({
      next: (settings) => this.taxApplicable.set(settings.isTaxApplicable),
      error: () => this.toast.error('Failed to load report settings.')
    });
  }

  private invalidatePreview(): void {
    this.hasGenerated.set(false);
  }

  private validateFilters(): boolean {
    this.validationError.set(null);

    if (this.selectedDatePreset() !== ReportDatePreset.CustomRange)
    {
      return true;
    }

    const start = this.customStartDate();
    const end = this.customEndDate();

    if (!start || !end)
    {
      this.validationError.set('Custom range requires both start and end dates.');
      return false;
    }

    const startMs = Date.parse(`${start}T00:00:00Z`);
    const endMs = Date.parse(`${end}T00:00:00Z`);
    if (Number.isNaN(startMs) || Number.isNaN(endMs))
    {
      this.validationError.set('Custom range dates are invalid.');
      return false;
    }

    if (endMs < startMs)
    {
      this.validationError.set('End date cannot be earlier than start date.');
      return false;
    }

    const totalDays = Math.floor((endMs - startMs) / 86400000) + 1;
    if (totalDays > 31)
    {
      this.validationError.set('Custom range cannot exceed 31 days.');
      return false;
    }

    return true;
  }

  private buildFilterParams(): Record<string, unknown> {
    const params: Record<string, unknown> = {
      datePreset: this.selectedDatePreset()
    };

    if (this.selectedDatePreset() === ReportDatePreset.CustomRange)
    {
      params['customStartDate'] = this.customStartDate();
      params['customEndDate'] = this.customEndDate();
    }

    if (this.selectedCustomerId())
    {
      params['customerId'] = this.selectedCustomerId();
    }

    return params;
  }

  private buildExportPayload(): ReportExportRequest {
    return {
      reportType: this.selectedReportType(),
      datePreset: this.selectedDatePreset(),
      customStartDate: this.selectedDatePreset() === ReportDatePreset.CustomRange ? this.customStartDate() : undefined,
      customEndDate: this.selectedDatePreset() === ReportDatePreset.CustomRange ? this.customEndDate() : undefined,
      customerId: this.selectedCustomerId() || undefined
    };
  }

  private reportSlug(): string {
    switch (this.selectedReportType())
    {
      case ReportType.SalesSummary:
        return 'sales-summary';
      case ReportType.SalesTransactions:
        return 'sales-transactions';
      case ReportType.SalesByVessel:
        return 'sales-by-vessel';
      default:
        return 'report';
    }
  }

  private reportErrorMessage(reportType: ReportType): string {
    switch (reportType)
    {
      case ReportType.SalesSummary:
        return 'Failed to generate sales summary report.';
      case ReportType.SalesTransactions:
        return 'Failed to generate sales transactions report.';
      case ReportType.SalesByVessel:
        return 'Failed to generate sales by vessel report.';
      default:
        return 'Failed to generate report.';
    }
  }

  private todayIso(): string {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private download(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  private readError(error: unknown, fallback: string): string {
    const apiError = error as {
      error?: {
        message?: string;
        errors?: string[] | Record<string, string[]>;
        title?: string;
        detail?: string;
      };
    };

    const errors = apiError?.error?.errors;
    const firstValidationError = Array.isArray(errors)
      ? errors[0]
      : errors && typeof errors === 'object'
        ? Object.values(errors).flat()[0]
        : undefined;

    return firstValidationError
      ?? apiError?.error?.message
      ?? apiError?.error?.title
      ?? apiError?.error?.detail
      ?? fallback;
  }
}
