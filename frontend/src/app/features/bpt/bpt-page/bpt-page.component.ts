import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  ApprovalStatus,
  BptAdjustmentRecord,
  BptCategoryLookup,
  BptClassificationGroup,
  BptExchangeRate,
  BptExpenseMapping,
  BptPeriodMode,
  BptReport,
  BptSourceModule,
  Customer,
  InvoiceListItem,
  OtherIncomeEntryRecord,
  SalesAdjustmentRecord,
  SalesAdjustmentType
} from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { PortalApiService } from '../../services/portal-api.service';

type BptWorkspaceTab =
  | 'summary'
  | 'transactions'
  | 'mappings'
  | 'exchangeRates'
  | 'salesAdjustments'
  | 'otherIncome'
  | 'adjustments';

type DeleteIntentKind = 'exchangeRate' | 'salesAdjustment' | 'otherIncome' | 'adjustment';

type DeleteIntent = {
  kind: DeleteIntentKind;
  id: string;
  label: string;
};

interface Option<TValue> {
  value: TValue;
  label: string;
}

@Component({
  selector: 'app-bpt-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppButtonComponent,
    AppCardComponent,
    AppConfirmDialogComponent,
    AppDataTableComponent,
    AppEmptyStateComponent,
    AppPageHeaderComponent
  ],
  templateUrl: './bpt-page.component.html',
  styleUrls: ['./bpt-page.component.scss']
})
export class BptPageComponent implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);
  private readonly formBuilder = inject(FormBuilder);

  readonly bptPeriodMode = BptPeriodMode;
  readonly periodModes: ReadonlyArray<Option<BptPeriodMode>> = [
    { value: BptPeriodMode.Quarter, label: 'Quarter' },
    { value: BptPeriodMode.Year, label: 'Year' },
    { value: BptPeriodMode.CustomRange, label: 'Custom Range' }
  ];
  readonly quarterOptions: ReadonlyArray<Option<number>> = [
    { value: 1, label: 'Q1' },
    { value: 2, label: 'Q2' },
    { value: 3, label: 'Q3' },
    { value: 4, label: 'Q4' }
  ];
  readonly approvalStatusOptions: ReadonlyArray<Option<ApprovalStatus>> = [
    { value: 'Approved', label: 'Approved' },
    { value: 'Draft', label: 'Draft' },
    { value: 'Rejected', label: 'Rejected' }
  ];
  readonly classificationGroupOptions: ReadonlyArray<Option<BptClassificationGroup>> = [
    { value: 'Revenue', label: 'Revenue' },
    { value: 'SalesReturnAllowance', label: 'Sales Returns / Allowances' },
    { value: 'CostOfGoodsSold', label: 'Cost of Goods Sold' },
    { value: 'OperatingExpense', label: 'Operating Expense' },
    { value: 'OtherIncome', label: 'Other Income' },
    { value: 'Excluded', label: 'Excluded' }
  ];

  readonly selectedPeriodMode = signal<BptPeriodMode>(BptPeriodMode.Quarter);
  readonly selectedYear = signal(new Date().getFullYear());
  readonly selectedQuarter = signal(this.currentQuarter());
  readonly customStartDate = signal(this.todayIso());
  readonly customEndDate = signal(this.todayIso());
  readonly validationError = signal<string | null>(null);
  readonly activeTab = signal<BptWorkspaceTab>('summary');
  readonly reportLoading = signal(false);
  readonly exportLoading = signal<'excel' | 'pdf' | null>(null);
  readonly report = signal<BptReport | null>(null);

  readonly mappings = signal<BptExpenseMapping[]>([]);
  readonly categories = signal<BptCategoryLookup[]>([]);
  readonly exchangeRates = signal<BptExchangeRate[]>([]);
  readonly salesAdjustments = signal<SalesAdjustmentRecord[]>([]);
  readonly otherIncomeEntries = signal<OtherIncomeEntryRecord[]>([]);
  readonly adjustments = signal<BptAdjustmentRecord[]>([]);
  readonly customers = signal<Customer[]>([]);
  readonly invoices = signal<InvoiceListItem[]>([]);

  readonly transactionSearch = signal('');
  readonly transactionGroupFilter = signal('');
  readonly transactionSourceFilter = signal('');
  readonly transactionCategoryFilter = signal('');

  readonly mappingEditorOpen = signal(false);
  readonly mappingSaving = signal(false);
  readonly mappingTarget = signal<BptExpenseMapping | null>(null);

  readonly exchangeRateEditorOpen = signal(false);
  readonly exchangeRateSaving = signal(false);
  readonly exchangeRateEditingId = signal<string | null>(null);

  readonly salesAdjustmentEditorOpen = signal(false);
  readonly salesAdjustmentSaving = signal(false);
  readonly salesAdjustmentEditingId = signal<string | null>(null);

  readonly otherIncomeEditorOpen = signal(false);
  readonly otherIncomeSaving = signal(false);
  readonly otherIncomeEditingId = signal<string | null>(null);

  readonly adjustmentEditorOpen = signal(false);
  readonly adjustmentSaving = signal(false);
  readonly adjustmentEditingId = signal<string | null>(null);

  readonly deleteDialogOpen = signal(false);
  readonly deleteIntent = signal<DeleteIntent | null>(null);

  readonly yearOptions = Array.from({ length: 8 }, (_, index) => new Date().getFullYear() - 3 + index);
  readonly selectedRangeLabel = computed(() => {
    const range = this.resolveSelectedRange();
    return `${range.start} to ${range.end}`;
  });
  readonly invoiceOptions = computed(() => this.invoices());
  readonly customerOptions = computed(() => this.customers());
  readonly mappingTargetCategories = computed(() =>
    this.categories().filter((item) =>
      item.classificationGroup === 'OperatingExpense'
      || item.classificationGroup === 'CostOfGoodsSold'
      || item.classificationGroup === 'Excluded'));
  readonly adjustableCategories = computed(() =>
    this.categories().filter((item) => item.classificationGroup !== 'Excluded'));
  readonly mappedCogsCount = computed(() =>
    this.mappings().filter((item) => item.classificationGroup === 'CostOfGoodsSold' && item.isActive).length);
  readonly activeExchangeRateCount = computed(() =>
    this.exchangeRates().filter((item) => item.isActive).length);
  readonly latestExchangeRateLabel = computed(() => {
    const latest = this.exchangeRates()[0];
    if (!latest) {
      return 'No rate';
    }

    return `${latest.currency} ${latest.rateToMvr.toFixed(4)}`;
  });
  readonly transactionCategoryOptions = computed(() => {
    const unique = new Set(
      (this.report()?.transactions ?? [])
        .map((item) => item.bptCategoryName)
        .filter((item) => !!item));

    return Array.from(unique).sort((left, right) => left.localeCompare(right));
  });
  readonly sourceModuleOptions = computed(() => {
    const unique = new Set((this.report()?.transactions ?? []).map((item) => item.sourceModule));
    return Array.from(unique)
      .sort((left, right) => this.sourceModuleLabel(left).localeCompare(this.sourceModuleLabel(right)))
      .map((item) => ({ value: item, label: this.sourceModuleLabel(item) }));
  });
  readonly filteredTransactions = computed(() => {
    const search = this.transactionSearch().trim().toLowerCase();
    const group = this.transactionGroupFilter();
    const source = this.transactionSourceFilter();
    const category = this.transactionCategoryFilter();

    return (this.report()?.transactions ?? []).filter((item) => {
      if (group && item.classificationGroup !== group) {
        return false;
      }

      if (source && item.sourceModule !== source) {
        return false;
      }

      if (category && item.bptCategoryName !== category) {
        return false;
      }

      if (!search) {
        return true;
      }

      const haystack = [
        item.sourceDocumentNumber,
        item.counterpartyName,
        item.description,
        item.bptCategoryName,
        item.notes,
        item.sourceStatus
      ]
        .filter((value) => !!value)
        .join(' ')
        .toLowerCase();

      return haystack.includes(search);
    });
  });
  readonly filteredTransactionAmountMvr = computed(() =>
    this.round2(this.filteredTransactions().reduce((sum, item) => sum + (item.amountMvr ?? 0), 0)));
  readonly sourceBreakdown = computed(() => {
    const buckets = new Map<string, { label: string; count: number; amount: number }>();

    for (const row of this.report()?.transactions ?? []) {
      const key = row.sourceModule;
      const current = buckets.get(key) ?? { label: this.sourceModuleLabel(row.sourceModule), count: 0, amount: 0 };
      current.count += 1;
      current.amount += row.amountMvr ?? 0;
      buckets.set(key, current);
    }

    return Array.from(buckets.values())
      .map((item) => ({ ...item, amount: this.round2(item.amount) }))
      .sort((left, right) => right.amount - left.amount);
  });

  readonly mappingForm = this.formBuilder.nonNullable.group({
    bptCategoryId: ['', Validators.required],
    isActive: true,
    notes: ['', Validators.maxLength(600)]
  });

  readonly exchangeRateForm = this.formBuilder.group({
    rateDate: [this.todayIso(), Validators.required],
    currency: ['USD', Validators.required],
    rateToMvr: [1, [Validators.required, Validators.min(0.000001)]],
    source: ['', Validators.maxLength(120)],
    notes: ['', Validators.maxLength(600)],
    isActive: [true]
  });

  readonly salesAdjustmentForm = this.formBuilder.group({
    adjustmentType: ['Return', Validators.required],
    transactionDate: [this.todayIso(), Validators.required],
    relatedInvoiceId: [''],
    customerId: [''],
    currency: ['MVR', Validators.required],
    amountOriginal: [0, [Validators.required, Validators.min(0.01)]],
    exchangeRate: [null as number | null],
    approvalStatus: ['Approved', Validators.required],
    notes: ['', Validators.maxLength(600)]
  });

  readonly otherIncomeForm = this.formBuilder.group({
    transactionDate: [this.todayIso(), Validators.required],
    customerId: [''],
    counterpartyName: ['', Validators.maxLength(200)],
    description: ['', [Validators.required, Validators.maxLength(300)]],
    currency: ['MVR', Validators.required],
    amountOriginal: [0, [Validators.required, Validators.min(0.01)]],
    exchangeRate: [null as number | null],
    approvalStatus: ['Approved', Validators.required],
    notes: ['', Validators.maxLength(600)]
  });

  readonly adjustmentForm = this.formBuilder.group({
    transactionDate: [this.todayIso(), Validators.required],
    description: ['', [Validators.required, Validators.maxLength(300)]],
    bptCategoryId: ['', Validators.required],
    currency: ['MVR', Validators.required],
    amountOriginal: [0, [Validators.required, Validators.min(0.01)]],
    exchangeRate: [null as number | null],
    approvalStatus: ['Approved', Validators.required],
    notes: ['', Validators.maxLength(600)]
  });

  ngOnInit(): void {
    this.loadStaticLookups();
    this.generateReport();
  }

  setActiveTab(tab: BptWorkspaceTab): void {
    this.activeTab.set(tab);
  }

  onPeriodModeChange(event: Event): void {
    this.selectedPeriodMode.set(Number(this.asSelectValue(event)) as BptPeriodMode);
    this.validationError.set(null);
  }

  onYearChange(event: Event): void {
    this.selectedYear.set(Number(this.asSelectValue(event)));
    this.validationError.set(null);
  }

  onQuarterChange(event: Event): void {
    this.selectedQuarter.set(Number(this.asSelectValue(event)));
    this.validationError.set(null);
  }

  onCustomStartInput(event: Event): void {
    this.customStartDate.set(this.asInputValue(event));
    this.validationError.set(null);
  }

  onCustomEndInput(event: Event): void {
    this.customEndDate.set(this.asInputValue(event));
    this.validationError.set(null);
  }

  generateReport(): void {
    if (this.reportLoading() || this.exportLoading() !== null) {
      return;
    }

    if (!this.validateFilters()) {
      return;
    }

    this.refreshManagementData();
    this.reportLoading.set(true);
    this.api.getBptReport(this.buildReportParams())
      .pipe(finalize(() => this.reportLoading.set(false)))
      .subscribe({
        next: (report) => this.report.set(report),
        error: (error) => {
          const message = extractApiError(error, 'Failed to generate the BPT statement.');
          this.toast.error(message);

          if (message.toLowerCase().includes('exchange rate')) {
            this.activeTab.set('exchangeRates');
          }
        }
      });
  }

  exportStatement(format: 'excel' | 'pdf'): void {
    if (!this.report() || this.reportLoading() || this.exportLoading() !== null) {
      return;
    }

    if (!this.validateFilters()) {
      return;
    }

    this.exportLoading.set(format);
    const request$ = format === 'excel'
      ? this.api.exportBptExcel(this.buildReportParams())
      : this.api.exportBptPdf(this.buildReportParams());

    request$
      .pipe(finalize(() => this.exportLoading.set(null)))
      .subscribe({
        next: (blob) => {
          this.download(blob, `bpt-statement-${this.todayIso()}.${format === 'excel' ? 'xlsx' : 'pdf'}`);
          this.toast.success(`BPT statement exported to ${format.toUpperCase()}.`);
        },
        error: (error) => {
          this.toast.error(extractApiError(error, `Failed to export ${format.toUpperCase()} BPT statement.`));
        }
      });
  }

  hasActiveTransactionFilters(): boolean {
    return !!(
      this.transactionSearch().trim()
      || this.transactionGroupFilter()
      || this.transactionSourceFilter()
      || this.transactionCategoryFilter());
  }

  clearTransactionFilters(): void {
    this.transactionSearch.set('');
    this.transactionGroupFilter.set('');
    this.transactionSourceFilter.set('');
    this.transactionCategoryFilter.set('');
  }

  drillToGroup(group: BptClassificationGroup): void {
    this.activeTab.set('transactions');
    this.transactionGroupFilter.set(group);
  }

  drillToCategory(categoryName: string): void {
    this.activeTab.set('transactions');
    this.transactionCategoryFilter.set(categoryName);
  }

  classificationGroupLabel(value: BptClassificationGroup | string): string {
    switch (value) {
      case 'SalesReturnAllowance':
        return 'Sales Returns / Allowances';
      case 'CostOfGoodsSold':
        return 'Cost of Goods Sold';
      case 'OperatingExpense':
        return 'Operating Expense';
      case 'OtherIncome':
        return 'Other Income';
      case 'Excluded':
        return 'Excluded';
      default:
        return 'Revenue';
    }
  }

  sourceModuleLabel(value: BptSourceModule | string): string {
    switch (value) {
      case 'SalesAdjustment':
        return 'Sales Adjustment';
      case 'OtherIncome':
        return 'Other Income';
      case 'ReceivedInvoice':
        return 'Received Invoice';
      case 'ExpenseEntry':
        return 'Manual Expense';
      case 'RentEntry':
        return 'Rent';
      case 'BptAdjustment':
        return 'BPT Adjustment';
      default:
        return value === 'Payroll' ? 'Payroll' : 'Invoice';
    }
  }

  salesAdjustmentTypeLabel(value: SalesAdjustmentType | string): string {
    return value === 'Allowance' ? 'Allowance' : 'Return';
  }

  asInputValue(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  asSelectValue(event: Event): string {
    return (event.target as HTMLSelectElement).value;
  }

  openMappingEditor(row: BptExpenseMapping): void {
    this.mappingTarget.set(row);
    this.mappingForm.reset({
      bptCategoryId: this.normalizeLookupId(row.bptCategoryId),
      isActive: row.isActive,
      notes: row.notes ?? ''
    });
    this.mappingEditorOpen.set(true);
  }

  closeMappingEditor(): void {
    this.mappingEditorOpen.set(false);
    this.mappingTarget.set(null);
  }

  saveMapping(): void {
    const target = this.mappingTarget();
    if (!target) {
      return;
    }

    if (this.mappingForm.invalid || this.mappingSaving()) {
      this.mappingForm.markAllAsTouched();
      return;
    }

    const raw = this.mappingForm.getRawValue();
    this.mappingSaving.set(true);
    this.api.updateBptMapping(target.expenseCategoryId, {
      bptCategoryId: raw.bptCategoryId,
      isActive: raw.isActive,
      notes: raw.notes?.trim() || undefined
    })
      .pipe(finalize(() => this.mappingSaving.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('BPT mapping updated.');
          this.closeMappingEditor();
          this.loadMappings();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Failed to save the BPT mapping.'))
      });
  }

  openExchangeRateCreate(): void {
    this.exchangeRateEditingId.set(null);
    this.exchangeRateForm.reset({
      rateDate: this.todayIso(),
      currency: 'USD',
      rateToMvr: 1,
      source: '',
      notes: '',
      isActive: true
    });
    this.exchangeRateEditorOpen.set(true);
  }

  openExchangeRateEdit(row: BptExchangeRate): void {
    this.exchangeRateEditingId.set(row.id);
    this.exchangeRateForm.reset({
      rateDate: row.rateDate,
      currency: row.currency,
      rateToMvr: row.rateToMvr,
      source: row.source ?? '',
      notes: row.notes ?? '',
      isActive: row.isActive
    });
    this.exchangeRateEditorOpen.set(true);
  }

  closeExchangeRateEditor(): void {
    if (this.exchangeRateSaving()) {
      return;
    }

    this.exchangeRateEditorOpen.set(false);
    this.exchangeRateEditingId.set(null);
  }

  saveExchangeRate(): void {
    if (this.exchangeRateForm.invalid || this.exchangeRateSaving()) {
      this.exchangeRateForm.markAllAsTouched();
      return;
    }

    const raw = this.exchangeRateForm.getRawValue();
    const payload = {
      rateDate: raw.rateDate ?? this.todayIso(),
      currency: (raw.currency ?? 'USD').trim().toUpperCase(),
      rateToMvr: Number(raw.rateToMvr ?? 0),
      source: raw.source?.trim() || undefined,
      notes: raw.notes?.trim() || undefined,
      isActive: !!raw.isActive
    };

    this.exchangeRateSaving.set(true);
    const request$ = this.exchangeRateEditingId()
      ? this.api.updateBptExchangeRate(this.exchangeRateEditingId()!, payload)
      : this.api.createBptExchangeRate(payload);

    request$
      .pipe(finalize(() => this.exchangeRateSaving.set(false)))
      .subscribe({
        next: () => {
          this.toast.success(this.exchangeRateEditingId() ? 'Exchange rate updated.' : 'Exchange rate created.');
          this.closeExchangeRateEditor();
          this.loadExchangeRates();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Failed to save exchange rate.'))
      });
  }

  openSalesAdjustmentCreate(): void {
    this.salesAdjustmentEditingId.set(null);
    this.salesAdjustmentForm.reset({
      adjustmentType: 'Return',
      transactionDate: this.todayIso(),
      relatedInvoiceId: '',
      customerId: '',
      currency: 'MVR',
      amountOriginal: 0,
      exchangeRate: null,
      approvalStatus: 'Approved',
      notes: ''
    });
    this.salesAdjustmentEditorOpen.set(true);
  }

  openSalesAdjustmentEdit(row: SalesAdjustmentRecord): void {
    this.salesAdjustmentEditingId.set(row.id);
    this.salesAdjustmentForm.reset({
      adjustmentType: row.adjustmentType,
      transactionDate: row.transactionDate,
      relatedInvoiceId: row.relatedInvoiceId ?? '',
      customerId: row.customerId ?? '',
      currency: row.currency,
      amountOriginal: row.amountOriginal,
      exchangeRate: row.currency === 'MVR' ? null : row.exchangeRate,
      approvalStatus: row.approvalStatus,
      notes: row.notes ?? ''
    });
    this.salesAdjustmentEditorOpen.set(true);
  }

  closeSalesAdjustmentEditor(): void {
    if (this.salesAdjustmentSaving()) {
      return;
    }

    this.salesAdjustmentEditorOpen.set(false);
    this.salesAdjustmentEditingId.set(null);
  }

  saveSalesAdjustment(): void {
    if (this.salesAdjustmentForm.invalid || this.salesAdjustmentSaving()) {
      this.salesAdjustmentForm.markAllAsTouched();
      return;
    }

    const raw = this.salesAdjustmentForm.getRawValue();
    const payload = {
      adjustmentType: raw.adjustmentType as SalesAdjustmentType,
      transactionDate: raw.transactionDate ?? this.todayIso(),
      relatedInvoiceId: this.normalizeOptionalId(raw.relatedInvoiceId),
      customerId: this.normalizeOptionalId(raw.customerId),
      currency: (raw.currency ?? 'MVR').trim().toUpperCase(),
      amountOriginal: Number(raw.amountOriginal ?? 0),
      exchangeRate: this.normalizeOptionalNumber(raw.exchangeRate),
      approvalStatus: raw.approvalStatus as ApprovalStatus,
      notes: raw.notes?.trim() || undefined
    };

    this.salesAdjustmentSaving.set(true);
    const request$ = this.salesAdjustmentEditingId()
      ? this.api.updateBptSalesAdjustment(this.salesAdjustmentEditingId()!, payload)
      : this.api.createBptSalesAdjustment(payload);

    request$
      .pipe(finalize(() => this.salesAdjustmentSaving.set(false)))
      .subscribe({
        next: () => {
          this.toast.success(this.salesAdjustmentEditingId() ? 'Sales adjustment updated.' : 'Sales adjustment created.');
          this.closeSalesAdjustmentEditor();
          this.loadSalesAdjustments();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Failed to save sales adjustment.'))
      });
  }

  openOtherIncomeCreate(): void {
    this.otherIncomeEditingId.set(null);
    this.otherIncomeForm.reset({
      transactionDate: this.todayIso(),
      customerId: '',
      counterpartyName: '',
      description: '',
      currency: 'MVR',
      amountOriginal: 0,
      exchangeRate: null,
      approvalStatus: 'Approved',
      notes: ''
    });
    this.otherIncomeEditorOpen.set(true);
  }

  openOtherIncomeEdit(row: OtherIncomeEntryRecord): void {
    this.otherIncomeEditingId.set(row.id);
    this.otherIncomeForm.reset({
      transactionDate: row.transactionDate,
      customerId: row.customerId ?? '',
      counterpartyName: row.counterpartyName ?? '',
      description: row.description,
      currency: row.currency,
      amountOriginal: row.amountOriginal,
      exchangeRate: row.currency === 'MVR' ? null : row.exchangeRate,
      approvalStatus: row.approvalStatus,
      notes: row.notes ?? ''
    });
    this.otherIncomeEditorOpen.set(true);
  }

  closeOtherIncomeEditor(): void {
    if (this.otherIncomeSaving()) {
      return;
    }

    this.otherIncomeEditorOpen.set(false);
    this.otherIncomeEditingId.set(null);
  }

  saveOtherIncome(): void {
    if (this.otherIncomeForm.invalid || this.otherIncomeSaving()) {
      this.otherIncomeForm.markAllAsTouched();
      return;
    }

    const raw = this.otherIncomeForm.getRawValue();
    const payload = {
      transactionDate: raw.transactionDate ?? this.todayIso(),
      customerId: this.normalizeOptionalId(raw.customerId),
      counterpartyName: raw.counterpartyName?.trim() || undefined,
      description: raw.description?.trim() || '',
      currency: (raw.currency ?? 'MVR').trim().toUpperCase(),
      amountOriginal: Number(raw.amountOriginal ?? 0),
      exchangeRate: this.normalizeOptionalNumber(raw.exchangeRate),
      approvalStatus: raw.approvalStatus as ApprovalStatus,
      notes: raw.notes?.trim() || undefined
    };

    this.otherIncomeSaving.set(true);
    const request$ = this.otherIncomeEditingId()
      ? this.api.updateBptOtherIncome(this.otherIncomeEditingId()!, payload)
      : this.api.createBptOtherIncome(payload);

    request$
      .pipe(finalize(() => this.otherIncomeSaving.set(false)))
      .subscribe({
        next: () => {
          this.toast.success(this.otherIncomeEditingId() ? 'Other income updated.' : 'Other income created.');
          this.closeOtherIncomeEditor();
          this.loadOtherIncome();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Failed to save other income.'))
      });
  }

  openAdjustmentCreate(): void {
    this.adjustmentEditingId.set(null);
    this.adjustmentForm.reset({
      transactionDate: this.todayIso(),
      description: '',
      bptCategoryId: '',
      currency: 'MVR',
      amountOriginal: 0,
      exchangeRate: null,
      approvalStatus: 'Approved',
      notes: ''
    });
    this.adjustmentEditorOpen.set(true);
  }

  openAdjustmentEdit(row: BptAdjustmentRecord): void {
    this.adjustmentEditingId.set(row.id);
    this.adjustmentForm.reset({
      transactionDate: row.transactionDate,
      description: row.description,
      bptCategoryId: row.bptCategoryId,
      currency: row.currency,
      amountOriginal: row.amountOriginal,
      exchangeRate: row.currency === 'MVR' ? null : row.exchangeRate,
      approvalStatus: row.approvalStatus,
      notes: row.notes ?? ''
    });
    this.adjustmentEditorOpen.set(true);
  }

  closeAdjustmentEditor(): void {
    if (this.adjustmentSaving()) {
      return;
    }

    this.adjustmentEditorOpen.set(false);
    this.adjustmentEditingId.set(null);
  }

  saveAdjustment(): void {
    if (this.adjustmentForm.invalid || this.adjustmentSaving()) {
      this.adjustmentForm.markAllAsTouched();
      return;
    }

    const raw = this.adjustmentForm.getRawValue();
    const payload = {
      transactionDate: raw.transactionDate ?? this.todayIso(),
      description: raw.description?.trim() || '',
      bptCategoryId: raw.bptCategoryId ?? '',
      currency: (raw.currency ?? 'MVR').trim().toUpperCase(),
      amountOriginal: Number(raw.amountOriginal ?? 0),
      exchangeRate: this.normalizeOptionalNumber(raw.exchangeRate),
      approvalStatus: raw.approvalStatus as ApprovalStatus,
      notes: raw.notes?.trim() || undefined
    };

    this.adjustmentSaving.set(true);
    const request$ = this.adjustmentEditingId()
      ? this.api.updateBptAdjustment(this.adjustmentEditingId()!, payload)
      : this.api.createBptAdjustment(payload);

    request$
      .pipe(finalize(() => this.adjustmentSaving.set(false)))
      .subscribe({
        next: () => {
          this.toast.success(this.adjustmentEditingId() ? 'BPT adjustment updated.' : 'BPT adjustment created.');
          this.closeAdjustmentEditor();
          this.loadAdjustments();
        },
        error: (error) => this.toast.error(extractApiError(error, 'Failed to save BPT adjustment.'))
      });
  }

  confirmDelete(kind: DeleteIntentKind, id: string, label: string): void {
    this.deleteIntent.set({ kind, id, label });
    this.deleteDialogOpen.set(true);
  }

  closeDeleteDialog(): void {
    this.deleteDialogOpen.set(false);
    this.deleteIntent.set(null);
  }

  deleteDialogTitle(): string {
    const intent = this.deleteIntent();
    if (!intent) {
      return 'Delete record';
    }

    switch (intent.kind) {
      case 'exchangeRate':
        return 'Delete exchange rate';
      case 'salesAdjustment':
        return 'Delete sales adjustment';
      case 'otherIncome':
        return 'Delete other income';
      case 'adjustment':
        return 'Delete BPT adjustment';
      default:
        return 'Delete record';
    }
  }

  deleteDialogMessage(): string {
    const intent = this.deleteIntent();
    if (!intent) {
      return 'This action cannot be undone.';
    }

    return `Delete ${intent.label}? This action cannot be undone.`;
  }

  runDelete(): void {
    const intent = this.deleteIntent();
    if (!intent) {
      return;
    }

    const request$ = (() => {
      switch (intent.kind) {
        case 'exchangeRate':
          return this.api.deleteBptExchangeRate(intent.id);
        case 'salesAdjustment':
          return this.api.deleteBptSalesAdjustment(intent.id);
        case 'otherIncome':
          return this.api.deleteBptOtherIncome(intent.id);
        case 'adjustment':
          return this.api.deleteBptAdjustment(intent.id);
      }
    })();

    request$.subscribe({
      next: () => {
        this.toast.success('Record deleted.');
        this.closeDeleteDialog();

        switch (intent.kind) {
          case 'exchangeRate':
            this.loadExchangeRates();
            break;
          case 'salesAdjustment':
            this.loadSalesAdjustments();
            break;
          case 'otherIncome':
            this.loadOtherIncome();
            break;
          case 'adjustment':
            this.loadAdjustments();
            break;
        }
      },
      error: (error) => {
        this.toast.error(extractApiError(error, 'Failed to delete record.'));
        this.closeDeleteDialog();
      }
    });
  }

  private loadStaticLookups(): void {
    this.api.getBptCategories().subscribe({
      next: (items) => this.categories.set(items),
      error: (error) => this.toast.error(extractApiError(error, 'Failed to load BPT categories.'))
    });

    this.loadMappings();

    this.api.getCustomers({ pageNumber: 1, pageSize: 500, sortDirection: 'asc' }).subscribe({
      next: (result) => this.customers.set(result.items),
      error: (error) => this.toast.error(extractApiError(error, 'Failed to load customers for BPT forms.'))
    });

    this.api.getInvoices({ pageNumber: 1, pageSize: 300, sortDirection: 'desc' }).subscribe({
      next: (result) => this.invoices.set(result.items),
      error: (error) => this.toast.error(extractApiError(error, 'Failed to load invoices for BPT forms.'))
    });
  }

  private refreshManagementData(): void {
    this.loadMappings();
    this.loadExchangeRates();
    this.loadSalesAdjustments();
    this.loadOtherIncome();
    this.loadAdjustments();
  }

  private loadMappings(): void {
    this.api.getBptMappings().subscribe({
      next: (items) => this.mappings.set(items),
      error: (error) => this.toast.error(extractApiError(error, 'Failed to load BPT mappings.'))
    });
  }

  private loadExchangeRates(): void {
    const range = this.resolveSelectedRange();
    this.api.getBptExchangeRates({ dateFrom: range.start, dateTo: range.end }).subscribe({
      next: (items) => {
        const sorted = [...items].sort((left, right) => right.rateDate.localeCompare(left.rateDate));
        this.exchangeRates.set(sorted);
      },
      error: (error) => this.toast.error(extractApiError(error, 'Failed to load exchange rates.'))
    });
  }

  private loadSalesAdjustments(): void {
    const range = this.resolveSelectedRange();
    this.api.getBptSalesAdjustments({ dateFrom: range.start, dateTo: range.end }).subscribe({
      next: (items) => this.salesAdjustments.set(items),
      error: (error) => this.toast.error(extractApiError(error, 'Failed to load sales adjustments.'))
    });
  }

  private loadOtherIncome(): void {
    const range = this.resolveSelectedRange();
    this.api.getBptOtherIncome({ dateFrom: range.start, dateTo: range.end }).subscribe({
      next: (items) => this.otherIncomeEntries.set(items),
      error: (error) => this.toast.error(extractApiError(error, 'Failed to load other income.'))
    });
  }

  private loadAdjustments(): void {
    const range = this.resolveSelectedRange();
    this.api.getBptAdjustments({ dateFrom: range.start, dateTo: range.end }).subscribe({
      next: (items) => this.adjustments.set(items),
      error: (error) => this.toast.error(extractApiError(error, 'Failed to load BPT adjustments.'))
    });
  }

  private validateFilters(): boolean {
    this.validationError.set(null);

    if (this.selectedPeriodMode() !== BptPeriodMode.CustomRange) {
      return true;
    }

    const start = this.customStartDate();
    const end = this.customEndDate();
    if (!start || !end) {
      this.validationError.set('Custom range requires both start and end dates.');
      return false;
    }

    if (Date.parse(`${end}T00:00:00Z`) < Date.parse(`${start}T00:00:00Z`)) {
      this.validationError.set('End date cannot be earlier than start date.');
      return false;
    }

    return true;
  }

  private buildReportParams(): Record<string, unknown> {
    const params: Record<string, unknown> = {
      periodMode: this.selectedPeriodMode(),
      year: this.selectedYear()
    };

    if (this.selectedPeriodMode() === BptPeriodMode.Quarter) {
      params['quarter'] = this.selectedQuarter();
    }

    if (this.selectedPeriodMode() === BptPeriodMode.CustomRange) {
      params['customStartDate'] = this.customStartDate();
      params['customEndDate'] = this.customEndDate();
    }

    return params;
  }

  private resolveSelectedRange(): { start: string; end: string } {
    const year = this.selectedYear();

    if (this.selectedPeriodMode() === BptPeriodMode.Year) {
      return { start: `${year}-01-01`, end: `${year}-12-31` };
    }

    if (this.selectedPeriodMode() === BptPeriodMode.CustomRange) {
      return {
        start: this.customStartDate() || this.todayIso(),
        end: this.customEndDate() || this.todayIso()
      };
    }

    const quarter = this.selectedQuarter();
    const startMonth = (quarter - 1) * 3;
    const startDate = new Date(Date.UTC(year, startMonth, 1));
    const endDate = new Date(Date.UTC(year, startMonth + 3, 0));

    return {
      start: this.toIsoDate(startDate),
      end: this.toIsoDate(endDate)
    };
  }

  private normalizeOptionalId(value: string | null | undefined): string | undefined {
    const trimmed = (value ?? '').trim();
    if (!trimmed || trimmed === '00000000-0000-0000-0000-000000000000') {
      return undefined;
    }

    return trimmed;
  }

  private normalizeLookupId(value: string | null | undefined): string {
    return this.normalizeOptionalId(value) ?? '';
  }

  private normalizeOptionalNumber(value: unknown): number | undefined {
    if (value === null || value === undefined || value === '') {
      return undefined;
    }

    const numeric = Number(value);
    return Number.isFinite(numeric) && numeric > 0 ? numeric : undefined;
  }

  private currentQuarter(): number {
    return Math.floor(new Date().getMonth() / 3) + 1;
  }

  private todayIso(): string {
    return this.toIsoDate(new Date());
  }

  private toIsoDate(date: Date): string {
    return date.toISOString().slice(0, 10);
  }

  private round2(value: number): number {
    return Math.round((value + Number.EPSILON) * 100) / 100;
  }

  private download(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
