import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { Customer, PagedResult, Vessel } from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { extractApiError } from '../../../core/utils/api-error.util';
import { NAME_REGEX, PHONE_REGEX } from '../../../core/validators/input-patterns';
import { PortalApiService } from '../../services/portal-api.service';

@Component({
  selector: 'app-customers-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppButtonComponent,
    AppCardComponent,
    AppConfirmDialogComponent,
    AppDataTableComponent,
    AppPageHeaderComponent,
    AppSearchBarComponent
  ],
  template: `
    <app-page-header title="Customers" subtitle="Manage customer master data and vessel references">
      <app-button variant="secondary" (clicked)="exportCustomersPdf()">Export PDF</app-button>
      <app-button variant="secondary" (clicked)="exportCustomersExcel()">Export Excel</app-button>
      <app-button (clicked)="openCreate()">New Customer</app-button>
    </app-page-header>

    <app-card>
      <div class="toolbar">
        <app-search-bar [value]="search()" placeholder="Search customer name, phone, email" (searchChange)="onSearch($event)"></app-search-bar>
      </div>
      <div class="summary" *ngIf="customers() as page">
        Total Customers: {{ page.totalCount }}
      </div>

      <app-data-table [hasData]="(customers()?.items?.length ?? 0) > 0" emptyTitle="No customers" emptyDescription="Create your first customer.">
        <thead>
          <tr>
            <th>Customer Name</th>
            <th>Tin No.</th>
            <th>Phone</th>
            <th>Email</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let customer of customers()?.items">
            <td>{{ customer.name }}</td>
            <td>{{ customer.tinNumber || '-' }}</td>
            <td>{{ customer.phone || '-' }}</td>
            <td>{{ customer.email || '-' }}</td>
            <td class="actions">
              <app-button size="sm" variant="secondary" (clicked)="openEdit(customer)">Edit</app-button>
              <app-button size="sm" variant="danger" (clicked)="confirmDelete(customer)">Delete</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>

      <div class="pager" *ngIf="customers() as page">
        <span>Page {{ page.pageNumber }} of {{ page.totalPages || 1 }}</span>
        <div>
          <app-button size="sm" variant="secondary" [disabled]="page.pageNumber <= 1" (clicked)="changePage(page.pageNumber - 1)">Prev</app-button>
          <app-button size="sm" variant="secondary" [disabled]="page.pageNumber >= page.totalPages" (clicked)="changePage(page.pageNumber + 1)">Next</app-button>
        </div>
      </div>
    </app-card>

    <app-card class="vessel-card">
      <div class="vessel-head">
        <div>
          <h3>Vessels</h3>
          <p>{{ vessels().length }} registered</p>
        </div>
        <app-button (clicked)="openCreateVessel()">Add Vessel</app-button>
      </div>

      <app-data-table
        [hasData]="vessels().length > 0"
        emptyTitle="No registered vessels"
        emptyDescription="Add your first vessel to use vessel names in delivery notes and invoices.">
        <thead>
          <tr>
            <th>Vessel Name</th>
            <th>Registration No.</th>
            <th>Issued Date</th>
            <th>Passenger Capacity</th>
            <th>Type</th>
            <th>Home Port</th>
            <th>Owner</th>
            <th>Contact</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let vessel of vessels()">
            <td>{{ vessel.name }}</td>
            <td>{{ vessel.registrationNumber || '-' }}</td>
            <td>{{ vessel.issuedDate || '-' }}</td>
            <td>{{ vessel.passengerCapacity ?? '-' }}</td>
            <td>{{ vessel.vesselType || '-' }}</td>
            <td>{{ vessel.homePort || '-' }}</td>
            <td>{{ vessel.ownerName || '-' }}</td>
            <td>{{ vessel.contactPhone || '-' }}</td>
            <td class="actions">
              <app-button size="sm" variant="danger" (clicked)="confirmDeleteVessel(vessel)">Delete</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>
    </app-card>

    <div class="drawer" *ngIf="formOpen()">
      <app-card>
        <h3>{{ editId() ? 'Edit Customer' : 'New Customer' }}</h3>
        <form [formGroup]="form" (ngSubmit)="save()" class="form-grid">
          <label>
            Name
            <input formControlName="name">
            <small class="field-error" *ngIf="form.controls.name.touched && form.controls.name.hasError('pattern')">Name must not contain numbers.</small>
          </label>
          <label>TIN No. <input formControlName="tinNumber"></label>
          <label>
            Phone
            <input formControlName="phone" type="tel">
            <small class="field-error" *ngIf="form.controls.phone.touched && form.controls.phone.hasError('pattern')">Phone number must contain only digits (optional leading +).</small>
          </label>
          <label>
            Email
            <input formControlName="email" type="email">
            <small class="field-error" *ngIf="form.controls.email.touched && form.controls.email.hasError('email')">Enter a valid email address.</small>
          </label>
          <label>References (comma-separated) <input formControlName="references"></label>
          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closeForm()">Cancel</app-button>
            <app-button type="submit">Save</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <div class="drawer" *ngIf="vesselFormOpen()">
      <app-card>
        <h3>New Vessel</h3>
        <form [formGroup]="vesselForm" (ngSubmit)="saveVessel()" class="form-grid">
          <div class="two-col">
            <label>Vessel Name <input formControlName="name"></label>
            <label>Registration Number <input formControlName="registrationNumber"></label>
          </div>
          <div class="two-col">
            <label>Issued Date <input type="date" formControlName="issuedDate"></label>
            <label>Passenger Capacity <input type="number" min="0" formControlName="passengerCapacity"></label>
          </div>
          <div class="two-col">
            <label>Vessel Type <input formControlName="vesselType"></label>
            <label>Home Port <input formControlName="homePort"></label>
          </div>
          <div class="two-col">
            <label>
              Owner Name
              <input formControlName="ownerName">
              <small class="field-error" *ngIf="vesselForm.controls.ownerName.touched && vesselForm.controls.ownerName.hasError('pattern')">Owner name must not contain numbers.</small>
            </label>
            <label>
              Contact Phone
              <input type="tel" formControlName="contactPhone">
              <small class="field-error" *ngIf="vesselForm.controls.contactPhone.touched && vesselForm.controls.contactPhone.hasError('pattern')">Contact phone must contain only digits (optional leading +).</small>
            </label>
          </div>
          <label>Notes <textarea rows="3" formControlName="notes"></textarea></label>
          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closeVesselForm()">Cancel</app-button>
            <app-button type="submit">Save</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <app-confirm-dialog
      [open]="deleteDialogOpen()"
      title="Delete customer"
      message="This will permanently delete the customer if no transactions exist."
      (cancel)="deleteDialogOpen.set(false)"
      (confirm)="deleteCustomer()"></app-confirm-dialog>

    <app-confirm-dialog
      [open]="deleteVesselDialogOpen()"
      title="Delete vessel"
      message="This will permanently delete the vessel if no delivery notes are linked."
      (cancel)="deleteVesselDialogOpen.set(false)"
      (confirm)="deleteVessel()"></app-confirm-dialog>
  `,
  styles: `
    .toolbar { display: flex; justify-content: flex-end; margin-bottom: .8rem; }
    .toolbar app-search-bar { width: 100%; max-width: 360px; }
    .summary {
      margin: 0 0 .75rem;
      font-size: .84rem;
      color: var(--text-muted);
      font-weight: 600;
    }
    .actions { display: flex; gap: .45rem; }
    .pager { margin-top: .8rem; display: flex; justify-content: space-between; align-items: center; }
    .pager > div { display: inline-flex; gap: .45rem; }
    .vessel-card { margin-top: 1.1rem; }
    .vessel-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: .8rem;
      margin-bottom: .7rem;
    }
    .vessel-head h3 {
      margin: 0;
    }
    .vessel-head p {
      margin: .2rem 0 0;
      font-size: .8rem;
      color: var(--text-muted);
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
      width: min(560px, 100%);
      max-height: 92dvh;
      overflow: auto;
      --card-bg: linear-gradient(160deg, rgba(255,255,255,.95), rgba(245,248,255,.9));
    }
    .form-grid { display: grid; gap: .78rem; }
    .two-col { display: grid; gap: .75rem; grid-template-columns: repeat(2, minmax(0, 1fr)); }
    label { display: grid; gap: .25rem; font-size: .83rem; color: var(--text-muted); align-content: start; }
    .field-error { margin-top: .08rem; display: block; font-size: .75rem; line-height: 1.25; color: #bf2f46; }
    input,
    textarea {
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .56rem .62rem;
      background: rgba(255,255,255,.92);
    }
    .form-actions { display: flex; justify-content: flex-end; gap: .5rem; }
    @media (max-width: 700px) {
      .toolbar {
        justify-content: stretch;
      }
      .toolbar app-search-bar {
        max-width: none;
      }
      .actions {
        flex-wrap: wrap;
      }
      .actions app-button {
        flex: 1 1 90px;
      }
      .vessel-head {
        flex-wrap: wrap;
      }
      .pager {
        flex-direction: column;
        align-items: flex-start;
        gap: .55rem;
      }
      .drawer {
        padding: .6rem;
        place-items: start center;
        overflow: auto;
      }
      .two-col {
        grid-template-columns: 1fr;
      }
      .form-actions {
        flex-wrap: wrap;
        justify-content: stretch;
      }
      .form-actions app-button {
        display: block;
        flex: 1 1 140px;
      }
    }
  `
})
export class CustomersPageComponent implements OnInit {
  readonly customers = signal<PagedResult<Customer> | null>(null);
  readonly search = signal('');
  readonly pageNumber = signal(1);
  readonly formOpen = signal(false);
  readonly editId = signal<string | null>(null);
  readonly deleteDialogOpen = signal(false);
  readonly deleteTarget = signal<Customer | null>(null);
  readonly vesselFormOpen = signal(false);
  readonly deleteVesselDialogOpen = signal(false);
  readonly deleteVesselTarget = signal<Vessel | null>(null);

  readonly vessels = signal<Vessel[]>([]);

  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.pattern(NAME_REGEX)]],
    tinNumber: [''],
    phone: ['', Validators.pattern(PHONE_REGEX)],
    email: ['', Validators.email],
    references: ['']
  });

  readonly vesselForm = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    registrationNumber: ['', Validators.maxLength(100)],
    issuedDate: [''],
    passengerCapacity: [null as number | null, Validators.min(0)],
    vesselType: ['', Validators.maxLength(100)],
    homePort: ['', Validators.maxLength(120)],
    ownerName: ['', [Validators.maxLength(200), Validators.pattern(NAME_REGEX)]],
    contactPhone: ['', [Validators.maxLength(50), Validators.pattern(PHONE_REGEX)]],
    notes: ['', Validators.maxLength(500)]
  });

  ngOnInit(): void {
    this.loadCustomers();
    this.loadVessels();
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.pageNumber.set(1);
    this.loadCustomers();
  }

  changePage(page: number): void {
    this.pageNumber.set(page);
    this.loadCustomers();
  }

  openCreate(): void {
    this.editId.set(null);
    this.form.reset({ name: '', tinNumber: '', phone: '', email: '', references: '' });
    this.formOpen.set(true);
  }

  openEdit(customer: Customer): void {
    this.editId.set(customer.id);
    this.form.reset({
      name: customer.name,
      tinNumber: customer.tinNumber || '',
      phone: customer.phone || '',
      email: customer.email || '',
      references: customer.references.join(', ')
    });
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error('Please fill required customer fields.');
      return;
    }

    const value = this.form.getRawValue();
    const payload = {
      name: value.name,
      tinNumber: value.tinNumber || undefined,
      phone: value.phone || undefined,
      email: value.email || undefined,
      references: value.references
        .split(',')
        .map((item) => item.trim())
        .filter(Boolean)
    };

    const request$ = this.editId()
      ? this.api.updateCustomer(this.editId()!, payload)
      : this.api.createCustomer(payload);

    request$.subscribe({
      next: () => {
        this.toast.success(`Customer ${this.editId() ? 'updated' : 'created'} successfully.`);
        this.formOpen.set(false);
        this.loadCustomers();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to save customer.'))
    });
  }

  confirmDelete(customer: Customer): void {
    this.deleteTarget.set(customer);
    this.deleteDialogOpen.set(true);
  }

  deleteCustomer(): void {
    const target = this.deleteTarget();
    if (!target) {
      return;
    }

    this.api.deleteCustomer(target.id).subscribe({
      next: () => {
        this.toast.success('Customer deleted.');
        this.deleteDialogOpen.set(false);
        this.loadCustomers();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to delete customer.'))
    });
  }

  openCreateVessel(): void {
    this.vesselForm.reset({
      name: '',
      registrationNumber: '',
      issuedDate: '',
      passengerCapacity: null,
      vesselType: '',
      homePort: '',
      ownerName: '',
      contactPhone: '',
      notes: ''
    });
    this.vesselFormOpen.set(true);
  }

  closeVesselForm(): void {
    this.vesselFormOpen.set(false);
  }

  saveVessel(): void {
    if (this.vesselForm.invalid) {
      this.vesselForm.markAllAsTouched();
      this.toast.error('Please fill required vessel fields.');
      return;
    }

    const value = this.vesselForm.getRawValue();
    const payload = {
      name: value.name?.trim() || '',
      registrationNumber: value.registrationNumber?.trim() || undefined,
      issuedDate: value.issuedDate || undefined,
      passengerCapacity: value.passengerCapacity ?? undefined,
      vesselType: value.vesselType?.trim() || undefined,
      homePort: value.homePort?.trim() || undefined,
      ownerName: value.ownerName?.trim() || undefined,
      contactPhone: value.contactPhone?.trim() || undefined,
      notes: value.notes?.trim() || undefined
    };

    this.api.createVessel(payload).subscribe({
      next: () => {
        this.toast.success('Vessel created.');
        this.vesselFormOpen.set(false);
        this.loadVessels();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to add vessel.'))
    });
  }

  confirmDeleteVessel(vessel: Vessel): void {
    this.deleteVesselTarget.set(vessel);
    this.deleteVesselDialogOpen.set(true);
  }

  deleteVessel(): void {
    const target = this.deleteVesselTarget();
    if (!target) {
      return;
    }

    this.api.deleteVessel(target.id).subscribe({
      next: () => {
        this.toast.success('Vessel deleted.');
        this.deleteVesselDialogOpen.set(false);
        this.loadVessels();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to delete vessel.'))
    });
  }

  private loadCustomers(): void {
    this.api.getCustomers({ pageNumber: this.pageNumber(), pageSize: 10, search: this.search() }).subscribe({
      next: (result) => this.customers.set(result),
      error: () => this.toast.error('Failed to load customers.')
    });
  }

  private loadVessels(): void {
    this.api.getAllVessels().subscribe({
      next: (vessels) => this.vessels.set(vessels),
      error: () => this.toast.error('Failed to load vessels.')
    });
  }

  exportCustomersPdf(): void {
    this.api.exportCustomersPdf(this.buildExportParams()).subscribe({
      next: (file) => {
        this.download(file, `customers-${this.today()}.pdf`);
        this.toast.success('Customers PDF exported.');
      },
      error: () => this.toast.error('Failed to export customers PDF.')
    });
  }

  exportCustomersExcel(): void {
    this.api.exportCustomersExcel(this.buildExportParams()).subscribe({
      next: (file) => {
        this.download(file, `customers-${this.today()}.xlsx`);
        this.toast.success('Customers Excel exported.');
      },
      error: () => this.toast.error('Failed to export customers Excel.')
    });
  }

  private buildExportParams(): Record<string, unknown> {
    return {
      search: this.search(),
      sortDirection: 'desc'
    };
  }

  private download(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private readError(error: unknown, fallback: string): string {
    return extractApiError(error, fallback);
  }
}


