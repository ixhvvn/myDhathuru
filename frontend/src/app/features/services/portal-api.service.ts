import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AccountStatement,
  BugReportRequest,
  Customer,
  DashboardAnalytics,
  DashboardSummary,
  DeliveryNote,
  DeliveryNoteListItem,
  Invoice,
  InvoiceListItem,
  InvoicePayment,
  ReportExportRequest,
  PagedResult,
  PayrollEntry,
  PayrollPeriod,
  PayrollPeriodDetail,
  SalarySlip,
  SalesByVesselReport,
  SalesSummaryReport,
  SalesTransactionsReport,
  Staff,
  TenantLogoUpload,
  TenantSettings,
  Vessel
} from '../../core/models/app.models';
import { ApiService } from '../../core/services/api.service';

@Injectable({ providedIn: 'root' })
export class PortalApiService {
  private readonly api = inject(ApiService);

  getDashboardSummary(): Observable<DashboardSummary> {
    return this.api.get<DashboardSummary>('dashboard/summary');
  }

  getDashboardAnalytics(topCustomers = 5): Observable<DashboardAnalytics> {
    return this.api.get<DashboardAnalytics>('dashboard/analytics', { topCustomers });
  }

  getSalesSummaryReport(params: Record<string, unknown>): Observable<SalesSummaryReport> {
    return this.api.get<SalesSummaryReport>('reports/sales-summary', params);
  }

  getSalesTransactionsReport(params: Record<string, unknown>): Observable<SalesTransactionsReport> {
    return this.api.get<SalesTransactionsReport>('reports/sales-transactions', params);
  }

  getSalesByVesselReport(params: Record<string, unknown>): Observable<SalesByVesselReport> {
    return this.api.get<SalesByVesselReport>('reports/sales-by-vessel', params);
  }

  exportReportExcel(payload: ReportExportRequest): Observable<Blob> {
    return this.api.postFile('reports/export/excel', payload);
  }

  exportReportPdf(payload: ReportExportRequest): Observable<Blob> {
    return this.api.postFile('reports/export/pdf', payload);
  }

  getCustomers(params: Record<string, unknown>): Observable<PagedResult<Customer>> {
    return this.api.get<PagedResult<Customer>>('customers', params);
  }

  getCustomerById(id: string): Observable<Customer> {
    return this.api.get<Customer>(`customers/${id}`);
  }

  exportCustomersPdf(params: Record<string, unknown>): Observable<Blob> {
    return this.api.getFile('customers/export/pdf', params);
  }

  exportCustomersExcel(params: Record<string, unknown>): Observable<Blob> {
    return this.api.getFile('customers/export/excel', params);
  }

  createCustomer(payload: Partial<Customer>): Observable<Customer> {
    return this.api.post<Customer>('customers', payload);
  }

  updateCustomer(id: string, payload: Partial<Customer>): Observable<Customer> {
    return this.api.put<Customer>(`customers/${id}`, payload);
  }

  deleteCustomer(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`customers/${id}`);
  }

  getVessels(params: Record<string, unknown>): Observable<PagedResult<Vessel>> {
    return this.api.get<PagedResult<Vessel>>('vessels', params);
  }

  getAllVessels(): Observable<Vessel[]> {
    return this.api.get<Vessel[]>('vessels/all');
  }

  createVessel(payload: Partial<Vessel>): Observable<Vessel> {
    return this.api.post<Vessel>('vessels', payload);
  }

  deleteVessel(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`vessels/${id}`);
  }

  getDeliveryNotes(params: Record<string, unknown>): Observable<PagedResult<DeliveryNoteListItem>> {
    return this.api.get<PagedResult<DeliveryNoteListItem>>('delivery-notes', params);
  }

  getDeliveryNoteById(id: string): Observable<DeliveryNote> {
    return this.api.get<DeliveryNote>(`delivery-notes/${id}`);
  }

  createDeliveryNote(payload: unknown): Observable<DeliveryNote> {
    return this.api.post<DeliveryNote>('delivery-notes', payload);
  }

  updateDeliveryNote(id: string, payload: unknown): Observable<DeliveryNote> {
    return this.api.put<DeliveryNote>(`delivery-notes/${id}`, payload);
  }

  deleteDeliveryNote(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`delivery-notes/${id}`);
  }

  clearAllDeliveryNotes(password: string): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>('delivery-notes/clear-all', { password });
  }

  createInvoiceFromDeliveryNote(id: string, payload: unknown): Observable<{ invoiceId: string; invoiceNo: string }> {
    return this.api.post<{ invoiceId: string; invoiceNo: string }>(`delivery-notes/${id}/create-invoice`, payload);
  }

  exportDeliveryNote(id: string): Observable<Blob> {
    return this.api.getFile(`delivery-notes/${id}/export`);
  }

  getInvoices(params: Record<string, unknown>): Observable<PagedResult<InvoiceListItem>> {
    return this.api.get<PagedResult<InvoiceListItem>>('invoices', params);
  }

  getInvoiceById(id: string): Observable<Invoice> {
    return this.api.get<Invoice>(`invoices/${id}`);
  }

  createInvoice(payload: unknown): Observable<Invoice> {
    return this.api.post<Invoice>('invoices', payload);
  }

  updateInvoice(id: string, payload: unknown): Observable<Invoice> {
    return this.api.put<Invoice>(`invoices/${id}`, payload);
  }

  receiveInvoicePayment(id: string, payload: unknown): Observable<InvoicePayment> {
    return this.api.post<InvoicePayment>(`invoices/${id}/receive-payment`, payload);
  }

  clearAllInvoices(password: string): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>('invoices/clear-all', { password });
  }

  getInvoicePayments(id: string): Observable<InvoicePayment[]> {
    return this.api.get<InvoicePayment[]>(`invoices/${id}/payments`);
  }

  exportInvoice(id: string): Observable<Blob> {
    return this.api.getFile(`invoices/${id}/export`);
  }

  getStatement(customerId: string, year: number): Observable<AccountStatement> {
    return this.api.get<AccountStatement>('account-statements', { customerId, year });
  }

  saveOpeningBalance(payload: unknown): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>('account-statements/opening-balance', payload);
  }

  exportStatement(customerId: string, year: number): Observable<Blob> {
    return this.api.getFile('account-statements/export', { customerId, year });
  }

  getStaff(params: Record<string, unknown>): Observable<PagedResult<Staff>> {
    return this.api.get<PagedResult<Staff>>('payroll/staff', params);
  }

  createStaff(payload: unknown): Observable<Staff> {
    return this.api.post<Staff>('payroll/staff', payload);
  }

  updateStaff(id: string, payload: unknown): Observable<Staff> {
    return this.api.put<Staff>(`payroll/staff/${id}`, payload);
  }

  deleteStaff(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`payroll/staff/${id}`);
  }

  getPayrollPeriods(): Observable<PayrollPeriod[]> {
    return this.api.get<PayrollPeriod[]>('payroll/periods');
  }

  createPayrollPeriod(payload: unknown): Observable<PayrollPeriodDetail> {
    return this.api.post<PayrollPeriodDetail>('payroll/periods', payload);
  }

  getPayrollPeriod(id: string): Observable<PayrollPeriodDetail> {
    return this.api.get<PayrollPeriodDetail>(`payroll/periods/${id}`);
  }

  deletePayrollPeriod(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`payroll/periods/${id}`);
  }

  recalculatePayrollPeriod(id: string): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>(`payroll/periods/${id}/recalculate`, {});
  }

  updatePayrollEntry(periodId: string, entryId: string, payload: unknown): Observable<PayrollEntry> {
    return this.api.put<PayrollEntry>(`payroll/periods/${periodId}/entries/${entryId}`, payload);
  }

  generateSalarySlip(entryId: string): Observable<SalarySlip> {
    return this.api.post<SalarySlip>(`payroll/entries/${entryId}/salary-slip`, {});
  }

  getSalarySlip(entryId: string): Observable<SalarySlip> {
    return this.api.get<SalarySlip>(`payroll/entries/${entryId}/salary-slip`);
  }

  exportSalarySlip(entryId: string): Observable<Blob> {
    return this.api.getFile(`payroll/entries/${entryId}/salary-slip/export`);
  }

  exportPayrollPeriodPdf(periodId: string): Observable<Blob> {
    return this.api.getFile(`payroll/periods/${periodId}/export/pdf`);
  }

  exportPayrollPeriodExcel(periodId: string): Observable<Blob> {
    return this.api.getFile(`payroll/periods/${periodId}/export/excel`);
  }

  getSettings(): Observable<TenantSettings> {
    return this.api.get<TenantSettings>('settings');
  }

  updateSettings(payload: unknown): Observable<TenantSettings> {
    return this.api.put<TenantSettings>('settings', payload);
  }

  uploadSettingsLogo(file: File): Observable<TenantLogoUpload> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.api.post<TenantLogoUpload>('settings/logo-upload', formData);
  }

  changePassword(payload: unknown): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>('settings/change-password', payload);
  }

  reportBug(payload: BugReportRequest, attachment?: File | null): Observable<Record<string, never>> {
    const formData = new FormData();
    formData.append('subject', payload.subject);
    formData.append('description', payload.description);

    if (payload.pageUrl) {
      formData.append('pageUrl', payload.pageUrl);
    }

    if (attachment) {
      formData.append('attachment', attachment, attachment.name);
    }

    return this.api.post<Record<string, never>>('support/report-bug', formData);
  }
}
