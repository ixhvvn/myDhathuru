import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import {
  BptExpenseNoteSection,
  MiraPeriodMode,
  MiraReportExportRequest,
  MiraReportPreview,
  MiraReportType
} from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { PortalApiService } from '../../services/portal-api.service';

interface MiraOption<TValue>
{
  value: TValue;
  label: string;
}

@Component({
  selector: 'app-mira-page',
  standalone: true,
  imports: [
    CommonModule,
    AppButtonComponent,
    AppCardComponent,
    AppDataTableComponent,
    AppEmptyStateComponent,
    AppPageHeaderComponent
  ],
  template: `
    <section class="mira-page">
      <app-page-header
        title="MIRA"
        subtitle="Generate MIRA input tax, output tax, BPT income statement, and BPT notes directly from live invoices, purchases, payroll, and expense data.">
      </app-page-header>

      <app-card class="filters-card">
        <div class="filters-grid">
          <label>
            Statement
            <select [value]="selectedReportType()" (change)="onReportTypeChange($event)">
              <option *ngFor="let option of reportTypes" [value]="option.value">{{ option.label }}</option>
            </select>
          </label>

          <label>
            Period
            <select [value]="selectedPeriodMode()" (change)="onPeriodModeChange($event)">
              <option *ngFor="let option of periodModes" [value]="option.value">{{ option.label }}</option>
            </select>
          </label>

          <label>
            Year
            <select [value]="selectedYear()" (change)="onYearChange($event)">
              <option *ngFor="let year of years" [value]="year">{{ year }}</option>
            </select>
          </label>

          <label *ngIf="selectedPeriodMode() === miraPeriodMode.Quarter">
            Quarter
            <select [value]="selectedQuarter()" (change)="onQuarterChange($event)">
              <option *ngFor="let option of quarterOptions" [value]="option.value">{{ option.label }}</option>
            </select>
          </label>

          <label *ngIf="selectedPeriodMode() === miraPeriodMode.CustomRange">
            Start Date
            <input type="date" [value]="customStartDate()" (input)="onCustomStartInput($event)">
          </label>

          <label *ngIf="selectedPeriodMode() === miraPeriodMode.CustomRange">
            End Date
            <input type="date" [value]="customEndDate()" (input)="onCustomEndInput($event)">
          </label>
        </div>

        <p class="helper-text">
          Statutory exports are calculated from approved purchase documents, invoices issued, payroll periods, and manual expense entries already stored in the database.
        </p>

        <p class="validation-error" *ngIf="validationError()">
          {{ validationError() }}
        </p>

        <div class="actions-row">
          <app-button
            variant="secondary"
            [loading]="exportLoading() === 'excel'"
            [disabled]="previewLoading() || exportLoading() !== null"
            (clicked)="exportStatement('excel')">Export Excel</app-button>
          <app-button
            variant="secondary"
            [loading]="exportLoading() === 'pdf'"
            [disabled]="previewLoading() || exportLoading() !== null"
            (clicked)="exportStatement('pdf')">Export PDF</app-button>
          <app-button
            [loading]="previewLoading()"
            [disabled]="exportLoading() !== null"
            (clicked)="generatePreview()">Generate Preview</app-button>
        </div>
      </app-card>

      <app-card class="preview-card">
        <div class="preview-head">
          <div>
            <h3>{{ activeTitle() }}</h3>
            <p *ngIf="preview() as current">
              {{ current.meta.periodLabel }} |
              {{ current.meta.rangeStart | date: 'mediumDate' }} - {{ current.meta.rangeEnd | date: 'mediumDate' }} |
              Generated {{ current.meta.generatedAtUtc | date: 'medium' : '+0500' }} MVT
            </p>
            <p *ngIf="!preview()">Select a statement and generate a preview.</p>
          </div>
          <div class="meta-chip" *ngIf="preview() as current">
            <span>Activity No.</span>
            <strong>{{ current.meta.taxableActivityNumber || '-' }}</strong>
          </div>
        </div>

        <ng-container *ngIf="preview() as current; else emptyStateTpl">
          <section class="filing-notes" *ngIf="current.assumptions.length > 0">
            <div class="notes-title">Important Filing Notes</div>
            <article *ngFor="let item of current.assumptions">{{ item }}</article>
          </section>

          <ng-container [ngSwitch]="selectedReportType()">
            <ng-container *ngSwitchCase="miraReportType.InputTaxStatement">
              <div class="metric-grid" *ngIf="current.inputTaxStatement as statement">
                <article>
                  <span>Invoices</span>
                  <strong>{{ statement.totalInvoices }}</strong>
                </article>
                <article>
                  <span>Taxable Base</span>
                  <strong>MVR {{ statement.totalInvoiceBase | number: '1.2-2' }}</strong>
                </article>
                <article>
                  <span>Claimable GST</span>
                  <strong>MVR {{ statement.totalClaimableGst | number: '1.2-2' }}</strong>
                </article>
                <article>
                  <span>GST 8%</span>
                  <strong>{{ statement.totalGst8 | number: '1.2-2' }}</strong>
                </article>
              </div>

              <app-data-table
                [hasData]="(current.inputTaxStatement?.rows?.length ?? 0) > 0"
                emptyTitle="No Input Tax Data"
                emptyDescription="No approved claimable purchase invoices were found for the selected period.">
                <thead>
                  <tr>
                    <th>Supplier TIN</th>
                    <th>Supplier</th>
                    <th>Invoice No.</th>
                    <th>Date</th>
                    <th>Taxable Value</th>
                    <th>GST 6%</th>
                    <th>GST 8%</th>
                    <th>GST 12%</th>
                    <th>GST 16%</th>
                    <th>GST 17%</th>
                    <th>Total GST</th>
                    <th>Activity No.</th>
                    <th>Revenue / Capital</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let row of current.inputTaxStatement?.rows">
                    <td>{{ row.supplierTin }}</td>
                    <td>{{ row.supplierName }}</td>
                    <td>{{ row.supplierInvoiceNumber }}</td>
                    <td>{{ row.invoiceDate }}</td>
                    <td>{{ row.invoiceTotalExcludingGst | number: '1.2-2' }}</td>
                    <td>{{ row.gstChargedAt6 | number: '1.2-2' }}</td>
                    <td>{{ row.gstChargedAt8 | number: '1.2-2' }}</td>
                    <td>{{ row.gstChargedAt12 | number: '1.2-2' }}</td>
                    <td>{{ row.gstChargedAt16 | number: '1.2-2' }}</td>
                    <td>{{ row.gstChargedAt17 | number: '1.2-2' }}</td>
                    <td>{{ row.totalGst | number: '1.2-2' }}</td>
                    <td>{{ row.taxableActivityNumber || '-' }}</td>
                    <td>{{ row.revenueCapitalClassification }}</td>
                  </tr>
                </tbody>
              </app-data-table>
            </ng-container>

            <ng-container *ngSwitchCase="miraReportType.OutputTaxStatement">
              <div class="metric-grid" *ngIf="current.outputTaxStatement as statement">
                <article>
                  <span>Invoices</span>
                  <strong>{{ statement.totalInvoices }}</strong>
                </article>
                <article>
                  <span>Taxable Supplies</span>
                  <strong>MVR {{ statement.totalTaxableSupplies | number: '1.2-2' }}</strong>
                </article>
                <article>
                  <span>Out of Scope</span>
                  <strong>MVR {{ statement.totalOutOfScopeSupplies | number: '1.2-2' }}</strong>
                </article>
                <article>
                  <span>GST Amount</span>
                  <strong>MVR {{ statement.totalTaxAmount | number: '1.2-2' }}</strong>
                </article>
              </div>

              <app-data-table
                [hasData]="(current.outputTaxStatement?.rows?.length ?? 0) > 0"
                emptyTitle="No Output Tax Data"
                emptyDescription="No customer invoices were found for the selected period.">
                <thead>
                  <tr>
                    <th>Customer TIN</th>
                    <th>Customer</th>
                    <th>Invoice No.</th>
                    <th>Date</th>
                    <th>Taxable</th>
                    <th>Zero Rated</th>
                    <th>Exempt</th>
                    <th>Out of Scope</th>
                    <th>GST Rate</th>
                    <th>GST Amount</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let row of current.outputTaxStatement?.rows">
                    <td>{{ row.customerTin }}</td>
                    <td>{{ row.customerName }}</td>
                    <td>{{ row.invoiceNo }}</td>
                    <td>{{ row.invoiceDate }}</td>
                    <td>{{ row.taxableSupplies | number: '1.2-2' }}</td>
                    <td>{{ row.zeroRatedSupplies | number: '1.2-2' }}</td>
                    <td>{{ row.exemptSupplies | number: '1.2-2' }}</td>
                    <td>{{ row.outOfScopeSupplies | number: '1.2-2' }}</td>
                    <td>{{ row.gstRate | number: '1.2-2' }}</td>
                    <td>{{ row.gstAmount | number: '1.2-2' }}</td>
                  </tr>
                </tbody>
              </app-data-table>
            </ng-container>

            <ng-container *ngSwitchCase="miraReportType.BptIncomeStatement">
              <div class="metric-grid" *ngIf="current.bptIncomeStatement as statement">
                <article>
                  <span>Net Sales</span>
                  <strong>MVR {{ statement.netSales | number: '1.2-2' }}</strong>
                </article>
                <article>
                  <span>Gross Profit</span>
                  <strong>MVR {{ statement.grossProfit | number: '1.2-2' }}</strong>
                </article>
                <article>
                  <span>Operating Expenses</span>
                  <strong>MVR {{ statement.totalOperatingExpenses | number: '1.2-2' }}</strong>
                </article>
                <article class="metric-good">
                  <span>Net Income</span>
                  <strong>MVR {{ statement.netIncome | number: '1.2-2' }}</strong>
                </article>
              </div>

              <div class="statement-grid" *ngIf="current.bptIncomeStatement as statement">
                <article class="statement-panel">
                  <h4>Income Statement</h4>
                  <div class="statement-line">
                    <span>Gross sales</span>
                    <strong>MVR {{ statement.grossSales | number: '1.2-2' }}</strong>
                  </div>
                  <div class="statement-line">
                    <span>Sales returns and allowances</span>
                    <strong>MVR {{ statement.salesReturnsAndAllowances | number: '1.2-2' }}</strong>
                  </div>
                  <div class="statement-line strong">
                    <span>Net sales</span>
                    <strong>MVR {{ statement.netSales | number: '1.2-2' }}</strong>
                  </div>
                  <div class="statement-line">
                    <span>Cost of goods sold</span>
                    <strong>MVR {{ statement.costOfGoodsSold | number: '1.2-2' }}</strong>
                  </div>
                  <div class="statement-line strong">
                    <span>Gross profit</span>
                    <strong>MVR {{ statement.grossProfit | number: '1.2-2' }}</strong>
                  </div>
                  <div class="statement-line">
                    <span>Total operating expenses</span>
                    <strong>MVR {{ statement.totalOperatingExpenses | number: '1.2-2' }}</strong>
                  </div>
                  <div class="statement-line strong">
                    <span>Net income</span>
                    <strong>MVR {{ statement.netIncome | number: '1.2-2' }}</strong>
                  </div>
                </article>

                <article class="statement-panel">
                  <h4>Operating Expenses</h4>
                  <div class="statement-line" *ngFor="let line of statement.operatingExpenses">
                    <span>{{ line.label }}</span>
                    <strong>MVR {{ line.amount | number: '1.2-2' }}</strong>
                  </div>
                  <div class="statement-line" *ngIf="statement.operatingExpenses.length === 0">
                    <span>No operating expenses</span>
                    <strong>MVR 0.00</strong>
                  </div>
                </article>
              </div>
            </ng-container>

            <ng-container *ngSwitchCase="miraReportType.BptNotes">
              <div class="metric-grid" *ngIf="current.bptNotes as notes">
                <article>
                  <span>Salary Rows</span>
                  <strong>{{ notes.salaryRows.length }}</strong>
                </article>
                <article>
                  <span>Total Salary</span>
                  <strong>MVR {{ notes.totalSalary | number: '1.2-2' }}</strong>
                </article>
                <article>
                  <span>Expense Sections</span>
                  <strong>{{ notes.sections.length }}</strong>
                </article>
                <article>
                  <span>Period</span>
                  <strong>{{ current.meta.periodLabel }}</strong>
                </article>
              </div>

              <app-data-table
                [hasData]="(current.bptNotes?.salaryRows?.length ?? 0) > 0"
                emptyTitle="No Salary Note Data"
                emptyDescription="No payroll entries were found for the selected period.">
                <thead>
                  <tr>
                    <th>Staff ID</th>
                    <th>Staff Name</th>
                    <th>Avg Basic</th>
                    <th>Avg Allowance</th>
                    <th>Periods</th>
                    <th>Total Salary</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let row of current.bptNotes?.salaryRows">
                    <td>{{ row.staffCode }}</td>
                    <td>{{ row.staffName }}</td>
                    <td>{{ row.averageBasicPerPeriod | number: '1.2-2' }}</td>
                    <td>{{ row.averageAllowancePerPeriod | number: '1.2-2' }}</td>
                    <td>{{ row.periodCount }}</td>
                    <td>{{ row.totalForPeriodRange | number: '1.2-2' }}</td>
                  </tr>
                </tbody>
              </app-data-table>

              <div class="notes-section-grid" *ngIf="current.bptNotes as notes">
                <article class="notes-panel" *ngFor="let section of notes.sections; trackBy: trackExpenseSection">
                  <div class="notes-panel-head">
                    <div>
                      <h4>{{ section.title }}</h4>
                      <p>{{ section.rows.length }} line items</p>
                    </div>
                    <strong>MVR {{ section.totalAmount | number: '1.2-2' }}</strong>
                  </div>

                  <div class="notes-table-wrap">
                    <table class="notes-table">
                      <thead>
                        <tr>
                          <th>Date</th>
                          <th>Document</th>
                          <th>Payee</th>
                          <th>Detail</th>
                          <th>Amount</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr *ngFor="let row of section.rows">
                          <td>{{ row.date }}</td>
                          <td>{{ row.documentNumber }}</td>
                          <td>{{ row.payeeName }}</td>
                          <td>{{ row.detail }}</td>
                          <td>{{ row.amount | number: '1.2-2' }}</td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </article>
              </div>
            </ng-container>
          </ng-container>
        </ng-container>

        <ng-template #emptyStateTpl>
          <app-empty-state
            title="No MIRA Preview Yet"
            description="Configure the filing period and generate a preview to validate the figures before export.">
          </app-empty-state>
        </ng-template>
      </app-card>
    </section>
  `,
  styles: `
    .mira-page {
      display: grid;
      gap: 1rem;
    }
    .filters-card,
    .preview-card {
      --card-bg: linear-gradient(158deg, rgba(255,255,255,.95), rgba(246,250,255,.91));
      --card-border: rgba(255,255,255,.86);
      --card-padding: 1rem;
    }
    .filters-grid {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: .75rem;
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
      background: rgba(255,255,255,.93);
    }
    .helper-text {
      margin: .72rem 0 0;
      color: #66799f;
      font-size: .83rem;
      line-height: 1.45;
    }
    .validation-error {
      margin: .7rem 0 0;
      color: #b33a54;
      background: rgba(227, 127, 151, .13);
      border: 1px solid rgba(218, 118, 145, .34);
      border-radius: 11px;
      padding: .52rem .64rem;
      font-size: .81rem;
      font-weight: 600;
    }
    .actions-row {
      margin-top: .85rem;
      display: flex;
      justify-content: flex-end;
      gap: .5rem;
      flex-wrap: wrap;
    }
    .preview-head {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: .9rem;
      margin-bottom: .8rem;
    }
    .preview-head h3 {
      margin: 0;
      color: #30436d;
      font-size: 1.08rem;
    }
    .preview-head p {
      margin: .22rem 0 0;
      color: #66799f;
      font-size: .81rem;
      line-height: 1.4;
    }
    .meta-chip {
      min-width: 160px;
      display: grid;
      gap: .12rem;
      border-radius: 16px;
      border: 1px solid rgba(151, 171, 238, .45);
      background: linear-gradient(145deg, rgba(238, 244, 255, .94), rgba(229, 246, 255, .92));
      padding: .6rem .78rem;
      color: #4f6392;
    }
    .meta-chip span {
      font-size: .72rem;
      text-transform: uppercase;
      letter-spacing: .06em;
      font-weight: 700;
    }
    .meta-chip strong {
      font-family: var(--font-heading);
      color: #2f456d;
      font-size: 1rem;
    }
    .filing-notes {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: .55rem;
      margin-bottom: .78rem;
    }
    .notes-title {
      grid-column: 1 / -1;
      color: #6f5520;
      font-size: .78rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: .05em;
    }
    .filing-notes article {
      padding: .66rem .72rem;
      border-radius: 14px;
      border: 1px solid rgba(238, 194, 113, .44);
      background: linear-gradient(145deg, rgba(255, 247, 230, .95), rgba(255, 252, 244, .92));
      color: #8a641e;
      font-size: .79rem;
      line-height: 1.42;
      font-weight: 600;
    }
    .metric-grid {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: .58rem;
      margin-bottom: .82rem;
    }
    .metric-grid article {
      border: 1px solid rgba(211, 222, 247, .76);
      border-radius: 15px;
      padding: .72rem .76rem;
      background: linear-gradient(145deg, rgba(255,255,255,.92), rgba(242,247,255,.88));
      display: grid;
      gap: .2rem;
    }
    .metric-grid article.metric-good {
      background: linear-gradient(145deg, rgba(235, 251, 243, .94), rgba(246, 255, 250, .9));
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
      font-size: 1rem;
      line-height: 1.15;
    }
    .statement-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: .8rem;
    }
    .statement-panel {
      border: 1px solid rgba(211, 222, 247, .76);
      border-radius: 18px;
      background: linear-gradient(152deg, rgba(255,255,255,.94), rgba(245,249,255,.9));
      padding: .9rem;
      display: grid;
      gap: .5rem;
    }
    .statement-panel h4,
    .notes-panel-head h4 {
      margin: 0;
      font-size: 1rem;
      color: #31456d;
    }
    .statement-line {
      display: flex;
      justify-content: space-between;
      gap: .9rem;
      padding: .48rem 0;
      border-bottom: 1px solid rgba(214, 224, 248, .7);
      color: #566b97;
      font-size: .86rem;
    }
    .statement-line.strong {
      color: #2f456d;
      font-weight: 700;
    }
    .statement-line strong {
      color: #2f456d;
      white-space: nowrap;
    }
    .notes-section-grid {
      display: grid;
      gap: .8rem;
      margin-top: .82rem;
    }
    .notes-panel {
      border: 1px solid rgba(211, 222, 247, .76);
      border-radius: 18px;
      background: linear-gradient(152deg, rgba(255,255,255,.94), rgba(245,249,255,.9));
      padding: .9rem;
      display: grid;
      gap: .7rem;
    }
    .notes-panel-head {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: .8rem;
    }
    .notes-panel-head p {
      margin: .18rem 0 0;
      color: #7284a8;
      font-size: .8rem;
    }
    .notes-panel-head strong {
      color: #30456f;
      font-family: var(--font-heading);
      white-space: nowrap;
    }
    .notes-table-wrap {
      overflow-x: auto;
    }
    .notes-table {
      width: 100%;
      border-collapse: collapse;
      min-width: 760px;
    }
    .notes-table th,
    .notes-table td {
      padding: .58rem .62rem;
      border-bottom: 1px solid rgba(219, 229, 249, .74);
      text-align: left;
      font-size: .84rem;
      color: #4d638f;
      vertical-align: top;
    }
    .notes-table th {
      font-size: .76rem;
      text-transform: uppercase;
      letter-spacing: .05em;
      color: #5f729d;
      background: rgba(236, 243, 255, .76);
    }
    @media (max-width: 1280px) {
      .filters-grid,
      .metric-grid {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .statement-grid {
        grid-template-columns: 1fr;
      }
    }
    @media (max-width: 760px) {
      .filters-grid,
      .metric-grid {
        grid-template-columns: 1fr;
      }
      .actions-row {
        justify-content: stretch;
      }
      .actions-row app-button {
        display: block;
        width: 100%;
      }
      .preview-head {
        flex-direction: column;
      }
      .meta-chip {
        width: 100%;
      }
      .notes-table {
        min-width: 620px;
      }
    }
  `
})
export class MiraPageComponent
{
  readonly miraReportType = MiraReportType;
  readonly miraPeriodMode = MiraPeriodMode;

  readonly reportTypes: ReadonlyArray<MiraOption<MiraReportType>> = [
    { value: MiraReportType.InputTaxStatement, label: 'Input Tax Statement' },
    { value: MiraReportType.OutputTaxStatement, label: 'Output Tax Statement' },
    { value: MiraReportType.BptIncomeStatement, label: 'BPT Income Statement' },
    { value: MiraReportType.BptNotes, label: 'BPT Notes' }
  ];

  readonly periodModes: ReadonlyArray<MiraOption<MiraPeriodMode>> = [
    { value: MiraPeriodMode.Quarter, label: 'Quarter' },
    { value: MiraPeriodMode.Year, label: 'Year' },
    { value: MiraPeriodMode.CustomRange, label: 'Custom Range' }
  ];

  readonly quarterOptions: ReadonlyArray<MiraOption<number>> = [
    { value: 1, label: 'Q1' },
    { value: 2, label: 'Q2' },
    { value: 3, label: 'Q3' },
    { value: 4, label: 'Q4' }
  ];

  readonly years = Array.from({ length: 6 }, (_, index) => new Date().getFullYear() - 2 + index);
  readonly selectedReportType = signal<MiraReportType>(MiraReportType.InputTaxStatement);
  readonly selectedPeriodMode = signal<MiraPeriodMode>(MiraPeriodMode.Quarter);
  readonly selectedYear = signal(new Date().getFullYear());
  readonly selectedQuarter = signal(this.currentQuarter());
  readonly customStartDate = signal(this.todayIso());
  readonly customEndDate = signal(this.todayIso());
  readonly preview = signal<MiraReportPreview | null>(null);
  readonly previewLoading = signal(false);
  readonly exportLoading = signal<'excel' | 'pdf' | null>(null);
  readonly validationError = signal<string | null>(null);

  readonly activeTitle = computed(() =>
    this.reportTypes.find((item) => item.value === this.selectedReportType())?.label ?? 'MIRA Preview');

  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);

  onReportTypeChange(event: Event): void
  {
    this.selectedReportType.set(Number((event.target as HTMLSelectElement).value) as MiraReportType);
    this.preview.set(null);
  }

  onPeriodModeChange(event: Event): void
  {
    this.selectedPeriodMode.set(Number((event.target as HTMLSelectElement).value) as MiraPeriodMode);
    this.validationError.set(null);
    this.preview.set(null);
  }

  onYearChange(event: Event): void
  {
    this.selectedYear.set(Number((event.target as HTMLSelectElement).value));
    this.preview.set(null);
  }

  onQuarterChange(event: Event): void
  {
    this.selectedQuarter.set(Number((event.target as HTMLSelectElement).value));
    this.preview.set(null);
  }

  onCustomStartInput(event: Event): void
  {
    this.customStartDate.set((event.target as HTMLInputElement).value);
    this.preview.set(null);
  }

  onCustomEndInput(event: Event): void
  {
    this.customEndDate.set((event.target as HTMLInputElement).value);
    this.preview.set(null);
  }

  generatePreview(): void
  {
    if (this.previewLoading() || this.exportLoading() !== null)
    {
      return;
    }

    if (!this.validateFilters())
    {
      return;
    }

    this.previewLoading.set(true);
    this.api.getMiraPreview(this.buildQueryParams())
      .pipe(finalize(() => this.previewLoading.set(false)))
      .subscribe({
        next: (preview) =>
        {
          this.validationError.set(null);
          this.preview.set(preview);
        },
        error: (error) =>
        {
          const message = extractApiError(error, 'Failed to generate MIRA preview.');
          this.preview.set(null);
          this.validationError.set(message);
          this.toast.error(message);
        }
      });
  }

  exportStatement(format: 'excel' | 'pdf'): void
  {
    if (this.previewLoading() || this.exportLoading() !== null)
    {
      return;
    }

    if (!this.validateFilters())
    {
      return;
    }

    const payload = this.buildExportPayload();
    const request$ = format === 'excel'
      ? this.api.exportMiraExcel(payload)
      : this.api.exportMiraPdf(payload);

    this.exportLoading.set(format);
    request$
      .pipe(finalize(() => this.exportLoading.set(null)))
      .subscribe({
        next: (blob) =>
        {
          this.validationError.set(null);
          this.download(blob, `${this.fileSlug()}-${this.todayIso()}.${format === 'excel' ? 'xlsx' : 'pdf'}`);
          this.toast.success(`MIRA statement exported to ${format.toUpperCase()}.`);
        },
        error: (error) =>
        {
          const message = extractApiError(error, `Failed to export ${format.toUpperCase()} statement.`);
          this.validationError.set(message);
          this.toast.error(message);
        }
      });
  }

  trackExpenseSection(_: number, section: BptExpenseNoteSection): string
  {
    return `${section.title}-${section.totalAmount}`;
  }

  private validateFilters(): boolean
  {
    this.validationError.set(null);

    if (this.selectedPeriodMode() !== MiraPeriodMode.CustomRange)
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

    if (Date.parse(`${end}T00:00:00Z`) < Date.parse(`${start}T00:00:00Z`))
    {
      this.validationError.set('End date cannot be earlier than start date.');
      return false;
    }

    return true;
  }

  private buildQueryParams(): Record<string, unknown>
  {
    const params: Record<string, unknown> = {
      reportType: this.selectedReportType(),
      periodMode: this.selectedPeriodMode(),
      year: this.selectedYear()
    };

    if (this.selectedPeriodMode() === MiraPeriodMode.Quarter)
    {
      params['quarter'] = this.selectedQuarter();
    }

    if (this.selectedPeriodMode() === MiraPeriodMode.CustomRange)
    {
      params['customStartDate'] = this.customStartDate();
      params['customEndDate'] = this.customEndDate();
    }

    return params;
  }

  private buildExportPayload(): MiraReportExportRequest
  {
    return {
      reportType: this.selectedReportType(),
      periodMode: this.selectedPeriodMode(),
      year: this.selectedYear(),
      quarter: this.selectedPeriodMode() === MiraPeriodMode.Quarter ? this.selectedQuarter() : undefined,
      customStartDate: this.selectedPeriodMode() === MiraPeriodMode.CustomRange ? this.customStartDate() : undefined,
      customEndDate: this.selectedPeriodMode() === MiraPeriodMode.CustomRange ? this.customEndDate() : undefined
    };
  }

  private fileSlug(): string
  {
    switch (this.selectedReportType())
    {
      case MiraReportType.InputTaxStatement:
        return 'mira-input-tax';
      case MiraReportType.OutputTaxStatement:
        return 'mira-output-tax';
      case MiraReportType.BptIncomeStatement:
        return 'bpt-income-statement';
      case MiraReportType.BptNotes:
        return 'bpt-notes';
      default:
        return 'mira-report';
    }
  }

  private currentQuarter(): number
  {
    return Math.floor(new Date().getMonth() / 3) + 1;
  }

  private todayIso(): string
  {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private download(blob: Blob, filename: string): void
  {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
