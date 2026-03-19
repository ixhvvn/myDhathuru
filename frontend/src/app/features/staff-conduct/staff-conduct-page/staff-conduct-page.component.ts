import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, map, switchMap } from 'rxjs';
import {
  PagedResult,
  StaffConductDetail,
  StaffConductDhivehiExport,
  StaffConductFormType,
  StaffConductListItem,
  StaffConductSeverity,
  StaffConductStaffOption,
  StaffConductStatus,
  StaffConductSummary
} from '../../../core/models/app.models';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { setAppScrollLock } from '../../../core/utils/app-scroll-lock.util';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { PortalApiService } from '../../services/portal-api.service';

type DrawerMode = 'create' | 'edit' | 'view' | null;
type ExportFormat = 'pdf' | 'excel';

@Component({
  selector: 'app-staff-conduct-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppButtonComponent,
    AppCardComponent,
    AppDataTableComponent,
    AppPageHeaderComponent
  ],
  templateUrl: './staff-conduct-page.component.html',
  styleUrls: ['./staff-conduct-page.component.scss']
})
export class StaffConductPageComponent implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly scrollLockOwner = {};

  readonly page = signal<PagedResult<StaffConductListItem> | null>(null);
  readonly summary = signal<StaffConductSummary>({ totalForms: 0, warningCount: 0, disciplinaryCount: 0, openCount: 0, acknowledgedCount: 0, resolvedCount: 0 });
  readonly staffOptions = signal<StaffConductStaffOption[]>([]);
  readonly selectedDetail = signal<StaffConductDetail | null>(null);
  readonly drawerMode = signal<DrawerMode>(null);
  readonly listLoading = signal(false);
  readonly detailLoading = signal(false);
  readonly saveLoading = signal(false);
  readonly summaryExportLoading = signal<ExportFormat | null>(null);
  readonly detailExportLoading = signal<ExportFormat | null>(null);
  readonly rowExportKey = signal<string | null>(null);
  readonly exportTarget = signal<StaffConductDetail | null>(null);
  readonly exportChoiceOpen = signal(false);
  readonly exportChoiceEnglishLoading = signal(false);
  readonly dhivehiExport = signal<StaffConductDhivehiExport | null>(null);
  readonly dhivehiEditorOpen = signal(false);
  readonly dhivehiLoading = signal(false);
  readonly dhivehiSaveLoading = signal(false);
  readonly dhivehiGenerateLoading = signal(false);
  readonly dhivehiDownloadLoading = signal(false);
  readonly pageNumber = signal(1);
  readonly pageSize = 10;
  readonly formTypeOptions: StaffConductFormType[] = ['Warning', 'Disciplinary'];
  readonly severityOptions: StaffConductSeverity[] = ['Low', 'Medium', 'High', 'Critical'];
  readonly statusOptions: StaffConductStatus[] = ['Open', 'Acknowledged', 'Resolved'];
  readonly isAdmin = computed(() => this.authService.user()?.role === 'Admin');
  readonly busy = computed(() =>
    this.listLoading()
    || this.detailLoading()
    || this.saveLoading()
    || this.summaryExportLoading() !== null
    || this.detailExportLoading() !== null
    || this.rowExportKey() !== null
    || this.exportChoiceEnglishLoading()
    || this.dhivehiLoading()
    || this.dhivehiSaveLoading()
    || this.dhivehiGenerateLoading()
    || this.dhivehiDownloadLoading());
  readonly pageSummary = computed(() => {
    const current = this.page();
    if (!current) {
      return 'Loading conduct register...';
    }

    return `Showing ${current.items.length} form${current.items.length === 1 ? '' : 's'} on this page.`;
  });
  readonly exportChoiceLabel = computed(() => {
    const target = this.exportTarget();
    return target ? `${target.formNumber} · ${target.formType}` : 'PDF Export';
  });
  readonly dhivehiExportStatus = computed(() => {
    const model = this.dhivehiExport();
    if (!model) {
      return '';
    }

    if (model.hasSavedPdf && model.isSavedPdfStale) {
      return 'Saved PDF is out of date';
    }

    if (model.hasSavedPdf) {
      return 'Saved PDF ready';
    }

    return model.hasDhivehiContent ? 'Dhivehi content saved' : 'Dhivehi content not prepared';
  });

  readonly filtersForm = this.formBuilder.group({
    search: [''],
    staffId: [''],
    formType: [''],
    status: [''],
    dateFrom: [''],
    dateTo: ['']
  });

  readonly form = this.formBuilder.group({
    staffId: ['', Validators.required],
    formType: ['Warning' as StaffConductFormType, Validators.required],
    issueDate: [this.todayIso(), Validators.required],
    incidentDate: [this.todayIso(), Validators.required],
    subject: ['', Validators.required],
    severity: ['Low' as StaffConductSeverity, Validators.required],
    status: ['Open' as StaffConductStatus, Validators.required],
    issuedBy: ['', Validators.required],
    witnessedBy: [''],
    incidentDetails: ['', Validators.required],
    actionTaken: ['', Validators.required],
    requiredImprovement: [''],
    followUpDate: [''],
    isAcknowledgedByStaff: [false],
    acknowledgedDate: [''],
    employeeRemarks: [''],
    resolutionNotes: [''],
    resolvedDate: ['']
  });

  readonly dhivehiForm = this.formBuilder.group({
    subjectDv: ['', Validators.maxLength(200)],
    incidentDetailsDv: ['', Validators.maxLength(2000)],
    actionTakenDv: ['', Validators.maxLength(1000)],
    requiredImprovementDv: ['', Validators.maxLength(1000)],
    employeeRemarksDv: ['', Validators.maxLength(1000)],
    acknowledgementDv: ['', Validators.maxLength(1000)],
    resolutionNotesDv: ['', Validators.maxLength(1000)]
  });
  private readonly overlayScrollLockEffect = effect((onCleanup) => {
    const overlayOpen = this.drawerMode() !== null || this.exportChoiceOpen() || this.dhivehiEditorOpen();
    setAppScrollLock(this.scrollLockOwner, overlayOpen);
    onCleanup(() => setAppScrollLock(this.scrollLockOwner, false));
  });

  ngOnInit(): void {
    this.loadStaffOptions();
    this.applyFilters();
  }

  typePillClass(value: StaffConductFormType): string {
    return value === 'Warning' ? 'pill-warning' : 'pill-disciplinary';
  }

  statusPillClass(value: StaffConductStatus): string {
    switch (value) {
      case 'Acknowledged':
        return 'pill-acknowledged';
      case 'Resolved':
        return 'pill-resolved';
      default:
        return 'pill-open';
    }
  }

  detailDhivehiStatus(detail: StaffConductDetail): string {
    if (detail.hasSavedDhivehiPdf && detail.isSavedDhivehiPdfStale) {
      return 'Saved PDF needs regeneration';
    }

    if (detail.hasSavedDhivehiPdf) {
      return 'Saved PDF available';
    }

    return detail.hasDhivehiContent ? 'Dhivehi content saved' : 'Not prepared yet';
  }

  detailDhivehiNote(detail: StaffConductDetail): string {
    if (detail.hasSavedDhivehiPdf) {
      const filePart = detail.dhivehiPdfFileName ? `${detail.dhivehiPdfFileName}. ` : '';
      return detail.isSavedDhivehiPdfStale
        ? `${filePart}Regenerate after editing the Dhivehi content or the English source form.`
        : `${filePart}Download the stored Dhivehi PDF directly any time.`;
    }

    return detail.hasDhivehiContent
      ? 'Dhivehi text is saved. Generate the PDF once to store it for later downloads.'
      : 'Prepare the Dhivehi version once and the generated PDF will be stored for later download.';
  }

  exportKey(id: string, format: ExportFormat): string {
    return `${id}:${format}`;
  }

  busyExcept(id: string, format: ExportFormat): boolean {
    const key = this.exportKey(id, format);
    return this.busy() && this.rowExportKey() !== key;
  }

  applyFilters(): void {
    this.pageNumber.set(1);
    this.loadList();
    this.loadSummary();
  }

  resetFilters(): void {
    this.filtersForm.reset({ search: '', staffId: '', formType: '', status: '', dateFrom: '', dateTo: '' });
    this.applyFilters();
  }

  changePage(pageNumber: number): void {
    if (pageNumber < 1 || this.listLoading()) {
      return;
    }

    this.pageNumber.set(pageNumber);
    this.loadList();
  }

  openCreate(defaultType: StaffConductFormType): void {
    this.selectedDetail.set(null);
    this.form.reset({
      staffId: '',
      formType: defaultType,
      issueDate: this.todayIso(),
      incidentDate: this.todayIso(),
      subject: '',
      severity: 'Low',
      status: 'Open',
      issuedBy: '',
      witnessedBy: '',
      incidentDetails: '',
      actionTaken: '',
      requiredImprovement: '',
      followUpDate: '',
      isAcknowledgedByStaff: false,
      acknowledgedDate: '',
      employeeRemarks: '',
      resolutionNotes: '',
      resolvedDate: ''
    });
    this.drawerMode.set('create');
  }

  openView(item: StaffConductListItem): void {
    this.drawerMode.set('view');
    this.loadDetail(item.id);
  }

  openEdit(item: StaffConductListItem): void {
    if (!this.isAdmin()) {
      return;
    }

    this.drawerMode.set('edit');
    this.loadDetail(item.id, true);
  }

  editSelected(): void {
    const detail = this.selectedDetail();
    if (!detail || !this.isAdmin()) {
      return;
    }

    this.patchForm(detail);
    this.drawerMode.set('edit');
  }

  closeDrawer(): void {
    if (this.saveLoading()) {
      return;
    }

    this.drawerMode.set(null);
    this.detailLoading.set(false);
    this.detailExportLoading.set(null);
  }

  closeTransientPanels(): void {
    if (this.dhivehiEditorOpen()) {
      this.closeDhivehiEditor();
      return;
    }

    this.closeExportChoice();
  }

  closeExportChoice(): void {
    if (this.exportChoiceEnglishLoading() || this.dhivehiLoading()) {
      return;
    }

    this.exportChoiceOpen.set(false);
    this.exportTarget.set(null);
  }

  closeDhivehiEditor(): void {
    if (this.dhivehiSaveLoading() || this.dhivehiGenerateLoading() || this.dhivehiDownloadLoading()) {
      return;
    }

    this.dhivehiEditorOpen.set(false);
    this.dhivehiExport.set(null);
    this.exportTarget.set(null);
    this.dhivehiForm.reset({
      subjectDv: '',
      incidentDetailsDv: '',
      actionTakenDv: '',
      requiredImprovementDv: '',
      employeeRemarksDv: '',
      acknowledgementDv: '',
      resolutionNotesDv: ''
    });
  }

  saveForm(): void {
    if (!this.isAdmin()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload = this.buildFormPayload();
    const editingId = this.selectedDetail()?.id;
    const request = this.drawerMode() === 'edit' && editingId
      ? this.api.updateStaffConductForm(editingId, payload)
      : this.api.createStaffConductForm(payload);
    const successMessage = this.drawerMode() === 'edit' ? 'Form updated.' : 'Form created.';

    this.saveLoading.set(true);
    request.pipe(finalize(() => this.saveLoading.set(false))).subscribe({
      next: (detail) => {
        this.selectedDetail.set(detail);
        this.drawerMode.set('view');
        this.toast.success(successMessage);
        this.loadList();
        this.loadSummary();
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to save form.'))
    });
  }

  exportSummary(format: ExportFormat): void {
    if (this.busy()) {
      return;
    }

    const request = format === 'excel'
      ? this.api.exportStaffConductSummaryExcel(this.buildQueryParams(false))
      : this.api.exportStaffConductSummaryPdf(this.buildQueryParams(false));

    this.summaryExportLoading.set(format);
    request.pipe(finalize(() => this.summaryExportLoading.set(null))).subscribe({
      next: (blob) => {
        this.download(blob, `staff-conduct-summary-${this.todayIso()}.${format === 'excel' ? 'xlsx' : 'pdf'}`);
        this.toast.success(`Summary exported to ${format.toUpperCase()}.`);
      },
      error: (error) => this.toast.error(extractApiError(error, `Failed to export ${format.toUpperCase()} summary.`))
    });
  }

  exportSingle(item: StaffConductListItem, format: ExportFormat): void {
    if (format === 'excel') {
      this.exportExcelFromRow(item);
      return;
    }

    this.openExportChoiceFor(item);
  }

  exportDetailFromDrawer(format: ExportFormat): void {
    if (format === 'excel') {
      this.exportExcelFromDrawer();
      return;
    }

    this.downloadEnglishPdfFromDrawer();
  }

  openExportChoiceFor(item: StaffConductListItem | StaffConductDetail): void {
    if (this.busy()) {
      return;
    }

    if (this.isDetail(item)) {
      this.exportTarget.set(item);
      this.exportChoiceOpen.set(true);
      return;
    }

    const current = this.selectedDetail();
    if (current && current.id === item.id) {
      this.exportTarget.set(current);
      this.exportChoiceOpen.set(true);
      return;
    }

    const key = this.exportKey(item.id, 'pdf');
    this.rowExportKey.set(key);
    this.api.getStaffConductFormById(item.id).pipe(finalize(() => this.rowExportKey.set(null))).subscribe({
      next: (detail) => {
        this.exportTarget.set(detail);
        this.exportChoiceOpen.set(true);
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load the form for export.'))
    });
  }

  downloadEnglishPdfFromChoice(): void {
    const target = this.exportTarget();
    if (!target || this.exportChoiceEnglishLoading()) {
      return;
    }

    this.exportChoiceEnglishLoading.set(true);
    this.api.exportStaffConductFormPdf(target.id).pipe(finalize(() => this.exportChoiceEnglishLoading.set(false))).subscribe({
      next: (blob) => {
        this.download(blob, `${target.formNumber}.pdf`);
        this.toast.success(`${target.formNumber} exported to PDF.`);
        if (this.exportChoiceOpen()) {
          this.closeExportChoice();
        }
      },
      error: (error) => this.toast.error(extractApiError(error, `Failed to export ${target.formNumber}.`))
    });
  }

  prepareDhivehiExportFromChoice(): void {
    const target = this.exportTarget();
    if (!target) {
      return;
    }

    if (!this.isAdmin()) {
      this.downloadSavedDhivehiPdf(target.id, target.dhivehiPdfFileName ?? `${target.formNumber}-dhivehi.pdf`, target.formNumber);
      return;
    }

    this.loadDhivehiExport(target.id, target);
  }

  prepareDhivehiExportFromDetail(): void {
    const detail = this.selectedDetail();
    if (!detail || !this.isAdmin()) {
      return;
    }

    this.loadDhivehiExport(detail.id, detail);
  }

  saveDhivehiContent(): void {
    const model = this.dhivehiExport();
    if (!model || this.dhivehiForm.invalid) {
      this.dhivehiForm.markAllAsTouched();
      return;
    }

    this.dhivehiSaveLoading.set(true);
    this.api.saveStaffConductDhivehiExport(model.formId, this.buildDhivehiPayload())
      .pipe(finalize(() => this.dhivehiSaveLoading.set(false)))
      .subscribe({
        next: (saved) => {
          this.applyDhivehiExport(saved);
          this.refreshDetailSnapshots(model.formId);
          this.toast.success('Dhivehi export content saved.');
        },
        error: (error) => this.toast.error(extractApiError(error, 'Unable to save Dhivehi export content.'))
      });
  }

  generateDhivehiPdf(): void {
    const model = this.dhivehiExport();
    if (!model || this.dhivehiForm.invalid) {
      this.dhivehiForm.markAllAsTouched();
      return;
    }

    const fileName = model.savedPdfFileName || `${model.formNumber}-dhivehi.pdf`;
    this.dhivehiGenerateLoading.set(true);
    this.api.saveStaffConductDhivehiExport(model.formId, this.buildDhivehiPayload()).pipe(
      map((saved) => {
        this.applyDhivehiExport(saved);
        return saved;
      }),
      switchMap((saved) => this.api.generateStaffConductDhivehiPdf(saved.formId).pipe(map((blob) => ({ blob, formId: saved.formId, fileName })))),
      switchMap((result) => this.api.getStaffConductDhivehiExport(result.formId).pipe(map((updated) => ({ ...result, updated })))),
      finalize(() => this.dhivehiGenerateLoading.set(false))
    ).subscribe({
      next: ({ blob, updated, fileName: resolvedName }) => {
        this.applyDhivehiExport(updated);
        this.refreshDetailSnapshots(updated.formId);
        this.download(blob, updated.savedPdfFileName || resolvedName);
        this.toast.success('Dhivehi PDF generated and saved.');
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to generate the Dhivehi PDF.'))
    });
  }

  downloadSavedDhivehiPdfFromChoice(): void {
    const target = this.exportTarget();
    if (!target) {
      return;
    }

    this.downloadSavedDhivehiPdf(target.id, target.dhivehiPdfFileName ?? `${target.formNumber}-dhivehi.pdf`, target.formNumber, () => this.closeExportChoice());
  }

  downloadSavedDhivehiPdfFromDetail(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.downloadSavedDhivehiPdf(detail.id, detail.dhivehiPdfFileName ?? `${detail.formNumber}-dhivehi.pdf`, detail.formNumber);
  }

  downloadSavedDhivehiPdfFromEditor(): void {
    const model = this.dhivehiExport();
    if (!model) {
      return;
    }

    this.downloadSavedDhivehiPdf(model.formId, model.savedPdfFileName ?? `${model.formNumber}-dhivehi.pdf`, model.formNumber);
  }

  showError(controlName: string, errorCode: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.touched && control.hasError(errorCode);
  }

  private isDetail(value: StaffConductListItem | StaffConductDetail): value is StaffConductDetail {
    return 'incidentDetails' in value;
  }

  private loadStaffOptions(): void {
    this.api.getStaffConductStaffOptions().subscribe({
      next: (items) => this.staffOptions.set(items),
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load staff list.'))
    });
  }

  private loadList(): void {
    this.listLoading.set(true);
    this.api.getStaffConductForms(this.buildQueryParams(true)).pipe(finalize(() => this.listLoading.set(false))).subscribe({
      next: (page) => this.page.set(page),
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load staff conduct forms.'))
    });
  }

  private loadSummary(): void {
    this.api.getStaffConductSummary(this.buildQueryParams(false)).subscribe({
      next: (summary) => this.summary.set(summary),
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load conduct summary.'))
    });
  }

  private loadDetail(id: string, patchForEdit = false): void {
    this.detailLoading.set(true);
    this.selectedDetail.set(null);
    this.api.getStaffConductFormById(id).pipe(finalize(() => this.detailLoading.set(false))).subscribe({
      next: (detail) => {
        this.selectedDetail.set(detail);
        if (patchForEdit) {
          this.patchForm(detail);
        }
      },
      error: (error) => {
        this.toast.error(extractApiError(error, 'Unable to load form details.'));
        this.closeDrawer();
      }
    });
  }

  private loadDhivehiExport(formId: string, target: StaffConductDetail): void {
    this.dhivehiLoading.set(true);
    this.exportTarget.set(target);
    this.api.getStaffConductDhivehiExport(formId).pipe(finalize(() => this.dhivehiLoading.set(false))).subscribe({
      next: (model) => {
        this.applyDhivehiExport(model);
        this.exportChoiceOpen.set(false);
        this.dhivehiEditorOpen.set(true);
      },
      error: (error) => this.toast.error(extractApiError(error, 'Unable to load the Dhivehi export form.'))
    });
  }

  private applyDhivehiExport(model: StaffConductDhivehiExport): void {
    this.dhivehiExport.set(model);
    this.dhivehiForm.reset({
      subjectDv: model.subjectDv ?? '',
      incidentDetailsDv: model.incidentDetailsDv ?? '',
      actionTakenDv: model.actionTakenDv ?? '',
      requiredImprovementDv: model.requiredImprovementDv ?? '',
      employeeRemarksDv: model.employeeRemarksDv ?? '',
      acknowledgementDv: model.acknowledgementDv ?? '',
      resolutionNotesDv: model.resolutionNotesDv ?? ''
    });
  }

  private refreshDetailSnapshots(formId: string): void {
    this.api.getStaffConductFormById(formId).subscribe({
      next: (detail) => {
        if (this.selectedDetail()?.id === formId) {
          this.selectedDetail.set(detail);
        }

        if (this.exportTarget()?.id === formId) {
          this.exportTarget.set(detail);
        }
      }
    });
  }

  private exportExcelFromRow(item: StaffConductListItem): void {
    if (this.busy()) {
      return;
    }

    const key = this.exportKey(item.id, 'excel');
    this.rowExportKey.set(key);
    this.api.exportStaffConductFormExcel(item.id).pipe(finalize(() => this.rowExportKey.set(null))).subscribe({
      next: (blob) => {
        this.download(blob, `${item.formNumber}.xlsx`);
        this.toast.success(`${item.formNumber} exported to EXCEL.`);
      },
      error: (error) => this.toast.error(extractApiError(error, `Failed to export ${item.formNumber}.`))
    });
  }

  private exportExcelFromDrawer(): void {
    const detail = this.selectedDetail();
    if (!detail || this.busy()) {
      return;
    }

    this.detailExportLoading.set('excel');
    this.api.exportStaffConductFormExcel(detail.id).pipe(finalize(() => this.detailExportLoading.set(null))).subscribe({
      next: (blob) => {
        this.download(blob, `${detail.formNumber}.xlsx`);
        this.toast.success(`${detail.formNumber} exported to EXCEL.`);
      },
      error: (error) => this.toast.error(extractApiError(error, `Failed to export ${detail.formNumber}.`))
    });
  }

  private downloadEnglishPdfFromDrawer(): void {
    const detail = this.selectedDetail();
    if (!detail || this.busy()) {
      return;
    }

    this.detailExportLoading.set('pdf');
    this.api.exportStaffConductFormPdf(detail.id).pipe(finalize(() => this.detailExportLoading.set(null))).subscribe({
      next: (blob) => {
        this.download(blob, `${detail.formNumber}.pdf`);
        this.toast.success(`${detail.formNumber} exported to PDF.`);
      },
      error: (error) => this.toast.error(extractApiError(error, `Failed to export ${detail.formNumber}.`))
    });
  }

  private patchForm(detail: StaffConductDetail): void {
    this.form.reset({
      staffId: detail.staffId,
      formType: detail.formType,
      issueDate: detail.issueDate,
      incidentDate: detail.incidentDate,
      subject: detail.subject,
      severity: detail.severity,
      status: detail.status,
      issuedBy: detail.issuedBy,
      witnessedBy: detail.witnessedBy ?? '',
      incidentDetails: detail.incidentDetails,
      actionTaken: detail.actionTaken,
      requiredImprovement: detail.requiredImprovement ?? '',
      followUpDate: detail.followUpDate ?? '',
      isAcknowledgedByStaff: detail.isAcknowledgedByStaff,
      acknowledgedDate: detail.acknowledgedDate ?? '',
      employeeRemarks: detail.employeeRemarks ?? '',
      resolutionNotes: detail.resolutionNotes ?? '',
      resolvedDate: detail.resolvedDate ?? ''
    });
  }

  private buildQueryParams(includePagination: boolean): Record<string, unknown> {
    const raw = this.filtersForm.getRawValue();
    const params: Record<string, unknown> = {};
    if (raw.search?.trim()) { params['search'] = raw.search.trim(); }
    if (raw.staffId) { params['staffId'] = raw.staffId; }
    if (raw.formType) { params['formType'] = raw.formType; }
    if (raw.status) { params['status'] = raw.status; }
    if (raw.dateFrom) { params['dateFrom'] = raw.dateFrom; }
    if (raw.dateTo) { params['dateTo'] = raw.dateTo; }
    if (includePagination) {
      params['pageNumber'] = this.pageNumber();
      params['pageSize'] = this.pageSize;
    }
    return params;
  }

  private buildFormPayload(): Record<string, unknown> {
    const raw = this.form.getRawValue();
    return {
      staffId: raw.staffId,
      formType: raw.formType,
      issueDate: raw.issueDate,
      incidentDate: raw.incidentDate,
      subject: raw.subject?.trim(),
      severity: raw.severity,
      status: raw.status,
      issuedBy: raw.issuedBy?.trim(),
      witnessedBy: this.normalizeString(raw.witnessedBy),
      incidentDetails: raw.incidentDetails?.trim(),
      actionTaken: raw.actionTaken?.trim(),
      requiredImprovement: this.normalizeString(raw.requiredImprovement),
      followUpDate: this.normalizeDate(raw.followUpDate),
      isAcknowledgedByStaff: !!raw.isAcknowledgedByStaff,
      acknowledgedDate: raw.isAcknowledgedByStaff ? this.normalizeDate(raw.acknowledgedDate) : null,
      employeeRemarks: this.normalizeString(raw.employeeRemarks),
      resolutionNotes: this.normalizeString(raw.resolutionNotes),
      resolvedDate: raw.status === 'Resolved' ? this.normalizeDate(raw.resolvedDate) : null
    };
  }

  private buildDhivehiPayload(): Record<string, unknown> {
    const raw = this.dhivehiForm.getRawValue();
    return {
      subjectDv: this.normalizeString(raw.subjectDv),
      incidentDetailsDv: this.normalizeString(raw.incidentDetailsDv),
      actionTakenDv: this.normalizeString(raw.actionTakenDv),
      requiredImprovementDv: this.normalizeString(raw.requiredImprovementDv),
      employeeRemarksDv: this.normalizeString(raw.employeeRemarksDv),
      acknowledgementDv: this.normalizeString(raw.acknowledgementDv),
      resolutionNotesDv: this.normalizeString(raw.resolutionNotesDv)
    };
  }

  private downloadSavedDhivehiPdf(formId: string, fileName: string, formNumber: string, afterSuccess?: () => void): void {
    if (this.dhivehiDownloadLoading()) {
      return;
    }

    this.dhivehiDownloadLoading.set(true);
    this.api.downloadSavedStaffConductDhivehiPdf(formId).pipe(finalize(() => this.dhivehiDownloadLoading.set(false))).subscribe({
      next: (blob) => {
        this.download(blob, fileName);
        this.toast.success(`${formNumber} Dhivehi PDF downloaded.`);
        afterSuccess?.();
      },
      error: (error) => this.toast.error(extractApiError(error, `Failed to download the saved Dhivehi PDF for ${formNumber}.`))
    });
  }

  private normalizeString(value: string | null | undefined): string | null {
    const normalized = value?.trim() ?? '';
    return normalized ? normalized : null;
  }

  private normalizeDate(value: string | null | undefined): string | null {
    const normalized = value?.trim() ?? '';
    return normalized ? normalized : null;
  }

  private todayIso(): string {
    return new Date().toISOString().slice(0, 10);
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
