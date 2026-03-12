import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppDateBadgeComponent } from '../../../shared/components/app-date-badge/app-date-badge.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AccountStatement, Customer, StatementRow } from '../../../core/models/app.models';
import { PortalApiService } from '../../services/portal-api.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-statements-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppButtonComponent,
    AppCardComponent,
    AppCurrencyPipe,
    AppDataTableComponent,
    AppDateBadgeComponent,
    AppPageHeaderComponent
  ],
  template: `
    <app-page-header title="Account Statements" subtitle="Customer account history, opening balances and running totals"></app-page-header>

    <app-card>
      <form [formGroup]="filterForm" class="filters" (ngSubmit)="loadStatement()">
        <label>Customer
          <select formControlName="customerId">
            <option value="">Select customer</option>
            <option *ngFor="let customer of customers()" [value]="customer.id">{{ customer.name }}</option>
          </select>
        </label>
        <label>Year <input type="number" formControlName="year"></label>
        <label>Opening Balance (MVR) <input type="number" formControlName="openingBalanceMvr"></label>
        <label>Opening Balance (USD) <input type="number" formControlName="openingBalanceUsd"></label>
        <div class="btns">
          <app-button type="submit">Load Statement</app-button>
          <app-button variant="secondary" (clicked)="saveOpeningBalance()">Save Opening Balance</app-button>
          <app-button variant="secondary" (clicked)="exportStatement()">Export PDF</app-button>
        </div>
      </form>

      <div *ngIf="statement() as summary" class="summary-grid">
        <div>
          <label>Statement No</label>
          <strong>{{ summary.statementNo }}</strong>
        </div>
        <div>
          <label>Opening Balance</label>
          <strong>{{ formatDualCurrency(summary.openingBalance.mvr, summary.openingBalance.usd) }}</strong>
        </div>
        <div>
          <label>Total Invoiced</label>
          <strong>{{ formatDualCurrency(summary.totalInvoiced.mvr, summary.totalInvoiced.usd) }}</strong>
        </div>
        <div>
          <label>Total Received</label>
          <strong>{{ formatDualCurrency(summary.totalReceived.mvr, summary.totalReceived.usd) }}</strong>
        </div>
        <div>
          <label>Total Pending</label>
          <strong>{{ formatDualCurrency(summary.totalPending.mvr, summary.totalPending.usd) }}</strong>
        </div>
      </div>

      <app-data-table [hasData]="(statement()?.rows?.length || 0) > 0" emptyTitle="No statement rows" emptyDescription="Select customer and year to generate statement.">
        <thead>
          <tr>
            <th>#</th>
            <th>Date</th>
            <th>Description</th>
            <th>Reference</th>
            <th>Currency</th>
            <th>Amount</th>
            <th>Payments</th>
            <th>Received On</th>
            <th>Balance (MVR | USD)</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let row of statementRowsWithDualBalance()">
            <td>{{ row.index }}</td>
            <td><app-date-badge [value]="row.date || ''"></app-date-badge></td>
            <td>{{ row.description }}</td>
            <td>{{ row.reference || '-' }}</td>
            <td>{{ row.currency }}</td>
            <td>{{ row.amount | appCurrency: row.currency }}</td>
            <td>{{ row.payments | appCurrency: row.currency }}</td>
            <td>
              <ng-container *ngIf="row.receivedOn; else noReceivedDate">
                <app-date-badge [value]="row.receivedOn || ''"></app-date-badge>
              </ng-container>
              <ng-template #noReceivedDate>-</ng-template>
            </td>
            <td>{{ formatDualCurrency(row.balanceMvr, row.balanceUsd) }}</td>
          </tr>
        </tbody>
      </app-data-table>
    </app-card>
  `,
  styles: `
    .filters {
      display: grid;
      grid-template-columns: 2fr 1fr 1fr 1fr auto;
      gap: .6rem;
      margin-bottom: .9rem;
      align-items: end;
    }
    label { display: grid; gap: .2rem; font-size: .82rem; color: var(--text-muted); }
    select, input {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .52rem .62rem;
      background: rgba(255,255,255,.92);
    }
    .btns { display: flex; gap: .45rem; flex-wrap: wrap; }
    .summary-grid {
      margin-bottom: .8rem;
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
      gap: .6rem;
      background: linear-gradient(145deg, rgba(246, 250, 255, .92), rgba(237, 247, 255, .86));
      border: 1px solid #dce7f8;
      border-radius: 14px;
      padding: .8rem;
    }
    .summary-grid label { font-size: .74rem; text-transform: uppercase; letter-spacing: .05em; }
    .summary-grid strong { font-size: .97rem; }
    @media (max-width: 900px) {
      .filters {
        grid-template-columns: 1fr 1fr 1fr;
      }
      .btns {
        grid-column: 1 / -1;
      }
    }
    @media (max-width: 640px) {
      .filters {
        grid-template-columns: 1fr;
      }
      .btns app-button {
        width: 100%;
      }
      .summary-grid {
        grid-template-columns: 1fr;
      }
    }
  `
})
export class StatementsPageComponent implements OnInit {
  readonly customers = signal<Customer[]>([]);
  readonly statement = signal<AccountStatement | null>(null);
  readonly statementRowsWithDualBalance = computed<StatementRowWithDualBalance[]>(() => {
    const current = this.statement();
    if (!current) {
      return [];
    }

    let runningMvr = 0;
    let runningUsd = 0;

    return current.rows.map((row) => {
      if (this.normalizeCurrency(row.currency) === 'USD') {
        runningUsd = row.balance;
      } else {
        runningMvr = row.balance;
      }

      return {
        ...row,
        balanceMvr: runningMvr,
        balanceUsd: runningUsd
      };
    });
  });

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);

  readonly filterForm = this.fb.nonNullable.group({
    customerId: ['', Validators.required],
    year: [new Date().getFullYear(), Validators.required],
    openingBalanceMvr: [0],
    openingBalanceUsd: [0]
  });

  ngOnInit(): void {
    this.api.getCustomers({ pageNumber: 1, pageSize: 500 }).subscribe({
      next: (result) => this.customers.set(result.items),
      error: () => this.toast.error('Failed to load customers.')
    });
  }

  loadStatement(): void {
    if (this.filterForm.invalid) {
      this.toast.error('Select customer and year to load statement.');
      return;
    }

    const value = this.filterForm.getRawValue();
    this.api.getStatement(value.customerId, Number(value.year)).subscribe({
      next: (statement) => {
        this.statement.set(statement);
        this.filterForm.patchValue(
          {
            openingBalanceMvr: statement.openingBalance.mvr,
            openingBalanceUsd: statement.openingBalance.usd
          },
          { emitEvent: false });
      },
      error: (error) => this.toast.error(this.readError(error, 'Failed to load statement.'))
    });
  }

  saveOpeningBalance(): void {
    if (this.filterForm.invalid) {
      return;
    }

    const value = this.filterForm.getRawValue();
    this.api.saveOpeningBalance({
      customerId: value.customerId,
      year: Number(value.year),
      openingBalanceMvr: Number(value.openingBalanceMvr),
      openingBalanceUsd: Number(value.openingBalanceUsd)
    }).subscribe({
      next: () => {
        this.toast.success('Opening balance saved.');
        this.loadStatement();
      },
      error: (error) => this.toast.error(this.readError(error, 'Failed to save opening balance.'))
    });
  }

  exportStatement(): void {
    if (this.filterForm.invalid) {
      return;
    }

    const value = this.filterForm.getRawValue();
    this.api.exportStatement(value.customerId, Number(value.year)).subscribe({
      next: (file) => this.download(file, `statement-${value.year}.pdf`),
      error: () => this.toast.error('Failed to export statement PDF.')
    });
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
    const apiError = error as { error?: { message?: string; errors?: string[] } };
    return apiError?.error?.errors?.[0] ?? apiError?.error?.message ?? fallback;
  }

  formatDualCurrency(mvr: number, usd: number): string {
    return `MVR ${this.formatAmount(mvr)} | USD ${this.formatAmount(usd)}`;
  }

  private formatAmount(value: number): string {
    return Number(value ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  private normalizeCurrency(currency: string): 'MVR' | 'USD' {
    return (currency ?? '').trim().toUpperCase() === 'USD' ? 'USD' : 'MVR';
  }
}

interface StatementRowWithDualBalance extends StatementRow {
  balanceMvr: number;
  balanceUsd: number;
}


