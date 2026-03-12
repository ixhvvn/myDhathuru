import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { PagedResult, PayrollEntry, PayrollPeriod, PayrollPeriodDetail, Staff } from '../../../core/models/app.models';
import { extractApiError } from '../../../core/utils/api-error.util';
import { ACCOUNT_NUMBER_REGEX, NAME_REGEX } from '../../../core/validators/input-patterns';
import { PortalApiService } from '../../services/portal-api.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-payroll-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppButtonComponent,
    AppCardComponent,
    AppConfirmDialogComponent,
    AppCurrencyPipe,
    AppDataTableComponent,
    AppPageHeaderComponent
  ],
  template: `
    <app-page-header title="Payroll" subtitle="Staff master, monthly payroll run and salary slips"></app-page-header>

    <div class="layout-grid">
      <app-card>
        <div class="card-head">
          <h3>Staff Master</h3>
          <app-button size="sm" (clicked)="openStaffCreate()">Create Staff</app-button>
        </div>

        <app-data-table [hasData]="(staffPage()?.items?.length || 0) > 0" emptyTitle="No staff" emptyDescription="Create staff to run payroll.">
          <thead>
            <tr>
              <th>Staff ID</th>
              <th>Staff Name</th>
              <th>Designation</th>
              <th>Work Site</th>
              <th>Bank</th>
              <th>Account</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let staff of staffPage()?.items">
              <td>{{ staff.staffId }}</td>
              <td>{{ staff.staffName }}</td>
              <td>{{ staff.designation || '-' }}</td>
              <td>{{ staff.workSite || '-' }}</td>
              <td>{{ staff.bankName || '-' }}</td>
              <td>{{ staff.accountName || '-' }} / {{ staff.accountNumber || '-' }}</td>
              <td class="actions">
                <app-button size="sm" variant="secondary" (clicked)="editStaff(staff)">Edit</app-button>
                <app-button size="sm" variant="danger" (clicked)="confirmDeleteStaff(staff)">Delete</app-button>
              </td>
            </tr>
          </tbody>
        </app-data-table>
      </app-card>

      <app-card>
        <div class="card-head">
          <h3>Payroll Periods</h3>
        </div>
        <form [formGroup]="periodForm" class="period-form" (ngSubmit)="createPeriod()">
          <label>
            Year
            <select formControlName="year">
              <option *ngFor="let year of periodYearOptions()" [ngValue]="year">{{ year }}</option>
            </select>
          </label>
          <label>
            Month
            <select formControlName="month">
              <option *ngFor="let month of monthOptions" [ngValue]="month.value">{{ month.label }}</option>
            </select>
          </label>
          <app-button type="submit">Add Payroll</app-button>
        </form>
        <p class="period-note">Payroll uses a fixed 28-day calculation cycle.</p>

        <div class="period-picker">
          <label>
            Select Year
            <select [value]="selectedPeriodYear()" (change)="onSelectedPeriodYearChange($event)">
              <option *ngFor="let year of periodYearOptions()" [value]="year">{{ year }}</option>
            </select>
          </label>
          <label>
            Select Month
            <select [value]="selectedPeriodMonth()" (change)="onSelectedPeriodMonthChange($event)">
              <option *ngFor="let month of monthOptions" [value]="month.value">{{ month.label }}</option>
            </select>
          </label>
        </div>
        <p class="period-note" *ngIf="!hasMatchingPeriod()">No payroll period found for selected year/month.</p>
      </app-card>
    </div>

    <app-card *ngIf="selectedPeriod() as period" class="period-detail">
      <div class="card-head">
        <h3>Payroll Detail - {{ period.year }}-{{ period.month | number: '2.0' }}</h3>
        <div class="actions">
          <app-button variant="secondary" (clicked)="exportPayrollDetailExcel(period)">Excel</app-button>
          <app-button variant="secondary" (clicked)="exportPayrollDetailPdf(period)">PDF</app-button>
          <app-button variant="secondary" (clicked)="recalculatePeriod()">Recalculate</app-button>
          <app-button variant="danger" (clicked)="confirmDeletePeriod()">Delete Payroll</app-button>
          <span class="kpi">Total: {{ period.totalNetPayable | appCurrency }}</span>
        </div>
      </div>

      <app-data-table [hasData]="period.entries.length > 0" emptyTitle="No payroll entries" emptyDescription="This period has no staff entries.">
        <thead>
          <tr>
            <th>Staff</th>
            <th>Attended</th>
            <th>Food Days</th>
            <th>OT Pay</th>
            <th>Salary Advance</th>
            <th>Pension</th>
            <th>Total Pay</th>
            <th>Net Payable</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let entry of period.entries">
            <td>{{ entry.staffCode }} - {{ entry.staffName }}</td>
            <td>{{ entry.attendedDays }}</td>
            <td>{{ entry.foodAllowanceDays }}</td>
            <td>{{ entry.overtimePay | appCurrency }}</td>
            <td>{{ entry.salaryAdvanceDeduction | appCurrency }}</td>
            <td>{{ entry.pensionDeduction | appCurrency }}</td>
            <td>{{ entry.totalPay | appCurrency }}</td>
            <td>{{ entry.netPayable | appCurrency }}</td>
            <td class="actions">
              <app-button size="sm" variant="secondary" (clicked)="editEntry(entry)">Edit</app-button>
              <app-button size="sm" variant="secondary" (clicked)="exportSlip(entry)">PDF</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>
    </app-card>

    <div class="drawer" *ngIf="staffFormOpen()">
      <app-card>
        <h3>{{ editingStaffId() ? 'Edit Staff' : 'Create Staff' }}</h3>
        <form [formGroup]="staffForm" class="form-grid" (ngSubmit)="saveStaff()">
          <div class="two-col">
            <label>
              Staff ID
              <input formControlName="staffId">
              <small class="field-error" *ngIf="staffForm.controls.staffId.touched && staffForm.controls.staffId.hasError('required')">Staff ID is required.</small>
            </label>
            <label>
              Staff Name
              <input formControlName="staffName">
              <small class="field-error" *ngIf="staffForm.controls.staffName.touched && staffForm.controls.staffName.hasError('required')">Staff name is required.</small>
              <small class="field-error" *ngIf="staffForm.controls.staffName.touched && staffForm.controls.staffName.hasError('pattern')">Staff name must not contain numbers.</small>
            </label>
          </div>
          <div class="two-col">
            <label>
              Designation
              <input formControlName="designation">
            </label>
            <label>
              Work Site
              <input formControlName="workSite">
            </label>
          </div>
          <div class="two-col">
            <label>
              Bank
              <select formControlName="bankName">
                <option value="">Select bank</option>
                <option value="BML">BML</option>
                <option value="MIB">MIB</option>
              </select>
            </label>
            <label>
              Account Name
              <input formControlName="accountName">
              <small class="field-error" *ngIf="staffForm.controls.accountName.touched && staffForm.controls.accountName.hasError('pattern')">Account name must not contain numbers.</small>
            </label>
          </div>
          <div class="two-col">
            <label>
              Account Number
              <input formControlName="accountNumber" inputmode="numeric">
              <small class="field-error" *ngIf="staffForm.controls.accountNumber.touched && staffForm.controls.accountNumber.hasError('pattern')">Account number must contain only digits.</small>
            </label>
            <label>
              Basic
              <input type="number" formControlName="basic">
            </label>
          </div>
          <small class="field-error form-wide-error" *ngIf="hasIncompleteBankDetails()">Select bank and enter both account name and account number together.</small>
          <div class="two-col">
            <label>
              Service Allowance
              <input type="number" formControlName="serviceAllowance">
            </label>
            <label>
              Other Allowance
              <input type="number" formControlName="otherAllowance">
            </label>
          </div>
          <div class="two-col">
            <label>
              Phone Allowance
              <input type="number" formControlName="phoneAllowance">
            </label>
            <label>
              Food Rate
              <input type="number" formControlName="foodRate">
            </label>
          </div>
          <div class="actions">
            <app-button variant="secondary" (clicked)="staffFormOpen.set(false)">Cancel</app-button>
            <app-button type="submit">Save</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <div class="drawer" *ngIf="entryFormOpen()">
      <app-card>
        <h3>Edit Payroll Entry</h3>
        <form [formGroup]="entryForm" class="form-grid" (ngSubmit)="saveEntry()">
          <div class="two-col">
            <label>Attended Days <input type="number" min="0" max="28" formControlName="attendedDays"></label>
            <label>Food Days <input type="number" min="0" max="28" formControlName="foodAllowanceDays"></label>
          </div>
          <div class="two-col">
            <label>Food Rate <input type="number" formControlName="foodAllowanceRate"></label>
            <label>OT Pay <input type="number" formControlName="overtimePay"></label>
          </div>
          <label>Salary Advance <input type="number" formControlName="salaryAdvanceDeduction"></label>
          <div class="actions">
            <app-button variant="secondary" (clicked)="entryFormOpen.set(false)">Cancel</app-button>
            <app-button type="submit">Save Entry</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <app-confirm-dialog
      [open]="deleteDialogOpen()"
      title="Delete staff"
      message="Deleting staff is blocked when payroll history exists."
      (cancel)="deleteDialogOpen.set(false)"
      (confirm)="deleteStaff()"></app-confirm-dialog>

    <app-confirm-dialog
      [open]="deletePeriodDialogOpen()"
      title="Delete payroll"
      message="This will remove the selected payroll period and all related entries/salary slips."
      (cancel)="deletePeriodDialogOpen.set(false)"
      (confirm)="deletePeriod()"></app-confirm-dialog>
  `,
  styles: `
    .layout-grid {
      display: grid;
      gap: 1.15rem;
      grid-template-columns: 1.2fr 1fr;
      align-items: start;
      margin-bottom: 1.2rem;
    }
    .layout-grid > app-card {
      align-self: start;
    }
    .card-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: .7rem; }
    .period-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .5rem; align-items: end; }
    .period-picker { margin-top: .8rem; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .5rem; }
    .period-note { margin: .45rem 0 0; font-size: .78rem; color: var(--text-muted); }
    .period-detail {
      margin-top: .35rem;
      position: relative;
      z-index: 1;
    }
    .actions { display: flex; gap: .4rem; flex-wrap: wrap; }
    .kpi { align-self: center; font-weight: 700; color: #173c68; }
    label { display: grid; gap: .24rem; font-size: .82rem; color: var(--text-muted); align-content: start; }
    .field-error {
      margin-top: .05rem;
      display: block;
      font-size: .75rem;
      line-height: 1.25;
      color: #bf2f46;
    }
    .form-wide-error {
      margin-top: -.1rem;
      margin-bottom: .1rem;
      padding: .32rem .5rem;
      border-radius: 10px;
      border: 1px solid rgba(195, 67, 91, .28);
      background: rgba(245, 176, 191, .13);
    }
    input,
    select {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .52rem .6rem;
      background: rgba(255,255,255,.92);
    }
    .drawer {
      position: fixed;
      inset: 0;
      z-index: 1200;
      background: rgba(43, 54, 87, .34);
      backdrop-filter: blur(4px);
      display: grid;
      place-items: center;
      padding: 1rem;
    }
    .drawer app-card {
      width: min(720px, 100%);
      max-height: 95dvh;
      overflow: auto;
      --card-bg: linear-gradient(160deg, rgba(255,255,255,.95), rgba(245,248,255,.9));
    }
    .form-grid { display: grid; gap: .75rem; }
    .two-col { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .72rem; }
    @media (max-width: 1020px) {
      .layout-grid {
        grid-template-columns: 1fr;
        margin-bottom: 1rem;
      }
    }
    @media (max-width: 700px) {
      .card-head {
        flex-wrap: wrap;
        gap: .5rem;
      }
      .period-form {
        grid-template-columns: 1fr;
      }
      .two-col {
        grid-template-columns: 1fr;
      }
      .actions app-button {
        flex: 1 1 120px;
      }
      .drawer {
        padding: .6rem;
        place-items: start center;
        overflow: auto;
      }
    }
  `
})
export class PayrollPageComponent implements OnInit {
  readonly staffPage = signal<PagedResult<Staff> | null>(null);
  readonly periods = signal<PayrollPeriod[]>([]);
  readonly selectedPeriod = signal<PayrollPeriodDetail | null>(null);

  readonly staffFormOpen = signal(false);
  readonly editingStaffId = signal<string | null>(null);
  readonly deleteDialogOpen = signal(false);
  readonly deleteStaffTarget = signal<Staff | null>(null);
  readonly deletePeriodDialogOpen = signal(false);

  readonly entryFormOpen = signal(false);
  readonly editingEntry = signal<PayrollEntry | null>(null);

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);

  readonly staffForm = this.fb.nonNullable.group({
    staffId: ['', Validators.required],
    staffName: ['', [Validators.required, Validators.pattern(NAME_REGEX)]],
    designation: [''],
    workSite: [''],
    bankName: [''],
    accountName: ['', Validators.pattern(NAME_REGEX)],
    accountNumber: ['', Validators.pattern(ACCOUNT_NUMBER_REGEX)],
    basic: [0, Validators.required],
    serviceAllowance: [0, Validators.required],
    otherAllowance: [0, Validators.required],
    phoneAllowance: [0, Validators.required],
    foodRate: [0, Validators.required]
  });

  readonly periodForm = this.fb.nonNullable.group({
    year: [new Date().getFullYear(), Validators.required],
    month: [new Date().getMonth() + 1, Validators.required]
  });

  readonly entryForm = this.fb.nonNullable.group({
    attendedDays: [0, Validators.required],
    foodAllowanceDays: [0, Validators.required],
    foodAllowanceRate: [0, Validators.required],
    overtimePay: [0, Validators.required],
    salaryAdvanceDeduction: [0, Validators.required]
  });

  readonly monthOptions: ReadonlyArray<{ value: number; label: string }> = [
    { value: 1, label: 'January' },
    { value: 2, label: 'February' },
    { value: 3, label: 'March' },
    { value: 4, label: 'April' },
    { value: 5, label: 'May' },
    { value: 6, label: 'June' },
    { value: 7, label: 'July' },
    { value: 8, label: 'August' },
    { value: 9, label: 'September' },
    { value: 10, label: 'October' },
    { value: 11, label: 'November' },
    { value: 12, label: 'December' }
  ];

  readonly periodYearOptions = signal<number[]>(this.buildDefaultYearOptions());
  readonly selectedPeriodYear = signal(new Date().getFullYear());
  readonly selectedPeriodMonth = signal(new Date().getMonth() + 1);

  ngOnInit(): void {
    this.loadStaff();
    this.loadPeriods();
  }

  openStaffCreate(): void {
    this.editingStaffId.set(null);
    this.staffForm.reset({
      staffId: '', staffName: '', designation: '', workSite: '', bankName: '', accountName: '', accountNumber: '',
      basic: 0, serviceAllowance: 0, otherAllowance: 0, phoneAllowance: 0, foodRate: 0
    });
    this.staffFormOpen.set(true);
  }

  editStaff(staff: Staff): void {
    this.editingStaffId.set(staff.id);
    this.staffForm.reset({
      staffId: staff.staffId,
      staffName: staff.staffName,
      designation: staff.designation || '',
      workSite: staff.workSite || '',
      bankName: staff.bankName || '',
      accountName: staff.accountName || '',
      accountNumber: staff.accountNumber || '',
      basic: staff.basic,
      serviceAllowance: staff.serviceAllowance,
      otherAllowance: staff.otherAllowance,
      phoneAllowance: staff.phoneAllowance,
      foodRate: staff.foodRate
    });
    this.staffFormOpen.set(true);
  }

  saveStaff(): void {
    if (this.staffForm.invalid || this.hasIncompleteBankDetails()) {
      this.staffForm.markAllAsTouched();
      this.toast.error('Please complete required staff fields.');
      return;
    }

    const payload = this.staffForm.getRawValue();
    const request$ = this.editingStaffId()
      ? this.api.updateStaff(this.editingStaffId()!, payload)
      : this.api.createStaff(payload);

    request$.subscribe({
      next: () => {
        this.toast.success(`Staff ${this.editingStaffId() ? 'updated' : 'created'} successfully.`);
        this.staffFormOpen.set(false);
        this.loadStaff();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to save staff.'))
    });
  }

  confirmDeleteStaff(staff: Staff): void {
    this.deleteStaffTarget.set(staff);
    this.deleteDialogOpen.set(true);
  }

  deleteStaff(): void {
    const staff = this.deleteStaffTarget();
    if (!staff) {
      return;
    }

    this.api.deleteStaff(staff.id).subscribe({
      next: () => {
        this.toast.success('Staff deleted.');
        this.deleteDialogOpen.set(false);
        this.loadStaff();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to delete staff.'))
    });
  }

  createPeriod(): void {
    if (this.periodForm.invalid) {
      return;
    }

    const value = this.periodForm.getRawValue();
    const payload = {
      year: Number(value.year),
      month: Number(value.month),
      startDate: `${Number(value.year)}-${this.padMonth(Number(value.month))}-01`,
      endDate: `${Number(value.year)}-${this.padMonth(Number(value.month))}-28`
    };

    this.api.createPayrollPeriod(payload).subscribe({
      next: (detail) => {
        this.toast.success('Payroll period created.');
        this.selectedPeriod.set(detail);
        this.selectedPeriodYear.set(detail.year);
        this.selectedPeriodMonth.set(detail.month);
        this.loadPeriods();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to create payroll period.'))
    });
  }

  loadPeriods(): void {
    this.api.getPayrollPeriods().subscribe({
      next: (periods) => {
        this.periods.set(periods);
        this.periodYearOptions.set(this.buildYearOptionsFromPeriods(periods));

        if (periods.length > 0 && !this.findPeriodByYearMonth(this.selectedPeriodYear(), this.selectedPeriodMonth())) {
          this.selectedPeriodYear.set(periods[0].year);
          this.selectedPeriodMonth.set(periods[0].month);
        }

        this.syncSelectedPeriodFromPicker();
      },
      error: () => this.toast.error('Failed to load payroll periods.')
    });
  }

  selectPeriod(period: PayrollPeriod): void {
    this.api.getPayrollPeriod(period.id).subscribe({
      next: (detail) => this.selectedPeriod.set(detail),
      error: () => this.toast.error('Failed to load payroll period details.')
    });
  }

  recalculatePeriod(): void {
    const period = this.selectedPeriod();
    if (!period) {
      return;
    }

    this.api.recalculatePayrollPeriod(period.id).subscribe({
      next: () => {
        this.toast.success('Payroll period recalculated.');
        this.selectPeriod(period);
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to recalculate period.'))
    });
  }

  confirmDeletePeriod(): void {
    if (!this.selectedPeriod()) {
      return;
    }

    this.deletePeriodDialogOpen.set(true);
  }

  deletePeriod(): void {
    const period = this.selectedPeriod();
    if (!period) {
      return;
    }

    this.api.deletePayrollPeriod(period.id).subscribe({
      next: () => {
        this.toast.success('Payroll period deleted.');
        this.deletePeriodDialogOpen.set(false);
        this.selectedPeriod.set(null);
        this.loadPeriods();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to delete payroll period.'))
    });
  }

  editEntry(entry: PayrollEntry): void {
    this.editingEntry.set(entry);
    this.entryForm.reset({
      attendedDays: entry.attendedDays,
      foodAllowanceDays: entry.foodAllowanceDays,
      foodAllowanceRate: entry.foodAllowanceRate,
      overtimePay: entry.overtimePay,
      salaryAdvanceDeduction: entry.salaryAdvanceDeduction
    });
    this.entryFormOpen.set(true);
  }

  saveEntry(): void {
    const period = this.selectedPeriod();
    const entry = this.editingEntry();
    if (!period || !entry || this.entryForm.invalid) {
      return;
    }

    this.api.updatePayrollEntry(period.id, entry.id, this.entryForm.getRawValue()).subscribe({
      next: () => {
        this.toast.success('Payroll entry updated.');
        this.entryFormOpen.set(false);
        this.selectPeriod(period);
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to update payroll entry.'))
    });
  }

  exportSlip(entry: PayrollEntry): void {
    this.api.exportSalarySlip(entry.id).subscribe({
      next: (file) => this.download(file, `${entry.staffCode}-salary-slip.pdf`),
      error: () => this.toast.error('Failed to export salary slip PDF.')
    });
  }

  exportPayrollDetailPdf(period: PayrollPeriodDetail): void {
    this.api.exportPayrollPeriodPdf(period.id).subscribe({
      next: (file) => this.download(file, `payroll-detail-${period.year}-${this.padMonth(period.month)}.pdf`),
      error: () => this.toast.error('Failed to export payroll detail PDF.')
    });
  }

  exportPayrollDetailExcel(period: PayrollPeriodDetail): void {
    this.api.exportPayrollPeriodExcel(period.id).subscribe({
      next: (file) => this.download(file, `payroll-detail-${period.year}-${this.padMonth(period.month)}.xlsx`),
      error: () => this.toast.error('Failed to export payroll detail Excel.')
    });
  }

  onSelectedPeriodYearChange(event: Event): void {
    this.selectedPeriodYear.set(this.parseSelectedValue(event, this.selectedPeriodYear()));
    this.syncSelectedPeriodFromPicker();
  }

  onSelectedPeriodMonthChange(event: Event): void {
    this.selectedPeriodMonth.set(this.parseSelectedValue(event, this.selectedPeriodMonth()));
    this.syncSelectedPeriodFromPicker();
  }

  hasMatchingPeriod(): boolean {
    return this.findPeriodByYearMonth(this.selectedPeriodYear(), this.selectedPeriodMonth()) !== null;
  }

  private toDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private loadStaff(): void {
    this.api.getStaff({ pageNumber: 1, pageSize: 200 }).subscribe({
      next: (result) => this.staffPage.set(result),
      error: () => this.toast.error('Failed to load staff list.')
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

  private padMonth(month: number): string {
    return `${month}`.padStart(2, '0');
  }

  private parseSelectedValue(event: Event, fallback: number): number {
    const raw = Number((event.target as HTMLSelectElement | null)?.value);
    return Number.isFinite(raw) ? raw : fallback;
  }

  private syncSelectedPeriodFromPicker(): void {
    const period = this.findPeriodByYearMonth(this.selectedPeriodYear(), this.selectedPeriodMonth());
    if (!period) {
      this.selectedPeriod.set(null);
      return;
    }

    if (this.selectedPeriod()?.id === period.id) {
      return;
    }

    this.selectPeriod(period);
  }

  private findPeriodByYearMonth(year: number, month: number): PayrollPeriod | null {
    return this.periods().find((period) => period.year === year && period.month === month) ?? null;
  }

  private buildDefaultYearOptions(): number[] {
    const currentYear = new Date().getFullYear();
    const years: number[] = [];
    for (let year = currentYear - 5; year <= currentYear + 5; year++) {
      years.push(year);
    }
    return years.sort((a, b) => b - a);
  }

  private buildYearOptionsFromPeriods(periods: PayrollPeriod[]): number[] {
    const set = new Set<number>(this.buildDefaultYearOptions());
    for (const period of periods) {
      set.add(period.year);
    }

    return [...set].sort((a, b) => b - a);
  }

  private readError(error: unknown, fallback: string): string {
    return extractApiError(error, fallback);
  }

  hasIncompleteBankDetails(): boolean {
    const raw = this.staffForm.getRawValue();
    const hasBank = !!raw.bankName?.trim();
    const hasAccountName = !!raw.accountName?.trim();
    const hasAccountNumber = !!raw.accountNumber?.trim();

    if (!hasBank && !hasAccountName && !hasAccountNumber) {
      return false;
    }

    return !(hasBank && hasAccountName && hasAccountNumber);
  }
}


