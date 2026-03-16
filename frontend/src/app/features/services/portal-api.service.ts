import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AccountStatement,
  BptAdjustmentRecord,
  BptCategoryLookup,
  BptExchangeRate,
  BptExpenseMapping,
  BptReport,
  BugReportRequest,
  Customer,
  DashboardAnalytics,
  DashboardSummary,
  DeliveryNote,
  DeliveryNoteAttachment,
  DeliveryNoteListItem,
  DocumentEmailRequest,
  ExpenseCategory,
  ExpenseCategoryLookup,
  ExpenseEntryDetail,
  ExpenseLedgerRow,
  ExpenseSummary,
  Invoice,
  InvoiceListItem,
  InvoicePayment,
  PaymentVoucher,
  PaymentVoucherListItem,
  PurchaseOrder,
  PurchaseOrderListItem,
  Quotation,
  QuotationConversionResult,
  QuotationListItem,
  ReceivedInvoice,
  ReceivedInvoiceAttachment,
  ReceivedInvoiceListItem,
  ReceivedInvoicePayment,
  RentEntry,
  RentEntryListItem,
  ReportExportRequest,
  PagedResult,
  PayrollEntry,
  PayrollPeriod,
  PayrollPeriodDetail,
  SalarySlip,
  SalesByVesselReport,
  SalesSummaryReport,
  SalesTransactionsReport,
  MiraReportExportRequest,
  MiraReportPreview,
  OtherIncomeEntryRecord,
  SalesAdjustmentRecord,
  Staff,
  StaffConductDetail,
  StaffConductListItem,
  StaffConductStaffOption,
  StaffConductSummary,
  Supplier,
  SupplierLookup,
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

  getMiraPreview(params: Record<string, unknown>): Observable<MiraReportPreview> {
    return this.api.get<MiraReportPreview>('mira/preview', params);
  }

  exportMiraExcel(payload: MiraReportExportRequest): Observable<Blob> {
    return this.api.postFile('mira/export/excel', payload);
  }

  exportMiraPdf(payload: MiraReportExportRequest): Observable<Blob> {
    return this.api.postFile('mira/export/pdf', payload);
  }

  getBptReport(params: Record<string, unknown>): Observable<BptReport> {
    return this.api.get<BptReport>('bpt/report', params);
  }

  exportBptExcel(payload: unknown): Observable<Blob> {
    return this.api.postFile('bpt/export/excel', payload);
  }

  exportBptPdf(payload: unknown): Observable<Blob> {
    return this.api.postFile('bpt/export/pdf', payload);
  }

  getBptCategories(): Observable<BptCategoryLookup[]> {
    return this.api.get<BptCategoryLookup[]>('bpt/categories');
  }

  getBptMappings(): Observable<BptExpenseMapping[]> {
    return this.api.get<BptExpenseMapping[]>('bpt/mappings');
  }

  updateBptMapping(expenseCategoryId: string, payload: unknown): Observable<BptExpenseMapping> {
    return this.api.put<BptExpenseMapping>(`bpt/mappings/${expenseCategoryId}`, payload);
  }

  getBptExchangeRates(params: Record<string, unknown> = {}): Observable<BptExchangeRate[]> {
    return this.api.get<BptExchangeRate[]>('bpt/exchange-rates', params);
  }

  createBptExchangeRate(payload: unknown): Observable<BptExchangeRate> {
    return this.api.post<BptExchangeRate>('bpt/exchange-rates', payload);
  }

  updateBptExchangeRate(id: string, payload: unknown): Observable<BptExchangeRate> {
    return this.api.put<BptExchangeRate>(`bpt/exchange-rates/${id}`, payload);
  }

  deleteBptExchangeRate(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`bpt/exchange-rates/${id}`);
  }

  getBptSalesAdjustments(params: Record<string, unknown> = {}): Observable<SalesAdjustmentRecord[]> {
    return this.api.get<SalesAdjustmentRecord[]>('bpt/sales-adjustments', params);
  }

  createBptSalesAdjustment(payload: unknown): Observable<SalesAdjustmentRecord> {
    return this.api.post<SalesAdjustmentRecord>('bpt/sales-adjustments', payload);
  }

  updateBptSalesAdjustment(id: string, payload: unknown): Observable<SalesAdjustmentRecord> {
    return this.api.put<SalesAdjustmentRecord>(`bpt/sales-adjustments/${id}`, payload);
  }

  deleteBptSalesAdjustment(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`bpt/sales-adjustments/${id}`);
  }

  getBptOtherIncome(params: Record<string, unknown> = {}): Observable<OtherIncomeEntryRecord[]> {
    return this.api.get<OtherIncomeEntryRecord[]>('bpt/other-income', params);
  }

  createBptOtherIncome(payload: unknown): Observable<OtherIncomeEntryRecord> {
    return this.api.post<OtherIncomeEntryRecord>('bpt/other-income', payload);
  }

  updateBptOtherIncome(id: string, payload: unknown): Observable<OtherIncomeEntryRecord> {
    return this.api.put<OtherIncomeEntryRecord>(`bpt/other-income/${id}`, payload);
  }

  deleteBptOtherIncome(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`bpt/other-income/${id}`);
  }

  getBptAdjustments(params: Record<string, unknown> = {}): Observable<BptAdjustmentRecord[]> {
    return this.api.get<BptAdjustmentRecord[]>('bpt/adjustments', params);
  }

  createBptAdjustment(payload: unknown): Observable<BptAdjustmentRecord> {
    return this.api.post<BptAdjustmentRecord>('bpt/adjustments', payload);
  }

  updateBptAdjustment(id: string, payload: unknown): Observable<BptAdjustmentRecord> {
    return this.api.put<BptAdjustmentRecord>(`bpt/adjustments/${id}`, payload);
  }

  deleteBptAdjustment(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`bpt/adjustments/${id}`);
  }

  getCustomers(params: Record<string, unknown>): Observable<PagedResult<Customer>> {
    return this.api.get<PagedResult<Customer>>('customers', params);
  }

  getSuppliers(params: Record<string, unknown>): Observable<PagedResult<Supplier>> {
    return this.api.get<PagedResult<Supplier>>('suppliers', params);
  }

  getSupplierLookup(): Observable<SupplierLookup[]> {
    return this.api.get<SupplierLookup[]>('suppliers/lookup');
  }

  createSupplier(payload: unknown): Observable<Supplier> {
    return this.api.post<Supplier>('suppliers', payload);
  }

  updateSupplier(id: string, payload: unknown): Observable<Supplier> {
    return this.api.put<Supplier>(`suppliers/${id}`, payload);
  }

  deleteSupplier(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`suppliers/${id}`);
  }

  getExpenseCategories(params: Record<string, unknown>): Observable<PagedResult<ExpenseCategory>> {
    return this.api.get<PagedResult<ExpenseCategory>>('expense-categories', params);
  }

  getExpenseCategoryLookup(): Observable<ExpenseCategoryLookup[]> {
    return this.api.get<ExpenseCategoryLookup[]>('expense-categories/lookup');
  }

  createExpenseCategory(payload: unknown): Observable<ExpenseCategory> {
    return this.api.post<ExpenseCategory>('expense-categories', payload);
  }

  updateExpenseCategory(id: string, payload: unknown): Observable<ExpenseCategory> {
    return this.api.put<ExpenseCategory>(`expense-categories/${id}`, payload);
  }

  deleteExpenseCategory(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`expense-categories/${id}`);
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

  uploadDeliveryNoteVesselPaymentAttachment(id: string, file: File): Observable<DeliveryNoteAttachment> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.api.post<DeliveryNoteAttachment>(`delivery-notes/${id}/vessel-payment-attachment`, formData);
  }

  uploadDeliveryNotePoAttachment(id: string, file: File): Observable<DeliveryNoteAttachment> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.api.post<DeliveryNoteAttachment>(`delivery-notes/${id}/po-attachment`, formData);
  }

  viewDeliveryNotePoAttachment(id: string): Observable<Blob> {
    return this.api.getFile(`delivery-notes/${id}/po-attachment`);
  }

  viewDeliveryNoteVesselPaymentAttachment(id: string): Observable<Blob> {
    return this.api.getFile(`delivery-notes/${id}/vessel-payment-attachment`);
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

  deleteInvoice(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`invoices/${id}`);
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

  sendInvoiceEmail(id: string, payload: DocumentEmailRequest): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>(`invoices/${id}/email`, payload);
  }

  getReceivedInvoices(params: Record<string, unknown>): Observable<PagedResult<ReceivedInvoiceListItem>> {
    return this.api.get<PagedResult<ReceivedInvoiceListItem>>('received-invoices', params);
  }

  getReceivedInvoiceById(id: string): Observable<ReceivedInvoice> {
    return this.api.get<ReceivedInvoice>(`received-invoices/${id}`);
  }

  createReceivedInvoice(payload: unknown): Observable<ReceivedInvoice> {
    return this.api.post<ReceivedInvoice>('received-invoices', payload);
  }

  updateReceivedInvoice(id: string, payload: unknown): Observable<ReceivedInvoice> {
    return this.api.put<ReceivedInvoice>(`received-invoices/${id}`, payload);
  }

  deleteReceivedInvoice(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`received-invoices/${id}`);
  }

  recordReceivedInvoicePayment(id: string, payload: unknown): Observable<ReceivedInvoicePayment> {
    return this.api.post<ReceivedInvoicePayment>(`received-invoices/${id}/payments`, payload);
  }

  uploadReceivedInvoiceAttachment(id: string, file: File): Observable<ReceivedInvoiceAttachment> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.api.post<ReceivedInvoiceAttachment>(`received-invoices/${id}/attachments`, formData);
  }

  viewReceivedInvoiceAttachment(id: string, attachmentId: string): Observable<Blob> {
    return this.api.getFile(`received-invoices/${id}/attachments/${attachmentId}`);
  }

  getPaymentVouchers(params: Record<string, unknown>): Observable<PagedResult<PaymentVoucherListItem>> {
    return this.api.get<PagedResult<PaymentVoucherListItem>>('payment-vouchers', params);
  }

  getPaymentVoucherById(id: string): Observable<PaymentVoucher> {
    return this.api.get<PaymentVoucher>(`payment-vouchers/${id}`);
  }

  createPaymentVoucher(payload: unknown): Observable<PaymentVoucher> {
    return this.api.post<PaymentVoucher>('payment-vouchers', payload);
  }

  updatePaymentVoucher(id: string, payload: unknown): Observable<PaymentVoucher> {
    return this.api.put<PaymentVoucher>(`payment-vouchers/${id}`, payload);
  }

  approvePaymentVoucher(id: string, notes?: string): Observable<PaymentVoucher> {
    return this.api.post<PaymentVoucher>(`payment-vouchers/${id}/approve`, { notes: notes ?? null });
  }

  postPaymentVoucher(id: string, notes?: string): Observable<PaymentVoucher> {
    return this.api.post<PaymentVoucher>(`payment-vouchers/${id}/post`, { notes: notes ?? null });
  }

  cancelPaymentVoucher(id: string, notes?: string): Observable<PaymentVoucher> {
    return this.api.post<PaymentVoucher>(`payment-vouchers/${id}/cancel`, { notes: notes ?? null });
  }

  deletePaymentVoucher(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`payment-vouchers/${id}`);
  }

  exportPaymentVoucher(id: string): Observable<Blob> {
    return this.api.getFile(`payment-vouchers/${id}/export`);
  }

  getExpenseLedger(params: Record<string, unknown>): Observable<PagedResult<ExpenseLedgerRow>> {
    return this.api.get<PagedResult<ExpenseLedgerRow>>('expenses/ledger', params);
  }

  getExpenseSummary(params: Record<string, unknown>): Observable<ExpenseSummary> {
    return this.api.get<ExpenseSummary>('expenses/summary', params);
  }

  exportExpenseLedgerPdf(params: Record<string, unknown>): Observable<Blob> {
    return this.api.getFile('expenses/export/pdf', params);
  }

  exportExpenseLedgerExcel(params: Record<string, unknown>): Observable<Blob> {
    return this.api.getFile('expenses/export/excel', params);
  }

  getExpenseEntryById(id: string): Observable<ExpenseEntryDetail> {
    return this.api.get<ExpenseEntryDetail>(`expenses/manual/${id}`);
  }

  createExpenseEntry(payload: unknown): Observable<ExpenseEntryDetail> {
    return this.api.post<ExpenseEntryDetail>('expenses/manual', payload);
  }

  updateExpenseEntry(id: string, payload: unknown): Observable<ExpenseEntryDetail> {
    return this.api.put<ExpenseEntryDetail>(`expenses/manual/${id}`, payload);
  }

  deleteExpenseEntry(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`expenses/manual/${id}`);
  }

  getRentEntries(params: Record<string, unknown>): Observable<PagedResult<RentEntryListItem>> {
    return this.api.get<PagedResult<RentEntryListItem>>('rent', params);
  }

  getRentEntryById(id: string): Observable<RentEntry> {
    return this.api.get<RentEntry>(`rent/${id}`);
  }

  createRentEntry(payload: unknown): Observable<RentEntry> {
    return this.api.post<RentEntry>('rent', payload);
  }

  updateRentEntry(id: string, payload: unknown): Observable<RentEntry> {
    return this.api.put<RentEntry>(`rent/${id}`, payload);
  }

  deleteRentEntry(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`rent/${id}`);
  }

  getPurchaseOrders(params: Record<string, unknown>): Observable<PagedResult<PurchaseOrderListItem>> {
    return this.api.get<PagedResult<PurchaseOrderListItem>>('purchase-orders', params);
  }

  getPurchaseOrderById(id: string): Observable<PurchaseOrder> {
    return this.api.get<PurchaseOrder>(`purchase-orders/${id}`);
  }

  createPurchaseOrder(payload: unknown): Observable<PurchaseOrder> {
    return this.api.post<PurchaseOrder>('purchase-orders', payload);
  }

  updatePurchaseOrder(id: string, payload: unknown): Observable<PurchaseOrder> {
    return this.api.put<PurchaseOrder>(`purchase-orders/${id}`, payload);
  }

  deletePurchaseOrder(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`purchase-orders/${id}`);
  }

  exportPurchaseOrder(id: string): Observable<Blob> {
    return this.api.getFile(`purchase-orders/${id}/export`);
  }

  sendPurchaseOrderEmail(id: string, payload: DocumentEmailRequest): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>(`purchase-orders/${id}/email`, payload);
  }

  getQuotes(params: Record<string, unknown>): Observable<PagedResult<QuotationListItem>> {
    return this.api.get<PagedResult<QuotationListItem>>('quotes', params);
  }

  getQuoteById(id: string): Observable<Quotation> {
    return this.api.get<Quotation>(`quotes/${id}`);
  }

  createQuote(payload: unknown): Observable<Quotation> {
    return this.api.post<Quotation>('quotes', payload);
  }

  updateQuote(id: string, payload: unknown): Observable<Quotation> {
    return this.api.put<Quotation>(`quotes/${id}`, payload);
  }

  convertQuoteToSale(id: string): Observable<QuotationConversionResult> {
    return this.api.post<QuotationConversionResult>(`quotes/${id}/convert-to-sale`, {});
  }

  deleteQuote(id: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`quotes/${id}`);
  }

  exportQuote(id: string): Observable<Blob> {
    return this.api.getFile(`quotes/${id}/export`);
  }

  sendQuoteEmail(id: string, payload: DocumentEmailRequest): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>(`quotes/${id}/email`, payload);
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

  getStaffConductForms(params: Record<string, unknown>): Observable<PagedResult<StaffConductListItem>> {
    return this.api.get<PagedResult<StaffConductListItem>>('staff-conduct', params);
  }

  getStaffConductSummary(params: Record<string, unknown>): Observable<StaffConductSummary> {
    return this.api.get<StaffConductSummary>('staff-conduct/summary', params);
  }

  getStaffConductStaffOptions(): Observable<StaffConductStaffOption[]> {
    return this.api.get<StaffConductStaffOption[]>('staff-conduct/staff-options');
  }

  getStaffConductFormById(id: string): Observable<StaffConductDetail> {
    return this.api.get<StaffConductDetail>(`staff-conduct/${id}`);
  }

  createStaffConductForm(payload: unknown): Observable<StaffConductDetail> {
    return this.api.post<StaffConductDetail>('staff-conduct', payload);
  }

  updateStaffConductForm(id: string, payload: unknown): Observable<StaffConductDetail> {
    return this.api.put<StaffConductDetail>(`staff-conduct/${id}`, payload);
  }

  exportStaffConductFormPdf(id: string): Observable<Blob> {
    return this.api.getFile(`staff-conduct/${id}/export/pdf`);
  }

  exportStaffConductFormExcel(id: string): Observable<Blob> {
    return this.api.getFile(`staff-conduct/${id}/export/excel`);
  }

  exportStaffConductSummaryPdf(params: Record<string, unknown>): Observable<Blob> {
    return this.api.getFile('staff-conduct/export/pdf', params);
  }

  exportStaffConductSummaryExcel(params: Record<string, unknown>): Observable<Blob> {
    return this.api.getFile('staff-conduct/export/excel', params);
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

  uploadSettingsStamp(file: File): Observable<TenantLogoUpload> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.api.post<TenantLogoUpload>('settings/stamp-upload', formData);
  }

  uploadSettingsSignature(file: File): Observable<TenantLogoUpload> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.api.post<TenantLogoUpload>('settings/signature-upload', formData);
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
