import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, firstValueFrom } from 'rxjs';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { AppDataTableComponent } from '../../../shared/components/app-data-table/app-data-table.component';
import { AppDateBadgeComponent } from '../../../shared/components/app-date-badge/app-date-badge.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { Customer, DeliveryNote, DeliveryNoteAttachment, DeliveryNoteListItem, PagedResult, Vessel } from '../../../core/models/app.models';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { PortalApiService } from '../../services/portal-api.service';

@Component({
  selector: 'app-delivery-notes-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppButtonComponent,
    AppCardComponent,
    AppConfirmDialogComponent,
    AppDataTableComponent,
    AppDateBadgeComponent,
    AppPageHeaderComponent,
    AppSearchBarComponent,
    AppCurrencyPipe
  ],
  template: `
    <app-page-header title="Delivery Notes" subtitle="Create and manage tenant-scoped vessel delivery notes">
      <app-button *ngIf="isAdmin()" variant="danger" (clicked)="openClearAllDialog()">Clear All</app-button>
      <app-button (clicked)="openCreate()">New Note</app-button>
    </app-page-header>

    <app-card>
      <div class="filters">
        <app-search-bar [value]="search()" placeholder="Search notes or customer" (searchChange)="onSearch($event)"></app-search-bar>
        <select [value]="createdDatePreset()" (change)="onCreatedDatePresetChange($event)">
          <option value="">All Dates</option>
          <option *ngFor="let option of datePresetOptions" [value]="option.value">{{ option.label }}</option>
        </select>
        <select [value]="customerFilter()" (change)="onCustomerFilterChange($event)">
          <option value="">All Customers</option>
          <option *ngFor="let customer of customers()" [value]="customer.id">{{ customer.name }}</option>
        </select>
        <select [value]="vesselFilter()" (change)="onVesselFilterChange($event)">
          <option value="">All Couriers</option>
          <option *ngFor="let vessel of vessels()" [value]="vessel.id">{{ vessel.name }}</option>
        </select>
      </div>
      <div class="summary" *ngIf="notes() as page">
        Showing {{ page.totalCount }} delivery note{{ page.totalCount === 1 ? '' : 's' }}
        <span *ngIf="createdDatePreset()">for {{ currentDateFilterLabel() }}</span>.
      </div>

      <app-data-table [hasData]="(notes()?.items?.length ?? 0) > 0" emptyTitle="No delivery notes" emptyDescription="Create your first delivery note.">
        <thead>
          <tr>
            <th>DN No</th>
            <th>Date</th>
            <th>Details</th>
            <th>Qty</th>
            <th>Customer</th>
            <th>Courier</th>
            <th>Rate</th>
            <th>Currency</th>
            <th>Total</th>
            <th>Invoice No.</th>
            <th>Cash Payment</th>
            <th>Vessel Payment</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let note of notes()?.items">
            <td>{{ note.deliveryNoteNo }}</td>
            <td><app-date-badge [value]="note.date"></app-date-badge></td>
            <td>{{ note.details }}</td>
            <td>{{ note.qty }}</td>
            <td>{{ note.customer }}</td>
            <td>{{ note.vessel || '-' }}</td>
            <td>{{ note.rate | appCurrency: note.currency }}</td>
            <td>{{ note.currency }}</td>
            <td>{{ note.total | appCurrency: note.currency }}</td>
            <td>{{ note.invoiceNo || '-' }}</td>
            <td>{{ note.cashPayment > 0 ? 'Yes' : 'No' }}</td>
            <td>{{ note.vesselPayment > 0 ? 'Yes' : 'No' }}</td>
            <td class="actions">
              <app-button size="sm" variant="secondary" (clicked)="viewDetail(note)">View</app-button>
              <app-button
                *ngIf="note.hasPoAttachment"
                size="sm"
                variant="secondary"
                (clicked)="viewPoAttachment(note.id, note.poAttachmentFileName)">
                View PO
              </app-button>
              <app-button
                *ngIf="note.hasVesselPaymentInvoiceAttachment"
                size="sm"
                variant="secondary"
                (clicked)="viewVesselPaymentAttachment(note.id, note.vesselPaymentInvoiceAttachmentFileName)">
                View Invoice
              </app-button>
              <app-button size="sm" variant="secondary" (clicked)="edit(note)">Edit</app-button>
              <app-button *ngIf="isAdmin()" size="sm" variant="danger" (clicked)="confirmDelete(note)">Delete</app-button>
              <app-button
                size="sm"
                variant="secondary"
                [disabled]="!!note.invoiceNo || note.cashPayment > 0"
                (clicked)="createInvoice(note)">Create Invoice</app-button>
              <app-button size="sm" variant="secondary" (clicked)="export(note)">PDF</app-button>
            </td>
          </tr>
        </tbody>
      </app-data-table>

      <div class="pager" *ngIf="notes() as page">
        <span>Page {{ page.pageNumber }} of {{ page.totalPages || 1 }}</span>
        <div>
          <app-button size="sm" variant="secondary" [disabled]="page.pageNumber <= 1" (clicked)="changePage(page.pageNumber - 1)">Prev</app-button>
          <app-button size="sm" variant="secondary" [disabled]="page.pageNumber >= page.totalPages" (clicked)="changePage(page.pageNumber + 1)">Next</app-button>
        </div>
      </div>
    </app-card>

    <div class="drawer" *ngIf="formOpen()">
      <app-card>
        <h3>{{ editId() ? 'Edit Delivery Note' : 'New Delivery Note' }}</h3>
        <form [formGroup]="form" (ngSubmit)="save()" class="form-grid">
          <div class="two-col">
            <label>Date <input type="date" formControlName="date"></label>
            <label>Currency
              <select formControlName="currency">
                <option value="MVR">MVR</option>
                <option value="USD">USD</option>
              </select>
            </label>
          </div>

          <div class="two-col">
            <label>Customer
              <select formControlName="customerId">
                <option value="">Select customer</option>
                <option *ngFor="let customer of customers()" [value]="customer.id">{{ customer.name }}</option>
              </select>
            </label>
            <label>PO Number (Optional) <input type="text" formControlName="poNumber" (input)="onPoNumberChange()"></label>
          </div>

          <div class="attachment-field" *ngIf="hasPoNumberValue()">
            <span class="attachment-label">Attach PO</span>
            <input
              #poAttachmentInput
              class="file-input"
              type="file"
              accept=".pdf,image/png,image/jpeg,image/webp,image/gif"
              (change)="onPoAttachmentPicked($event)">
            <button
              type="button"
              class="attachment-dropzone"
              [class.dragging]="poAttachmentDragActive()"
              [class.uploading]="attachmentUploading()"
              (click)="openPoAttachmentPicker()"
              (dragover)="onPoAttachmentDragOver($event)"
              (dragleave)="onPoAttachmentDragLeave($event)"
              (drop)="onPoAttachmentDrop($event)">
              <ng-container *ngIf="pendingPoAttachmentLabel() as pendingLabel; else existingPoAttachmentState">
                <span>{{ pendingLabel }}</span>
                <small>This file will be uploaded when you save the delivery note.</small>
                <button type="button" class="attachment-remove" (click)="clearPendingPoAttachment($event)">Remove selection</button>
              </ng-container>
              <ng-template #existingPoAttachmentState>
                <ng-container *ngIf="currentPoAttachment() as attachment; else emptyPoAttachmentState">
                  <span>{{ attachment.fileName }}</span>
                  <small>{{ formatAttachmentSize(attachment.sizeBytes) }} | Existing uploaded PO</small>
                  <button type="button" class="attachment-view" (click)="viewCurrentPoAttachment($event)">View uploaded PO</button>
                </ng-container>
              </ng-template>
              <ng-template #emptyPoAttachmentState>
                <span>{{ attachmentUploading() ? 'Uploading PO...' : 'Drag and drop PO here' }}</span>
                <small>or click to browse (PDF, PNG, JPG, WEBP, GIF up to 10MB)</small>
              </ng-template>
            </button>
          </div>

        <div class="two-col">
          <label>Vessel Payment
              <select formControlName="hasVesselPayment" (change)="onVesselPaymentChange($event)">
                <option value="No">No</option>
                <option value="Yes">Yes</option>
              </select>
            </label>
            <label>Courier
              <select formControlName="vesselId">
                <option value="">Select courier</option>
                <option *ngFor="let vessel of vessels()" [value]="vessel.id">{{ vessel.name }}</option>
              </select>
            </label>
          </div>
          <small class="field-error" *ngIf="form.controls.hasVesselPayment.value === 'Yes' && form.controls.vesselId.invalid && form.controls.vesselId.touched">
            Please select a courier when vessel payment is Yes.
          </small>
          <div class="payment-callout" *ngIf="form.controls.hasVesselPayment.value === 'Yes'">
            <div>
              <strong>Vessel payment details</strong>
              <span *ngIf="hasVesselPaymentDetails(); else missingVesselPaymentDetails">
                Fee: {{ form.controls.vesselPaymentFee.value | appCurrency: form.controls.currency.value }} | Invoice No: {{ form.controls.vesselPaymentInvoiceNumber.value }}
              </span>
              <ng-template #missingVesselPaymentDetails>
                <span>Fee and vessel payment invoice number are required.</span>
              </ng-template>
              <span *ngIf="selectedAttachmentLabel() as attachmentLabel">
                Attachment: {{ attachmentLabel }}
              </span>
            </div>
            <app-button size="sm" variant="secondary" (clicked)="openVesselPaymentDialog()">
              {{ hasVesselPaymentDetails() ? 'Edit Details' : 'Add Details' }}
            </app-button>
          </div>

          <label>Notes <textarea rows="2" formControlName="notes"></textarea></label>

          <div class="item-section">
            <h4>Items</h4>
            <app-button size="sm" variant="secondary" (clicked)="addItem()">Add Item</app-button>
          </div>

          <div formArrayName="items" class="items-grid">
            <div class="item-row" *ngFor="let item of items.controls; let i = index" [formGroupName]="i">
              <label class="item-field">
                <span>Details</span>
                <input type="text" formControlName="details" placeholder="e.g. Fuel delivery">
              </label>
              <label class="item-field">
                <span>Qty</span>
                <input type="number" formControlName="qty" placeholder="1">
              </label>
              <label class="item-field">
                <span>Rate</span>
                <input type="number" formControlName="rate" placeholder="0.00">
              </label>
              <label class="item-field">
                <span>Cash Payment</span>
                <select formControlName="cashPayment">
                  <option value="No">No</option>
                  <option value="Yes">Yes</option>
                </select>
              </label>
              <div class="item-actions">
                <app-button size="sm" variant="danger" (clicked)="removeItem(i)" [disabled]="items.length === 1">Remove</app-button>
              </div>
            </div>
          </div>

          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closeForm()">Cancel</app-button>
            <app-button type="submit">Save</app-button>
          </div>
        </form>
      </app-card>
    </div>

    <div class="drawer nested-drawer" *ngIf="vesselPaymentDialogOpen()">
      <app-card class="vessel-payment-card">
        <h3>Vessel Payment Details</h3>
        <div [formGroup]="form" class="form-grid">
          <label>Vessel Payment Fee
            <input type="number" formControlName="vesselPaymentFee" step="0.01" placeholder="0.00">
          </label>
          <small class="field-error" *ngIf="form.controls.vesselPaymentFee.touched && form.controls.vesselPaymentFee.invalid">
            Enter a vessel payment fee greater than 0.
          </small>

          <label>Vessel Payment Invoice Number
            <input type="text" formControlName="vesselPaymentInvoiceNumber" placeholder="Enter invoice number">
          </label>
          <small class="field-error" *ngIf="form.controls.vesselPaymentInvoiceNumber.touched && form.controls.vesselPaymentInvoiceNumber.invalid">
            Enter the vessel payment invoice number.
          </small>

          <div class="attachment-field">
            <span class="attachment-label">Attach Invoice</span>
            <input
              #vesselPaymentAttachmentInput
              class="file-input"
              type="file"
              accept=".pdf,image/png,image/jpeg,image/webp,image/gif"
              (change)="onVesselPaymentAttachmentPicked($event)">
            <button
              type="button"
              class="attachment-dropzone"
              [class.dragging]="attachmentDragActive()"
              [class.uploading]="attachmentUploading()"
              (click)="openVesselPaymentAttachmentPicker()"
              (dragover)="onAttachmentDragOver($event)"
              (dragleave)="onAttachmentDragLeave($event)"
              (drop)="onAttachmentDrop($event)">
              <ng-container *ngIf="pendingAttachmentLabel() as pendingLabel; else existingAttachmentState">
                <span>{{ pendingLabel }}</span>
                <small>This file will be uploaded when you save the delivery note.</small>
                <button type="button" class="attachment-remove" (click)="clearPendingVesselPaymentAttachment($event)">Remove selection</button>
              </ng-container>
              <ng-template #existingAttachmentState>
                <ng-container *ngIf="currentAttachment() as attachment; else emptyAttachmentState">
                  <span>{{ attachment.fileName }}</span>
                  <small>{{ formatAttachmentSize(attachment.sizeBytes) }} | Existing uploaded invoice</small>
                  <button type="button" class="attachment-view" (click)="viewCurrentAttachment($event)">View uploaded invoice</button>
                </ng-container>
              </ng-template>
              <ng-template #emptyAttachmentState>
                <span>{{ attachmentUploading() ? 'Uploading invoice...' : 'Drag and drop invoice here' }}</span>
                <small>or click to browse (PDF, PNG, JPG, WEBP, GIF up to 10MB)</small>
              </ng-template>
            </button>
          </div>

          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closeVesselPaymentDialog()">Close</app-button>
            <app-button (clicked)="saveVesselPaymentDialog()">Save Details</app-button>
          </div>
        </div>
      </app-card>
    </div>

    <div class="drawer" *ngIf="selectedNote()">
      <app-card>
        <h3>Delivery Note Details</h3>
        <p><strong>DN No:</strong> {{ selectedNote()?.deliveryNoteNo }}</p>
        <p><strong>PO No:</strong> {{ selectedNote()?.poNumber || '-' }}</p>
        <p *ngIf="selectedNote()?.hasPoAttachment">
          <strong>Attached PO:</strong> {{ selectedNote()?.poAttachmentFileName }}
        </p>
        <p><strong>Date:</strong> {{ selectedNote()?.date }}</p>
        <p><strong>Currency:</strong> {{ selectedNote()?.currency }}</p>
        <p><strong>Customer:</strong> {{ selectedNote()?.customerName }}</p>
        <p><strong>Courier:</strong> {{ selectedNote()?.vesselName || '-' }}</p>
        <p><strong>Vessel Payment Fee:</strong> {{ (selectedNote()?.vesselPaymentFee || 0) | appCurrency: (selectedNote()?.currency || 'MVR') }}</p>
        <p><strong>Vessel Payment Invoice No:</strong> {{ selectedNote()?.vesselPaymentInvoiceNumber || '-' }}</p>
        <p *ngIf="selectedNote()?.hasVesselPaymentInvoiceAttachment">
          <strong>Attached Invoice:</strong> {{ selectedNote()?.vesselPaymentInvoiceAttachmentFileName }}
        </p>
        <p><strong>Total:</strong> {{ selectedNote()?.totalAmount || 0 | appCurrency: (selectedNote()?.currency || 'MVR') }}</p>
        <div class="detail-items">
          <div *ngFor="let item of selectedNote()?.items">
            {{ item.details }} - {{ item.qty }} x {{ item.rate | appCurrency: (selectedNote()?.currency || 'MVR') }} = {{ (item.total || 0) | appCurrency: (selectedNote()?.currency || 'MVR') }}
          </div>
        </div>
        <div class="form-actions">
          <app-button
            *ngIf="selectedNote()?.hasPoAttachment"
            variant="secondary"
            (clicked)="viewPoAttachment(selectedNote()!.id, selectedNote()?.poAttachmentFileName)">
            View Uploaded PO
          </app-button>
          <app-button
            *ngIf="selectedNote()?.hasVesselPaymentInvoiceAttachment"
            variant="secondary"
            (clicked)="viewVesselPaymentAttachment(selectedNote()!.id, selectedNote()?.vesselPaymentInvoiceAttachmentFileName)">
            View Uploaded Invoice
          </app-button>
          <app-button variant="secondary" (clicked)="selectedNote.set(null)">Close</app-button>
        </div>
      </app-card>
    </div>

    <app-confirm-dialog
      [open]="deleteDialogOpen()"
      title="Delete delivery note"
      message="This action is blocked if invoice already exists for this note."
      (cancel)="closeDeleteDialog()"
      (confirm)="deleteNote()"></app-confirm-dialog>

    <div class="drawer" *ngIf="clearAllDialogOpen()">
      <app-card class="clear-card">
        <h3>Clear All Delivery Notes</h3>
        <p>This will permanently delete all delivery notes for this business from the application and database.</p>
        <form [formGroup]="clearAllForm" (ngSubmit)="clearAllNotes()" class="form-grid">
          <label>Confirm with your password
            <input type="password" formControlName="password" autocomplete="current-password" placeholder="Enter your account password">
          </label>
          <div class="form-actions">
            <app-button variant="secondary" (clicked)="closeClearAllDialog()">Cancel</app-button>
            <app-button type="submit" variant="danger" [loading]="clearAllPending()">Clear All</app-button>
          </div>
        </form>
      </app-card>
    </div>
  `,
  styles: `
    .filters {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: .6rem;
      margin-bottom: .9rem;
      align-items: center;
    }
    .filters app-search-bar {
      width: 100%;
      min-width: 0;
    }
    .filters select {
      width: 100%;
      min-width: 0;
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .58rem .66rem;
      background: linear-gradient(145deg, rgba(255,255,255,.92), rgba(245,249,255,.85));
      height: 42px;
    }
    .actions {
      display: flex;
      flex-wrap: nowrap;
      gap: .35rem;
      align-items: center;
      min-width: 430px;
    }
    .summary {
      margin: .05rem 0 .72rem;
      font-size: .84rem;
      color: var(--text-muted);
      font-weight: 600;
    }
    .actions app-button {
      flex: 0 0 auto;
      white-space: nowrap;
    }
    .pager { margin-top: .8rem; display: flex; justify-content: space-between; align-items: center; gap: .55rem; }
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
      width: min(980px, 100%);
      max-height: 90vh;
      overflow: auto;
      --card-bg: linear-gradient(160deg, rgba(255,255,255,.95), rgba(245,248,255,.9));
    }
    .form-grid { display: grid; gap: .65rem; }
    .two-col { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .65rem; }
    label { display: grid; gap: .2rem; font-size: .82rem; color: var(--text-muted); }
    .form-grid input,
    .form-grid select,
    .form-grid textarea {
      width: 100%;
      min-width: 0;
      border: 1px solid var(--border-soft);
      border-radius: 12px;
      padding: .52rem .6rem;
      background: rgba(255,255,255,.92);
    }
    .payment-callout {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: .75rem;
      padding: .75rem .85rem;
      border: 1px solid var(--border-soft);
      border-radius: 14px;
      background: linear-gradient(145deg, rgba(245,249,255,.95), rgba(255,255,255,.92));
    }
    .payment-callout div {
      display: grid;
      gap: .18rem;
      min-width: 0;
    }
    .payment-callout strong {
      color: #283b63;
      font-size: .84rem;
    }
    .payment-callout span {
      color: var(--text-muted);
      font-size: .79rem;
      line-height: 1.4;
      word-break: break-word;
    }
    .item-section { display: flex; justify-content: space-between; align-items: center; margin-top: .4rem; }
    .item-section h4 { margin: 0; }
    .items-grid { display: grid; gap: .6rem; }
    .item-row {
      display: grid;
      grid-template-columns: minmax(0, 2fr) repeat(3, minmax(0, 1fr)) auto;
      gap: .5rem;
      align-items: end;
    }
    .item-field { display: grid; gap: .2rem; font-size: .78rem; color: var(--text-muted); }
    .item-actions { display: flex; align-items: end; }
    input.ng-invalid.ng-touched,
    select.ng-invalid.ng-touched,
    textarea.ng-invalid.ng-touched {
      border-color: #d64550;
      background: #fff6f7;
    }
    .field-error {
      margin-top: -.2rem;
      color: #c7353f;
      font-size: .78rem;
    }
    .attachment-field {
      display: grid;
      gap: .3rem;
    }
    .attachment-label {
      color: var(--text-muted);
      font-size: .82rem;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .file-input {
      display: none;
    }
    .attachment-dropzone {
      border: 1px dashed #b6c8ee;
      border-radius: 13px;
      background: linear-gradient(150deg, rgba(245, 248, 255, .95), rgba(235, 242, 255, .9));
      min-height: 128px;
      padding: .78rem;
      display: grid;
      place-items: center;
      gap: .38rem;
      text-align: center;
      color: #4f6490;
      cursor: pointer;
      transition: border-color .2s ease, background .2s ease, transform .2s ease;
    }
    .attachment-dropzone:hover {
      border-color: #90a6e7;
      transform: translateY(-1px);
    }
    .attachment-dropzone.dragging {
      border-color: #6f84f5;
      background: linear-gradient(150deg, rgba(228, 236, 255, .96), rgba(214, 228, 255, .9));
    }
    .attachment-dropzone.uploading {
      cursor: progress;
      opacity: .78;
    }
    .attachment-dropzone span {
      font-size: .85rem;
      font-family: var(--font-heading);
      font-weight: 600;
      line-height: 1.35;
      word-break: break-word;
    }
    .attachment-dropzone small {
      color: #667aa4;
      font-size: .74rem;
      line-height: 1.35;
      word-break: break-word;
    }
    .attachment-remove,
    .attachment-view {
      margin-top: .15rem;
      border: 1px solid #cad7f6;
      border-radius: 9px;
      background: #fff;
      color: #4f6392;
      padding: .28rem .55rem;
      font-size: .76rem;
      font-family: var(--font-heading);
      font-weight: 600;
      cursor: pointer;
    }
    .attachment-remove:hover,
    .attachment-view:hover {
      border-color: #9ab0ec;
      background: #f5f8ff;
    }
    .form-actions { display: flex; justify-content: flex-end; gap: .5rem; }
    .detail-items { margin: .5rem 0; display: grid; gap: .3rem; }
    .nested-drawer {
      z-index: 1300;
      background: rgba(43, 54, 87, .42);
    }
    .vessel-payment-card {
      width: min(460px, 100%);
    }
    .clear-card {
      width: min(520px, 100%);
    }
    .clear-card p {
      margin: .3rem 0 .8rem;
      color: var(--text-muted);
      line-height: 1.45;
    }
    @media (max-width: 1200px) {
      .filters {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .filters app-search-bar {
        grid-column: 1 / -1;
      }
    }
    @media (max-width: 940px) {
      .item-row {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .item-actions {
        grid-column: 1 / -1;
      }
    }
    @media (max-width: 700px) {
      .filters {
        grid-template-columns: 1fr;
      }
      .payment-callout {
        flex-direction: column;
        align-items: stretch;
      }
      .two-col {
        grid-template-columns: 1fr;
      }
      .item-row {
        grid-template-columns: 1fr;
      }
      .item-section {
        flex-wrap: wrap;
        gap: .45rem;
      }
      .item-section app-button {
        width: 100%;
      }
      .form-actions {
        flex-wrap: wrap;
      }
      .form-actions app-button {
        display: block;
        flex: 1 1 140px;
      }
      .pager {
        flex-direction: column;
        align-items: flex-start;
      }
      .pager > div {
        display: inline-flex;
        gap: .45rem;
        width: 100%;
      }
      .drawer {
        padding: .6rem;
      }
      .drawer app-card {
        max-height: 95dvh;
      }
    }
  `
})
export class DeliveryNotesPageComponent implements OnInit {
  readonly notes = signal<PagedResult<DeliveryNoteListItem> | null>(null);
  readonly search = signal('');
  readonly pageNumber = signal(1);
  readonly customerFilter = signal('');
  readonly vesselFilter = signal('');
  readonly createdDatePreset = signal('');

  readonly customers = signal<Customer[]>([]);
  readonly vessels = signal<Vessel[]>([]);

  readonly formOpen = signal(false);
  readonly editId = signal<string | null>(null);
  readonly selectedNote = signal<DeliveryNote | null>(null);
  readonly vesselPaymentDialogOpen = signal(false);
  readonly poAttachmentDragActive = signal(false);
  readonly attachmentDragActive = signal(false);
  readonly attachmentUploading = signal(false);
  readonly currentPoAttachment = signal<DeliveryNoteAttachment | null>(null);
  readonly currentAttachment = signal<DeliveryNoteAttachment | null>(null);
  readonly pendingPoAttachment = signal<File | null>(null);
  readonly pendingVesselPaymentAttachment = signal<File | null>(null);

  readonly deleteDialogOpen = signal(false);
  readonly deleteTarget = signal<DeliveryNoteListItem | null>(null);

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PortalApiService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  @ViewChild('poAttachmentInput')
  private poAttachmentInput?: ElementRef<HTMLInputElement>;

  @ViewChild('vesselPaymentAttachmentInput')
  private vesselPaymentAttachmentInput?: ElementRef<HTMLInputElement>;

  readonly form = this.fb.nonNullable.group({
    date: [this.today(), Validators.required],
    poNumber: [''],
    currency: ['MVR', Validators.required],
    customerId: ['', Validators.required],
    hasVesselPayment: ['No', Validators.required],
    vesselId: [''],
    vesselPaymentFee: [0],
    vesselPaymentInvoiceNumber: [''],
    notes: [''],
    items: this.fb.array([this.createItemForm()])
  });

  readonly clearAllDialogOpen = signal(false);
  readonly clearAllPending = signal(false);
  readonly clearAllForm = this.fb.nonNullable.group({
    password: ['', Validators.required]
  });

  readonly datePresetOptions = [
    { value: 'today', label: 'Today' },
    { value: 'yesterday', label: 'Yesterday' },
    { value: 'last-week', label: 'Last Week' },
    { value: 'last-7-days', label: 'Last 7 Days' },
    { value: 'last-month', label: 'Last Month' },
    { value: 'last-30-days', label: 'Last 30 Days' }
  ] as const;

  get items(): FormArray {
    return this.form.controls.items as FormArray;
  }

  ngOnInit(): void {
    this.reload();
    this.loadLookup();
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.pageNumber.set(1);
    this.reload();
  }

  onCustomerFilterChange(event: Event): void {
    this.customerFilter.set((event.target as HTMLSelectElement).value);
    this.pageNumber.set(1);
    this.reload();
  }

  onVesselFilterChange(event: Event): void {
    this.vesselFilter.set((event.target as HTMLSelectElement).value);
    this.pageNumber.set(1);
    this.reload();
  }

  onCreatedDatePresetChange(event: Event): void {
    this.createdDatePreset.set((event.target as HTMLSelectElement).value);
    this.pageNumber.set(1);
    this.reload();
  }

  changePage(page: number): void {
    this.pageNumber.set(page);
    this.reload();
  }

  openCreate(): void {
    this.editId.set(null);
    this.clearAllAttachmentState();
    this.form.reset({
      date: this.today(),
      poNumber: '',
      currency: 'MVR',
      customerId: '',
      hasVesselPayment: 'No',
      vesselId: '',
      vesselPaymentFee: 0,
      vesselPaymentInvoiceNumber: '',
      notes: ''
    });
    this.syncVesselValidation('No');
    this.form.setControl('items', this.fb.array([this.createItemForm()]));
    this.formOpen.set(true);
  }

  edit(note: DeliveryNoteListItem): void {
    this.api.getDeliveryNoteById(note.id).subscribe({
      next: (detail) => {
        this.editId.set(note.id);
        const vesselPaymentFee = Number(detail.vesselPaymentFee || detail.items.reduce((sum, item) => sum + Number(item.vesselPayment || 0), 0));
        const hasVesselPayment = vesselPaymentFee > 0 ? 'Yes' : 'No';
        this.form.reset({
          date: detail.date,
          poNumber: detail.poNumber || '',
          currency: detail.currency,
          customerId: detail.customerId,
          hasVesselPayment,
          vesselId: detail.vesselId || '',
          vesselPaymentFee,
          vesselPaymentInvoiceNumber: detail.vesselPaymentInvoiceNumber || '',
          notes: detail.notes || ''
        });
        this.currentPoAttachment.set(this.mapPoAttachment(detail));
        this.pendingPoAttachment.set(null);
        this.currentAttachment.set(this.mapAttachment(detail));
        this.pendingVesselPaymentAttachment.set(null);
        this.syncVesselValidation(hasVesselPayment);
        this.form.setControl('items', this.fb.array(detail.items.map((item) => this.createItemForm(item))));
        this.formOpen.set(true);
      },
      error: () => this.toast.error('Failed to load delivery note details.')
    });
  }

  viewDetail(note: DeliveryNoteListItem): void {
    this.api.getDeliveryNoteById(note.id).subscribe({
      next: (detail) => this.selectedNote.set(detail),
      error: () => this.toast.error('Failed to load delivery note details.')
    });
  }

  closeForm(): void {
    this.formOpen.set(false);
    this.vesselPaymentDialogOpen.set(false);
    this.clearAllAttachmentState();
  }

  addItem(): void {
    this.items.push(this.createItemForm());
  }

  removeItem(index: number): void {
    if (this.items.length <= 1) {
      return;
    }
    this.items.removeAt(index);
  }

  onVesselPaymentChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value === 'Yes' ? 'Yes' : 'No';
    this.syncVesselValidation(value);

    if (value === 'Yes') {
      this.openVesselPaymentDialog();
    }
  }

  onPoNumberChange(): void {
    if (!this.hasPoNumberValue()) {
      this.clearPendingPoAttachment();
      this.poAttachmentDragActive.set(false);
    }
  }

  save(): void {
    if (this.form.controls.hasVesselPayment.value === 'Yes' && !this.hasVesselPaymentDetails()) {
      this.form.controls.vesselPaymentFee.markAsTouched();
      this.form.controls.vesselPaymentInvoiceNumber.markAsTouched();
      this.openVesselPaymentDialog();
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error('Please fill all required delivery note fields.');
      return;
    }

    const raw = this.form.getRawValue();
    const poNumber = raw.poNumber?.trim() || null;
    if (!poNumber) {
      this.clearPendingPoAttachment();
    }

    const payload = {
      date: raw.date,
      poNumber,
      currency: raw.currency,
      customerId: raw.customerId,
      vesselId: raw.vesselId || null,
      vesselPaymentFee: Number(raw.vesselPaymentFee || 0),
      vesselPaymentInvoiceNumber: raw.vesselPaymentInvoiceNumber?.trim() || null,
      notes: raw.notes || null,
      items: raw.items.map((item) => ({
        details: item.details,
        qty: Number(item.qty),
        rate: Number(item.rate),
        cashPayment: item.cashPayment === 'Yes' ? 1 : 0,
        vesselPayment: 0
      }))
    };

    const request$ = this.editId()
      ? this.api.updateDeliveryNote(this.editId()!, payload)
      : this.api.createDeliveryNote(payload);
    const isEditing = !!this.editId();

    request$.subscribe({
      next: (detail) => {
        if (this.pendingVesselPaymentAttachment() || this.pendingPoAttachment()) {
          this.editId.set(detail.id);
          void this.uploadPendingAttachments(detail, isEditing);
          return;
        }

        this.completeSave(detail, `Delivery note ${isEditing ? 'updated' : 'created'} successfully.`);
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to save delivery note.'))
    });
  }

  createInvoice(note: DeliveryNoteListItem): void {
    if (note.cashPayment > 0) {
      this.toast.error('Cash-paid delivery notes do not require invoice.');
      return;
    }

    this.api.createInvoiceFromDeliveryNote(note.id, {}).subscribe({
      next: (result) => {
        this.toast.success(`Invoice ${result.invoiceNo} created.`);
        this.reload();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to create invoice.'))
    });
  }

  export(note: DeliveryNoteListItem): void {
    this.api.exportDeliveryNote(note.id).subscribe({
      next: (file) => this.download(file, `${note.deliveryNoteNo}.pdf`),
      error: () => this.toast.error('Failed to export delivery note PDF.')
    });
  }

  confirmDelete(note: DeliveryNoteListItem): void {
    this.deleteTarget.set(note);
    this.deleteDialogOpen.set(true);
  }

  openVesselPaymentDialog(): void {
    this.vesselPaymentDialogOpen.set(true);
  }

  closeVesselPaymentDialog(): void {
    this.vesselPaymentDialogOpen.set(false);
  }

  saveVesselPaymentDialog(): void {
    this.form.controls.vesselPaymentFee.markAsTouched();
    this.form.controls.vesselPaymentInvoiceNumber.markAsTouched();

    if (this.form.controls.vesselPaymentFee.invalid || this.form.controls.vesselPaymentInvoiceNumber.invalid) {
      return;
    }

    this.closeVesselPaymentDialog();
  }

  openPoAttachmentPicker(): void {
    if (this.attachmentUploading()) {
      return;
    }

    this.poAttachmentInput?.nativeElement.click();
  }

  onPoAttachmentPicked(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.item(0);
    if (!file) {
      return;
    }

    this.setPendingPoAttachment(file);
  }

  onPoAttachmentDragOver(event: DragEvent): void {
    event.preventDefault();
    if (this.attachmentUploading()) {
      return;
    }

    this.poAttachmentDragActive.set(true);
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'copy';
    }
  }

  onPoAttachmentDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.poAttachmentDragActive.set(false);
  }

  onPoAttachmentDrop(event: DragEvent): void {
    event.preventDefault();
    this.poAttachmentDragActive.set(false);

    if (this.attachmentUploading()) {
      return;
    }

    const file = event.dataTransfer?.files?.item(0);
    if (!file) {
      this.toast.error('Drop a PDF or image PO file.');
      return;
    }

    this.setPendingPoAttachment(file);
  }

  clearPendingPoAttachment(event?: Event): void {
    event?.preventDefault();
    event?.stopPropagation();
    this.pendingPoAttachment.set(null);
    this.resetPoAttachmentInput();
  }

  openVesselPaymentAttachmentPicker(): void {
    if (this.attachmentUploading()) {
      return;
    }

    this.vesselPaymentAttachmentInput?.nativeElement.click();
  }

  onVesselPaymentAttachmentPicked(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.item(0);
    if (!file) {
      return;
    }

    this.setPendingVesselPaymentAttachment(file);
  }

  onAttachmentDragOver(event: DragEvent): void {
    event.preventDefault();
    if (this.attachmentUploading()) {
      return;
    }

    this.attachmentDragActive.set(true);
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'copy';
    }
  }

  onAttachmentDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.attachmentDragActive.set(false);
  }

  onAttachmentDrop(event: DragEvent): void {
    event.preventDefault();
    this.attachmentDragActive.set(false);

    if (this.attachmentUploading()) {
      return;
    }

    const file = event.dataTransfer?.files?.item(0);
    if (!file) {
      this.toast.error('Drop a PDF or image invoice file.');
      return;
    }

    this.setPendingVesselPaymentAttachment(file);
  }

  clearPendingVesselPaymentAttachment(event?: Event): void {
    event?.preventDefault();
    event?.stopPropagation();
    this.pendingVesselPaymentAttachment.set(null);
    this.resetAttachmentInput();
  }

  closeDeleteDialog(): void {
    this.deleteDialogOpen.set(false);
    this.deleteTarget.set(null);
  }

  deleteNote(): void {
    const target = this.deleteTarget();
    if (!target) {
      return;
    }

    this.api.deleteDeliveryNote(target.id).subscribe({
      next: () => {
        this.toast.success('Delivery note deleted.');
        this.closeDeleteDialog();
        this.reload();
      },
      error: (error) => this.toast.error(this.readError(error, 'Unable to delete delivery note.'))
    });
  }

  openClearAllDialog(): void {
    this.clearAllForm.reset({ password: '' });
    this.clearAllDialogOpen.set(true);
  }

  closeClearAllDialog(): void {
    if (this.clearAllPending()) {
      return;
    }

    this.clearAllDialogOpen.set(false);
  }

  clearAllNotes(): void {
    if (this.clearAllForm.invalid) {
      this.clearAllForm.markAllAsTouched();
      return;
    }

    this.clearAllPending.set(true);
    const password = this.clearAllForm.controls.password.value;

    this.api.clearAllDeliveryNotes(password)
      .pipe(finalize(() => this.clearAllPending.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('All delivery notes deleted.');
          this.clearAllDialogOpen.set(false);
          this.selectedNote.set(null);
          this.pageNumber.set(1);
          this.reload();
          this.loadLookup();
        },
        error: (error) => this.toast.error(this.readError(error, 'Unable to clear delivery notes.'))
      });
  }

  isAdmin(): boolean {
    return this.auth.user()?.role === 'Admin';
  }

  reload(): void {
    this.api.getDeliveryNotes({
      pageNumber: this.pageNumber(),
      pageSize: 10,
      search: this.search(),
      createdDatePreset: this.createdDatePreset(),
      customerId: this.customerFilter(),
      vesselId: this.vesselFilter()
    }).subscribe({
      next: (result) => this.notes.set(result),
      error: () => this.toast.error('Failed to load delivery notes.')
    });
  }

  private loadLookup(): void {
    this.api.getCustomers({ pageNumber: 1, pageSize: 500 }).subscribe({
      next: (result) => this.customers.set(result.items),
      error: () => this.toast.error('Failed to load customers list.')
    });

    this.api.getAllVessels().subscribe({
      next: (vessels) => this.vessels.set(vessels),
      error: () => this.toast.error('Failed to load vessels list.')
    });
  }

  private createItemForm(item?: {
    details?: string;
    qty?: number;
    rate?: number;
    cashPayment?: number;
  }) {
    return this.fb.nonNullable.group({
      details: [item?.details || '', Validators.required],
      qty: [item?.qty ?? 1, [Validators.required, Validators.min(0.01)]],
      rate: [item?.rate ?? 0, [Validators.required, Validators.min(0)]],
      cashPayment: [(item?.cashPayment ?? 0) > 0 ? 'Yes' : 'No', Validators.required]
    });
  }

  private syncVesselValidation(value: 'Yes' | 'No'): void {
    const vesselControl = this.form.controls.vesselId;
    const vesselPaymentFeeControl = this.form.controls.vesselPaymentFee;
    const vesselPaymentInvoiceNumberControl = this.form.controls.vesselPaymentInvoiceNumber;
    if (value === 'Yes') {
      vesselControl.addValidators(Validators.required);
      vesselPaymentFeeControl.setValidators([Validators.required, Validators.min(0.01)]);
      vesselPaymentInvoiceNumberControl.setValidators([Validators.required, Validators.maxLength(100)]);
    } else {
      vesselControl.clearValidators();
      vesselPaymentFeeControl.clearValidators();
      vesselPaymentInvoiceNumberControl.clearValidators();
      vesselPaymentFeeControl.setValue(0);
      vesselPaymentInvoiceNumberControl.setValue('');
      this.vesselPaymentDialogOpen.set(false);
      this.clearPendingVesselPaymentAttachment();
      this.attachmentDragActive.set(false);
    }
    vesselControl.updateValueAndValidity();
    vesselPaymentFeeControl.updateValueAndValidity();
    vesselPaymentInvoiceNumberControl.updateValueAndValidity();
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  currentDateFilterLabel(): string {
    const selected = this.datePresetOptions.find((option) => option.value === this.createdDatePreset());
    return selected?.label ?? 'selected date range';
  }

  hasVesselPaymentDetails(): boolean {
    return Number(this.form.controls.vesselPaymentFee.value || 0) > 0
      && !!this.form.controls.vesselPaymentInvoiceNumber.value?.trim();
  }

  hasPoNumberValue(): boolean {
    return !!this.form.controls.poNumber.value?.trim();
  }

  pendingPoAttachmentLabel(): string | null {
    const file = this.pendingPoAttachment();
    if (!file) {
      return null;
    }

    return `${file.name} (${this.formatAttachmentSize(file.size)})`;
  }

  pendingAttachmentLabel(): string | null {
    const file = this.pendingVesselPaymentAttachment();
    if (!file) {
      return null;
    }

    return `${file.name} (${this.formatAttachmentSize(file.size)})`;
  }

  selectedAttachmentLabel(): string | null {
    return this.pendingAttachmentLabel()
      ?? (this.currentAttachment() ? `${this.currentAttachment()!.fileName} (${this.formatAttachmentSize(this.currentAttachment()!.sizeBytes)})` : null);
  }

  viewCurrentPoAttachment(event?: Event): void {
    event?.preventDefault();
    event?.stopPropagation();

    const noteId = this.editId() || this.selectedNote()?.id;
    if (!noteId) {
      return;
    }

    this.viewPoAttachment(noteId, this.currentPoAttachment()?.fileName);
  }

  viewCurrentAttachment(event?: Event): void {
    event?.preventDefault();
    event?.stopPropagation();

    const noteId = this.editId() || this.selectedNote()?.id;
    if (!noteId) {
      return;
    }

    this.viewVesselPaymentAttachment(noteId, this.currentAttachment()?.fileName);
  }

  viewPoAttachment(noteId: string, fileName?: string | null): void {
    this.api.viewDeliveryNotePoAttachment(noteId).subscribe({
      next: (file) => this.openInNewTab(file, fileName ?? 'purchase-order'),
      error: (error) => this.toast.error(this.readError(error, 'Failed to load PO attachment.'))
    });
  }

  viewVesselPaymentAttachment(noteId: string, fileName?: string | null): void {
    this.api.viewDeliveryNoteVesselPaymentAttachment(noteId).subscribe({
      next: (file) => this.openInNewTab(file, fileName ?? 'vessel-payment-invoice'),
      error: (error) => this.toast.error(this.readError(error, 'Failed to load vessel payment invoice attachment.'))
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

  private async uploadPendingAttachments(detail: DeliveryNote, isEditing: boolean): Promise<void> {
    this.attachmentUploading.set(true);

    try {
      let poAttachment = this.mapPoAttachment(detail);
      let vesselAttachment = this.mapAttachment(detail);
      const uploadedLabels: string[] = [];

      const pendingPoAttachment = this.pendingPoAttachment();
      if (pendingPoAttachment) {
        poAttachment = await firstValueFrom(this.api.uploadDeliveryNotePoAttachment(detail.id, pendingPoAttachment));
        this.currentPoAttachment.set(poAttachment);
        this.pendingPoAttachment.set(null);
        this.resetPoAttachmentInput();
        uploadedLabels.push('PO attachment');
      }

      const pendingVesselAttachment = this.pendingVesselPaymentAttachment();
      if (pendingVesselAttachment) {
        vesselAttachment = await firstValueFrom(this.api.uploadDeliveryNoteVesselPaymentAttachment(detail.id, pendingVesselAttachment));
        this.currentAttachment.set(vesselAttachment);
        this.pendingVesselPaymentAttachment.set(null);
        this.resetAttachmentInput();
        uploadedLabels.push('invoice attachment');
      }

      const attachmentSummary = uploadedLabels.length === 2
        ? 'attachments uploaded successfully.'
        : `${uploadedLabels[0]} uploaded successfully.`;

      this.completeSave(detail, `Delivery note ${isEditing ? 'updated' : 'created'} and ${attachmentSummary}`, vesselAttachment, poAttachment);
    } catch (error) {
      this.reload();
      this.toast.error(this.readError(error, 'Delivery note saved, but attachment upload failed. You can retry by saving again.'));
    } finally {
      this.attachmentUploading.set(false);
    }
  }

  private completeSave(
    detail: DeliveryNote,
    message: string,
    attachment?: DeliveryNoteAttachment | null,
    poAttachment?: DeliveryNoteAttachment | null): void
  {
    this.currentPoAttachment.set(poAttachment ?? this.mapPoAttachment(detail));
    this.currentAttachment.set(attachment ?? this.mapAttachment(detail));
    this.pendingPoAttachment.set(null);
    this.pendingVesselPaymentAttachment.set(null);
    this.resetPoAttachmentInput();
    this.resetAttachmentInput();
    this.toast.success(message);
    this.vesselPaymentDialogOpen.set(false);
    this.formOpen.set(false);
    this.reload();
  }

  private mapPoAttachment(detail: DeliveryNote): DeliveryNoteAttachment | null {
    if (!detail.hasPoAttachment
      || !detail.poAttachmentFileName
      || !detail.poAttachmentSizeBytes) {
      return null;
    }

    return {
      fileName: detail.poAttachmentFileName,
      contentType: detail.poAttachmentContentType || 'application/octet-stream',
      sizeBytes: detail.poAttachmentSizeBytes
    };
  }

  private mapAttachment(detail: DeliveryNote): DeliveryNoteAttachment | null {
    if (!detail.hasVesselPaymentInvoiceAttachment
      || !detail.vesselPaymentInvoiceAttachmentFileName
      || !detail.vesselPaymentInvoiceAttachmentSizeBytes) {
      return null;
    }

    return {
      fileName: detail.vesselPaymentInvoiceAttachmentFileName,
      contentType: detail.vesselPaymentInvoiceAttachmentContentType || 'application/octet-stream',
      sizeBytes: detail.vesselPaymentInvoiceAttachmentSizeBytes
    };
  }

  private setPendingPoAttachment(file: File): void {
    const error = this.validateAttachmentFile(file, 'PO');
    if (error) {
      this.toast.error(error);
      this.resetPoAttachmentInput();
      return;
    }

    this.pendingPoAttachment.set(file);
  }

  private setPendingVesselPaymentAttachment(file: File): void {
    const error = this.validateAttachmentFile(file, 'invoice');
    if (error) {
      this.toast.error(error);
      this.resetAttachmentInput();
      return;
    }

    this.pendingVesselPaymentAttachment.set(file);
  }

  private validateAttachmentFile(file: File, label: string): string | null {
    const maxSizeBytes = 10 * 1024 * 1024;
    const fileName = file.name.toLowerCase();
    const allowedExtensions = ['.pdf', '.png', '.jpg', '.jpeg', '.webp', '.gif'];
    const extension = allowedExtensions.find((candidate) => fileName.endsWith(candidate));

    if (!extension) {
      return `Supported ${label} formats are PDF, PNG, JPG, WEBP, and GIF.`;
    }

    if (file.size === 0) {
      return `Selected ${label} file is empty.`;
    }

    if (file.size > maxSizeBytes) {
      return `${label} attachment must be 10 MB or smaller.`;
    }

    return null;
  }

  private clearAllAttachmentState(): void {
    this.clearPoAttachmentState();
    this.clearAttachmentState();
  }

  private clearPoAttachmentState(): void {
    this.currentPoAttachment.set(null);
    this.pendingPoAttachment.set(null);
    this.poAttachmentDragActive.set(false);
    this.resetPoAttachmentInput();
  }

  private clearAttachmentState(): void {
    this.currentAttachment.set(null);
    this.pendingVesselPaymentAttachment.set(null);
    this.attachmentDragActive.set(false);
    this.resetAttachmentInput();
  }

  private resetPoAttachmentInput(): void {
    if (this.poAttachmentInput) {
      this.poAttachmentInput.nativeElement.value = '';
    }
  }

  private resetAttachmentInput(): void {
    if (this.vesselPaymentAttachmentInput) {
      this.vesselPaymentAttachmentInput.nativeElement.value = '';
    }
  }

  private openInNewTab(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const newWindow = window.open(url, '_blank', 'noopener');
    if (!newWindow) {
      this.download(blob, fileName);
      return;
    }

    window.setTimeout(() => URL.revokeObjectURL(url), 60000);
  }

  formatAttachmentSize(sizeBytes: number | null | undefined): string {
    const size = Number(sizeBytes || 0);
    if (size <= 0) {
      return '0 KB';
    }

    if (size >= 1024 * 1024) {
      return `${(size / (1024 * 1024)).toFixed(2)} MB`;
    }

    return `${Math.max(size / 1024, 0.01).toFixed(2)} KB`;
  }

  private readError(error: unknown, fallback: string): string {
    const apiError = error as {
      error?: {
        message?: string;
        errors?: string[];
        Message?: string;
        Errors?: string[];
      };
    };

    return apiError?.error?.errors?.[0]
      ?? apiError?.error?.Errors?.[0]
      ?? apiError?.error?.message
      ?? apiError?.error?.Message
      ?? fallback;
  }
}


