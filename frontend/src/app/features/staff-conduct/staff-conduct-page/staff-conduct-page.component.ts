import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  PagedResult,
  StaffConductDetail,
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
  template: `
    <section class="staff-conduct-page">
      <app-page-header
        title="Disciplinary & Warning"
        subtitle="Create, review, and export staff warning and disciplinary forms directly from the live payroll staff master.">
      </app-page-header>

      <div class="summary-grid">
        <app-card class="summary-card summary-card-all"><span>Total Forms</span><strong>{{ summary().totalForms }}</strong><small>All matching records</small></app-card>
        <app-card class="summary-card summary-card-warning"><span>Warnings</span><strong>{{ summary().warningCount }}</strong><small>Staff warning forms</small></app-card>
        <app-card class="summary-card summary-card-discipline"><span>Disciplinary</span><strong>{{ summary().disciplinaryCount }}</strong><small>Escalated HR forms</small></app-card>
        <app-card class="summary-card summary-card-open"><span>Open</span><strong>{{ summary().openCount }}</strong><small>Awaiting closure</small></app-card>
        <app-card class="summary-card summary-card-ack"><span>Acknowledged</span><strong>{{ summary().acknowledgedCount }}</strong><small>Staff acknowledged</small></app-card>
        <app-card class="summary-card summary-card-resolved"><span>Resolved</span><strong>{{ summary().resolvedCount }}</strong><small>Closed records</small></app-card>
      </div>

      <app-card class="filters-card">
        <div class="toolbar-row">
          <div>
            <h3>Form Register</h3>
            <p>Filter staff conduct records, export the current register, or issue a new form.</p>
          </div>

          <div class="toolbar-actions">
            <app-button variant="secondary" [loading]="summaryExportLoading() === 'excel'" [disabled]="busy()" (clicked)="exportSummary('excel')">Export Excel</app-button>
            <app-button variant="secondary" [loading]="summaryExportLoading() === 'pdf'" [disabled]="busy()" (clicked)="exportSummary('pdf')">Export PDF</app-button>
            <app-button *ngIf="isAdmin()" variant="secondary" [disabled]="busy()" (clicked)="openCreate('Warning')">New Warning</app-button>
            <app-button *ngIf="isAdmin()" [disabled]="busy()" (clicked)="openCreate('Disciplinary')">New Disciplinary</app-button>
          </div>
        </div>

        <form class="filters-grid" [formGroup]="filtersForm" (ngSubmit)="applyFilters()">
          <label>
            Search
            <input formControlName="search" placeholder="Form no., staff, subject, issued by">
          </label>

          <label>
            Staff
            <select formControlName="staffId">
              <option value="">All staff</option>
              <option *ngFor="let staff of staffOptions()" [value]="staff.id">{{ staff.staffId }} - {{ staff.staffName }}</option>
            </select>
          </label>

          <label>
            Form Type
            <select formControlName="formType">
              <option value="">All types</option>
              <option *ngFor="let option of formTypeOptions" [value]="option">{{ option }}</option>
            </select>
          </label>

          <label>
            Status
            <select formControlName="status">
              <option value="">All statuses</option>
              <option *ngFor="let option of statusOptions" [value]="option">{{ option }}</option>
            </select>
          </label>

          <label>
            From
            <input type="date" formControlName="dateFrom">
          </label>

          <label>
            To
            <input type="date" formControlName="dateTo">
          </label>

          <div class="filter-actions">
            <app-button type="submit" [loading]="listLoading()" [disabled]="busy()">Generate</app-button>
            <app-button type="button" variant="secondary" [disabled]="busy()" (clicked)="resetFilters()">Reset</app-button>
          </div>
        </form>
      </app-card>

      <app-card class="list-card">
        <div class="list-head">
          <div>
            <h3>Staff Conduct List</h3>
            <p>{{ pageSummary() }}</p>
          </div>
          <div class="meta-chip" *ngIf="page() as current">Page {{ current.pageNumber }} of {{ current.totalPages || 1 }}</div>
        </div>

        <app-data-table [hasData]="(page()?.items?.length || 0) > 0" emptyTitle="No forms found" emptyDescription="Create a warning or disciplinary form to start building the conduct register.">
          <thead>
            <tr>
              <th>Form No</th>
              <th>Type</th>
              <th>Issue Date</th>
              <th>Staff</th>
              <th>Subject</th>
              <th>Severity</th>
              <th>Status</th>
              <th>Issued By</th>
              <th>Follow Up</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let item of page()?.items">
              <td>{{ item.formNumber }}</td>
              <td><span class="pill" [ngClass]="typePillClass(item.formType)">{{ item.formType }}</span></td>
              <td>{{ item.issueDate | date: 'yyyy-MM-dd' }}</td>
              <td><div class="stack"><strong>{{ item.staffCode }} - {{ item.staffName }}</strong><span>{{ item.designation || '-' }} | {{ item.workSite || '-' }}</span></div></td>
              <td><div class="subject-cell"><strong>{{ item.subject }}</strong><small>Incident {{ item.incidentDate | date: 'yyyy-MM-dd' }}</small></div></td>
              <td>{{ item.severity }}</td>
              <td><span class="pill" [ngClass]="statusPillClass(item.status)">{{ item.status }}</span></td>
              <td>{{ item.issuedBy }}</td>
              <td>{{ item.followUpDate ? (item.followUpDate | date: 'yyyy-MM-dd') : '-' }}</td>
              <td class="row-actions">
                <div class="row-action-group">
                  <app-button size="sm" variant="secondary" [disabled]="busy()" (clicked)="openView(item)">View</app-button>
                  <app-button *ngIf="isAdmin()" size="sm" variant="secondary" [disabled]="busy()" (clicked)="openEdit(item)">Edit</app-button>
                  <app-button size="sm" variant="secondary" [loading]="rowExportKey() === exportKey(item.id, 'pdf')" [disabled]="busyExcept(item.id, 'pdf')" (clicked)="exportSingle(item, 'pdf')">PDF</app-button>
                  <app-button size="sm" variant="secondary" [loading]="rowExportKey() === exportKey(item.id, 'excel')" [disabled]="busyExcept(item.id, 'excel')" (clicked)="exportSingle(item, 'excel')">Excel</app-button>
                </div>
              </td>
            </tr>
          </tbody>
        </app-data-table>

        <div class="pager" *ngIf="page() as current">
          <span>Total {{ current.totalCount }} record{{ current.totalCount === 1 ? '' : 's' }}</span>
          <div class="pager-actions">
            <app-button size="sm" variant="secondary" [disabled]="listLoading() || current.pageNumber <= 1" (clicked)="changePage(current.pageNumber - 1)">Previous</app-button>
            <app-button size="sm" variant="secondary" [disabled]="listLoading() || current.pageNumber >= (current.totalPages || 1)" (clicked)="changePage(current.pageNumber + 1)">Next</app-button>
          </div>
        </div>
      </app-card>
    </section>

    <div class="drawer-backdrop" *ngIf="drawerMode()" (click)="closeDrawer()"></div>

    <aside class="drawer" *ngIf="drawerMode()">
      <app-card class="drawer-card">
        <ng-container [ngSwitch]="drawerMode()">
          <ng-container *ngSwitchCase="'view'">
            <div class="drawer-head">
              <div>
                <h3>{{ selectedDetail()?.formType }} Form</h3>
                <p>{{ selectedDetail()?.formNumber }}</p>
              </div>
              <div class="drawer-actions">
                <app-button variant="secondary" size="sm" [loading]="detailExportLoading() === 'excel'" [disabled]="busy()" (clicked)="exportDetailFromDrawer('excel')">Excel</app-button>
                <app-button variant="secondary" size="sm" [loading]="detailExportLoading() === 'pdf'" [disabled]="busy()" (clicked)="exportDetailFromDrawer('pdf')">PDF</app-button>
                <app-button *ngIf="isAdmin()" size="sm" [disabled]="busy()" (clicked)="editSelected()">Edit</app-button>
                <app-button size="sm" variant="secondary" [disabled]="busy()" (clicked)="closeDrawer()">Close</app-button>
              </div>
            </div>

            <div class="drawer-loading" *ngIf="detailLoading()">Loading form...</div>

            <ng-container *ngIf="!detailLoading() && selectedDetail() as detail">
              <div class="detail-grid">
                <article><span>Staff</span><strong>{{ detail.staffCode }} - {{ detail.staffName }}</strong><small>{{ detail.designation || '-' }} | {{ detail.workSite || '-' }}</small></article>
                <article><span>Status</span><strong>{{ detail.status }}</strong><small>Severity {{ detail.severity }}</small></article>
                <article><span>Issue Date</span><strong>{{ detail.issueDate | date: 'yyyy-MM-dd' }}</strong><small>Incident {{ detail.incidentDate | date: 'yyyy-MM-dd' }}</small></article>
                <article><span>Issued By</span><strong>{{ detail.issuedBy }}</strong><small>Witnessed by {{ detail.witnessedBy || '-' }}</small></article>
              </div>

              <div class="detail-panels">
                <section><h4>Subject</h4><p>{{ detail.subject }}</p></section>
                <section><h4>Incident Details</h4><p>{{ detail.incidentDetails }}</p></section>
                <section><h4>Action Taken</h4><p>{{ detail.actionTaken }}</p></section>
                <section><h4>Required Improvement</h4><p>{{ detail.requiredImprovement || '-' }}</p></section>
                <section><h4>Employee Remarks</h4><p>{{ detail.employeeRemarks || '-' }}</p></section>
                <section><h4>Resolution</h4><p>{{ detail.resolutionNotes || '-' }}</p></section>
              </div>

              <div class="detail-footer">
                <div><span>Acknowledged</span><strong>{{ detail.isAcknowledgedByStaff ? 'Yes' : 'No' }}</strong><small>{{ detail.acknowledgedDate ? (detail.acknowledgedDate | date: 'yyyy-MM-dd') : 'No acknowledgement date' }}</small></div>
                <div><span>Follow Up</span><strong>{{ detail.followUpDate ? (detail.followUpDate | date: 'yyyy-MM-dd') : '-' }}</strong><small>Resolved {{ detail.resolvedDate ? (detail.resolvedDate | date: 'yyyy-MM-dd') : 'Not resolved' }}</small></div>
              </div>
            </ng-container>
          </ng-container>

          <ng-container *ngSwitchDefault>
            <div class="drawer-head">
              <div>
                <h3>{{ drawerMode() === 'edit' ? 'Edit Form' : 'Create Form' }}</h3>
                <p>{{ drawerMode() === 'edit' ? selectedDetail()?.formNumber : 'Issue a new warning or disciplinary record.' }}</p>
              </div>
              <app-button size="sm" variant="secondary" [disabled]="saveLoading()" (clicked)="closeDrawer()">Close</app-button>
            </div>

            <form class="form-grid" [formGroup]="form" (ngSubmit)="saveForm()">
              <div class="two-col">
                <label>
                  Form Type
                  <select formControlName="formType"><option *ngFor="let option of formTypeOptions" [value]="option">{{ option }}</option></select>
                </label>
                <label>
                  Staff
                  <select formControlName="staffId"><option value="">Select staff</option><option *ngFor="let staff of staffOptions()" [value]="staff.id">{{ staff.staffId }} - {{ staff.staffName }}</option></select>
                  <small class="field-error" *ngIf="showError('staffId', 'required')">Staff is required.</small>
                </label>
              </div>

              <div class="two-col">
                <label>
                  Issue Date
                  <input type="date" formControlName="issueDate">
                  <small class="field-error" *ngIf="showError('issueDate', 'required')">Issue date is required.</small>
                </label>
                <label>
                  Incident Date
                  <input type="date" formControlName="incidentDate">
                  <small class="field-error" *ngIf="showError('incidentDate', 'required')">Incident date is required.</small>
                </label>
              </div>

              <label>
                Subject
                <input formControlName="subject" maxlength="160" placeholder="Short summary of the conduct issue">
                <small class="field-error" *ngIf="showError('subject', 'required')">Subject is required.</small>
              </label>

              <div class="two-col">
                <label>
                  Severity
                  <select formControlName="severity"><option *ngFor="let option of severityOptions" [value]="option">{{ option }}</option></select>
                </label>
                <label>
                  Status
                  <select formControlName="status"><option *ngFor="let option of statusOptions" [value]="option">{{ option }}</option></select>
                </label>
              </div>

              <div class="two-col">
                <label>
                  Issued By
                  <input formControlName="issuedBy" maxlength="120" placeholder="Supervisor or manager name">
                  <small class="field-error" *ngIf="showError('issuedBy', 'required')">Issued by is required.</small>
                </label>
                <label>
                  Witnessed By
                  <input formControlName="witnessedBy" maxlength="120" placeholder="Optional witness">
                </label>
              </div>

              <label>
                Incident Details
                <textarea formControlName="incidentDetails" rows="4" maxlength="4000" placeholder="Describe the incident in full"></textarea>
                <small class="field-error" *ngIf="showError('incidentDetails', 'required')">Incident details are required.</small>
              </label>

              <label>
                Action Taken
                <textarea formControlName="actionTaken" rows="3" maxlength="2000" placeholder="Explain the action, sanction, or next step"></textarea>
                <small class="field-error" *ngIf="showError('actionTaken', 'required')">Action taken is required.</small>
              </label>

              <label>
                Required Improvement
                <textarea formControlName="requiredImprovement" rows="3" maxlength="2000" placeholder="Expected improvement or follow-up requirements"></textarea>
              </label>

              <div class="two-col">
                <label>
                  Follow Up Date
                  <input type="date" formControlName="followUpDate">
                </label>
                <label class="checkbox-field">
                  <input type="checkbox" formControlName="isAcknowledgedByStaff">
                  <span>Staff acknowledged this form</span>
                </label>
              </div>

              <div class="two-col">
                <label>
                  Acknowledged Date
                  <input type="date" formControlName="acknowledgedDate">
                </label>
                <label>
                  Resolved Date
                  <input type="date" formControlName="resolvedDate">
                </label>
              </div>

              <label>
                Employee Remarks
                <textarea formControlName="employeeRemarks" rows="3" maxlength="2000" placeholder="Optional remarks from the staff member"></textarea>
              </label>

              <label>
                Resolution Notes
                <textarea formControlName="resolutionNotes" rows="3" maxlength="2000" placeholder="Optional closure notes"></textarea>
              </label>

              <div class="drawer-actions bottom-actions">
                <app-button type="submit" [loading]="saveLoading()" [disabled]="detailLoading()">{{ drawerMode() === 'edit' ? 'Save Changes' : 'Create Form' }}</app-button>
              </div>
            </form>
          </ng-container>
        </ng-container>
      </app-card>
    </aside>
  `,
  styles: `
    .staff-conduct-page { display: grid; gap: 1rem; }
    .summary-grid { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: .85rem; }
    .summary-card { --card-padding: 1rem; --card-shadow: 0 16px 32px rgba(81, 104, 170, .1); display: grid; gap: .18rem; }
    .summary-card span, .summary-card small { color: #6780ab; }
    .summary-card span { font-size: .78rem; text-transform: uppercase; letter-spacing: .06em; font-weight: 700; }
    .summary-card strong { font-size: 1.8rem; color: #2c4168; font-family: var(--font-heading); }
    .summary-card small { font-size: .82rem; }
    .summary-card-all { --card-bg: linear-gradient(145deg, rgba(240,244,255,.95), rgba(229,243,255,.84)); }
    .summary-card-warning { --card-bg: linear-gradient(145deg, rgba(255,246,231,.96), rgba(255,239,214,.86)); }
    .summary-card-discipline { --card-bg: linear-gradient(145deg, rgba(252,236,242,.96), rgba(247,223,233,.86)); }
    .summary-card-open { --card-bg: linear-gradient(145deg, rgba(238,242,255,.96), rgba(226,236,255,.86)); }
    .summary-card-ack { --card-bg: linear-gradient(145deg, rgba(234,248,241,.96), rgba(222,242,232,.86)); }
    .summary-card-resolved { --card-bg: linear-gradient(145deg, rgba(234,244,255,.96), rgba(221,238,255,.86)); }
    .filters-card, .list-card { --card-padding: 1rem; display: grid; gap: 1rem; }
    .toolbar-row, .list-head, .drawer-head, .pager { display: flex; justify-content: space-between; align-items: flex-start; gap: .9rem; flex-wrap: wrap; }
    .toolbar-row h3, .list-head h3, .drawer-head h3 { margin: 0; font-size: 1.08rem; font-family: var(--font-heading); color: #30466f; }
    .toolbar-row p, .list-head p, .drawer-head p { margin: .25rem 0 0; color: #7188af; font-size: .9rem; }
    .toolbar-actions, .drawer-actions, .filter-actions, .pager-actions, .row-action-group { display: flex; gap: .55rem; flex-wrap: wrap; align-items: center; }
    .filters-grid { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: .85rem; align-items: end; }
    .filters-grid label, .form-grid label { display: grid; gap: .38rem; font-size: .84rem; font-weight: 600; color: #5b7098; }
    .filters-grid input, .filters-grid select, .form-grid input, .form-grid select, .form-grid textarea { width: 100%; border-radius: 13px; border: 1px solid #d8e2f7; background: rgba(255,255,255,.92); padding: .72rem .82rem; color: #33496f; font: inherit; outline: none; transition: border-color .18s ease, box-shadow .18s ease; }
    .filters-grid input:focus, .filters-grid select:focus, .form-grid input:focus, .form-grid select:focus, .form-grid textarea:focus { border-color: #7e90f6; box-shadow: 0 0 0 4px rgba(120, 141, 239, .12); }
    .filter-actions { justify-content: flex-end; min-height: 46px; }
    .meta-chip { padding: .45rem .72rem; border-radius: 999px; border: 1px solid #d6e1fa; background: linear-gradient(135deg, #f5f8ff, #edf4ff); color: #5a6f99; font-size: .82rem; font-weight: 700; }
    .stack, .subject-cell { display: grid; gap: .12rem; white-space: normal; }
    .stack strong, .subject-cell strong { color: #30456c; }
    .stack span, .subject-cell small { color: #7288af; font-size: .8rem; }
    .pill { display: inline-flex; align-items: center; justify-content: center; padding: .34rem .62rem; border-radius: 999px; font-size: .75rem; font-weight: 700; border: 1px solid transparent; }
    .pill-warning { background: rgba(248, 183, 91, .16); color: #b77106; border-color: rgba(235, 165, 63, .32); }
    .pill-disciplinary { background: rgba(226, 126, 156, .16); color: #b14d6c; border-color: rgba(226, 126, 156, .3); }
    .pill-open { background: rgba(121, 139, 239, .14); color: #5368ca; border-color: rgba(121, 139, 239, .28); }
    .pill-acknowledged { background: rgba(245, 188, 84, .16); color: #b88017; border-color: rgba(245, 188, 84, .32); }
    .pill-resolved { background: rgba(88, 185, 132, .16); color: #2d9a6a; border-color: rgba(88, 185, 132, .3); }
    .row-actions { white-space: nowrap; }
    .pager { padding-top: .4rem; border-top: 1px solid #e6ecfa; color: #6277a1; font-size: .92rem; }
    .drawer-backdrop { position: fixed; inset: 0; background: rgba(31, 44, 76, .28); backdrop-filter: blur(3px); z-index: 90; }
    .drawer { position: fixed; top: 0; right: 0; height: 100dvh; width: min(760px, 100vw); padding: .85rem; z-index: 91; }
    .drawer-card { --card-padding: 1rem; height: 100%; overflow: auto; display: grid; align-content: start; gap: 1rem; }
    .drawer-loading { padding: 1rem; border-radius: 14px; background: linear-gradient(145deg, rgba(243,247,255,.94), rgba(237,244,255,.86)); color: #6b81aa; font-weight: 600; }
    .detail-grid, .detail-footer { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: .75rem; }
    .detail-grid article, .detail-footer div, .detail-panels section { border: 1px solid #dce4f8; border-radius: 16px; background: linear-gradient(160deg, rgba(255,255,255,.96), rgba(245,249,255,.86)); padding: .9rem; display: grid; gap: .22rem; }
    .detail-grid span, .detail-footer span, .detail-panels h4 { font-size: .78rem; text-transform: uppercase; letter-spacing: .05em; color: #6880aa; margin: 0; }
    .detail-grid strong, .detail-footer strong { font-size: 1rem; color: #31476f; }
    .detail-grid small, .detail-footer small { color: #758bb1; }
    .detail-panels { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .75rem; }
    .detail-panels p { margin: 0; color: #40567f; line-height: 1.5; white-space: pre-wrap; }
    .form-grid { display: grid; gap: .8rem; }
    .two-col { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .8rem; }
    .checkbox-field { display: flex !important; align-items: center; gap: .6rem; min-height: 48px; }
    .checkbox-field input { width: 18px; height: 18px; padding: 0; box-shadow: none; }
    .checkbox-field span { font-weight: 600; color: #425980; }
    .field-error { color: #c45373; font-size: .75rem; font-weight: 600; }
    .bottom-actions { justify-content: flex-end; }
    @media (max-width: 1400px) {
      .summary-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); }
      .filters-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); }
      .detail-grid, .detail-footer { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    @media (max-width: 980px) {
      .summary-grid, .filters-grid, .detail-panels, .detail-grid, .detail-footer, .two-col { grid-template-columns: 1fr; }
      .drawer { width: 100vw; padding: .5rem; }
      .toolbar-actions, .filter-actions, .drawer-actions, .pager-actions, .row-action-group { width: 100%; }
      .toolbar-actions app-button, .filter-actions app-button, .drawer-actions app-button { flex: 1 1 180px; }
    }
  `
})
export class StaffConductPageComponent implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);

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
  readonly pageNumber = signal(1);
  readonly pageSize = 10;
  readonly formTypeOptions: StaffConductFormType[] = ['Warning', 'Disciplinary'];
  readonly severityOptions: StaffConductSeverity[] = ['Low', 'Medium', 'High', 'Critical'];
  readonly statusOptions: StaffConductStatus[] = ['Open', 'Acknowledged', 'Resolved'];
  readonly isAdmin = computed(() => this.authService.user()?.role === 'Admin');
  readonly busy = computed(() => this.listLoading() || this.detailLoading() || this.saveLoading() || this.summaryExportLoading() !== null || this.detailExportLoading() !== null || this.rowExportKey() !== null);
  readonly pageSummary = computed(() => {
    const current = this.page();
    if (!current) {
      return 'Loading conduct register...';
    }
    return `Showing ${current.items.length} form${current.items.length === 1 ? '' : 's'} on this page.`;
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
    if (this.busy()) {
      return;
    }
    const key = this.exportKey(item.id, format);
    const request = format === 'excel' ? this.api.exportStaffConductFormExcel(item.id) : this.api.exportStaffConductFormPdf(item.id);
    this.rowExportKey.set(key);
    request.pipe(finalize(() => this.rowExportKey.set(null))).subscribe({
      next: (blob) => {
        this.download(blob, `${item.formNumber}.${format === 'excel' ? 'xlsx' : 'pdf'}`);
        this.toast.success(`${item.formNumber} exported to ${format.toUpperCase()}.`);
      },
      error: (error) => this.toast.error(extractApiError(error, `Failed to export ${item.formNumber}.`))
    });
  }

  exportDetailFromDrawer(format: ExportFormat): void {
    const detail = this.selectedDetail();
    if (!detail || this.busy()) {
      return;
    }
    const request = format === 'excel' ? this.api.exportStaffConductFormExcel(detail.id) : this.api.exportStaffConductFormPdf(detail.id);
    this.detailExportLoading.set(format);
    request.pipe(finalize(() => this.detailExportLoading.set(null))).subscribe({
      next: (blob) => {
        this.download(blob, `${detail.formNumber}.${format === 'excel' ? 'xlsx' : 'pdf'}`);
        this.toast.success(`${detail.formNumber} exported to ${format.toUpperCase()}.`);
      },
      error: (error) => this.toast.error(extractApiError(error, `Failed to export ${detail.formNumber}.`))
    });
  }

  showError(controlName: string, errorCode: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.touched && control.hasError(errorCode);
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
