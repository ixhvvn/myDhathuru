export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  errors?: string[];
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface UserProfile {
  id: string;
  tenantId: string;
  fullName: string;
  email: string;
  role: 'Admin' | 'Staff' | 'SuperAdmin';
  companyName: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: UserProfile;
}

export interface SignupRequestSubmitted {
  requestId: string;
  status: 'Pending' | 'Accepted' | 'Rejected';
}

export interface DashboardSummary {
  currentMonthInvoices: number;
  lastMonthInvoices: number;
  invoicesTrend: DashboardTrend;

  currentMonthSales: DashboardCurrencyAmount;
  lastMonthSales: DashboardCurrencyAmount;
  salesTrend: DashboardCurrencyTrend;

  currentMonthPending: DashboardCurrencyAmount;
  lastMonthPending: DashboardCurrencyAmount;
  pendingTrend: DashboardCurrencyTrend;

  currentMonthDeliveryNotes: number;
  lastMonthDeliveryNotes: number;
  deliveryNotesTrend: DashboardTrend;

  currentMonthNewCustomers: number;
  lastMonthNewCustomers: number;
  newCustomersTrend: DashboardTrend;

  currentMonthPayroll: number;
  lastMonthPayroll: number;
  payrollTrend: DashboardTrend;
}

export interface DashboardCurrencyAmount {
  mvr: number;
  usd: number;
}

export interface DashboardTrend {
  percentage?: number | null;
  direction: 'up' | 'down' | 'neutral';
  label: string;
}

export interface DashboardCurrencyTrend {
  mvr: DashboardTrend;
  usd: DashboardTrend;
}

export interface DashboardTopCustomer {
  rank: number;
  customerId: string;
  customerName: string;
  salesMvr: number;
  salesUsd: number;
  invoiceCount: number;
  contributionMvrPercentage: number;
  contributionUsdPercentage: number;
  initials: string;
}

export interface DashboardMonthlySales {
  year: number;
  month: number;
  label: string;
  salesMvr: number;
  salesUsd: number;
}

export interface DashboardVesselSales {
  vesselId?: string;
  vesselName: string;
  salesMvr: number;
  salesUsd: number;
  contributionMvrPercentage: number;
  contributionUsdPercentage: number;
}

export interface DashboardAnalytics {
  summary: DashboardSummary;
  topCustomers: DashboardTopCustomer[];
  salesLast6Months: DashboardMonthlySales[];
  vesselSales: DashboardVesselSales[];
}

export enum ReportType {
  SalesSummary = 1,
  SalesTransactions = 2,
  SalesByVessel = 3
}

export enum ReportDatePreset {
  Today = 1,
  Yesterday = 2,
  Last7Days = 3,
  LastWeek = 4,
  Last30Days = 5,
  LastMonth = 6,
  ThisYear = 7,
  CustomRange = 8
}

export interface ReportMeta {
  datePreset: string;
  rangeStartUtc: string;
  rangeEndUtc: string;
  customerFilterLabel: string;
  generatedAtUtc: string;
}

export interface SalesSummaryDailyRow {
  date: string;
  invoiceCount: number;
  salesMvr: number;
  salesUsd: number;
  receivedMvr: number;
  receivedUsd: number;
  pendingMvr: number;
  pendingUsd: number;
}

export interface SalesSummaryReport {
  meta: ReportMeta;
  totalInvoices: number;
  totalSales: DashboardCurrencyAmount;
  totalReceived: DashboardCurrencyAmount;
  totalPending: DashboardCurrencyAmount;
  totalCustomers: number;
  totalTax: DashboardCurrencyAmount;
  rows: SalesSummaryDailyRow[];
}

export interface SalesTransactionRow {
  invoiceNo: string;
  dateIssued: string;
  customer: string;
  vessel: string;
  description: string;
  currency: string;
  amount: number;
  paymentStatus: PaymentStatus;
  paymentMethod: string;
  receivedOn?: string;
  balance: number;
}

export interface SalesTransactionsReport {
  meta: ReportMeta;
  totalTransactions: number;
  totalSales: DashboardCurrencyAmount;
  totalReceived: DashboardCurrencyAmount;
  totalPending: DashboardCurrencyAmount;
  rows: SalesTransactionRow[];
}

export interface SalesByVesselRow {
  vessel: string;
  currency: string;
  transactionCount: number;
  totalSales: number;
  totalReceived: number;
  pendingAmount: number;
  percentageOfCurrencySales: number;
}

export interface SalesByVesselReport {
  meta: ReportMeta;
  totalTransactions: number;
  totalSales: DashboardCurrencyAmount;
  totalReceived: DashboardCurrencyAmount;
  totalPending: DashboardCurrencyAmount;
  rows: SalesByVesselRow[];
}

export interface ReportExportRequest {
  reportType: ReportType;
  datePreset: ReportDatePreset;
  customStartDate?: string;
  customEndDate?: string;
  customerId?: string;
}

export enum MiraReportType {
  InputTaxStatement = 1,
  OutputTaxStatement = 2,
  BptIncomeStatement = 3,
  BptNotes = 4
}

export enum MiraPeriodMode {
  Quarter = 1,
  Year = 2,
  CustomRange = 3
}

export interface MiraReportQuery {
  reportType: MiraReportType;
  periodMode: MiraPeriodMode;
  year: number;
  quarter?: number;
  customStartDate?: string;
  customEndDate?: string;
}

export interface MiraReportExportRequest extends MiraReportQuery {
}

export interface MiraReportMeta {
  title: string;
  periodLabel: string;
  rangeStart: string;
  rangeEnd: string;
  generatedAtUtc: string;
  taxableActivityNumber: string;
  companyName: string;
  companyTinNumber: string;
  hasUsdTransactions: boolean;
  unconvertedUsdRevenue: number;
  unconvertedUsdExpenses: number;
}

export interface MiraInputTaxRow {
  supplierTin: string;
  supplierName: string;
  supplierInvoiceNumber: string;
  invoiceDate: string;
  currency: string;
  invoiceTotalExcludingGst: number;
  gstChargedAt6: number;
  gstChargedAt8: number;
  gstChargedAt12: number;
  gstChargedAt16: number;
  gstChargedAt17: number;
  totalGst: number;
  taxableActivityNumber: string;
  revenueCapitalClassification: 'Revenue' | 'Capital';
}

export interface MiraInputTaxStatement {
  totalInvoices: number;
  totalInvoiceBase: number;
  totalGst6: number;
  totalGst8: number;
  totalGst12: number;
  totalGst16: number;
  totalGst17: number;
  totalClaimableGst: number;
  rows: MiraInputTaxRow[];
}

export interface MiraOutputTaxRow {
  customerTin: string;
  customerName: string;
  invoiceNo: string;
  invoiceDate: string;
  currency: string;
  taxableSupplies: number;
  zeroRatedSupplies: number;
  exemptSupplies: number;
  outOfScopeSupplies: number;
  gstRate: number;
  gstAmount: number;
  taxableActivityNumber: string;
}

export interface MiraOutputTaxStatement {
  totalInvoices: number;
  totalTaxableSupplies: number;
  totalZeroRatedSupplies: number;
  totalExemptSupplies: number;
  totalOutOfScopeSupplies: number;
  totalTaxAmount: number;
  rows: MiraOutputTaxRow[];
}

export interface BptIncomeLine {
  label: string;
  amount: number;
}

export interface BptIncomeStatement {
  grossSales: number;
  salesReturnsAndAllowances: number;
  netSales: number;
  costOfGoodsSoldLines: BptIncomeLine[];
  costOfGoodsSold: number;
  grossProfit: number;
  operatingExpenses: BptIncomeLine[];
  totalOperatingExpenses: number;
  netOperatingIncome: number;
  otherIncome: BptIncomeLine[];
  totalOtherIncome: number;
  netIncome: number;
}

export interface BptSalaryNoteRow {
  staffCode: string;
  staffName: string;
  averageBasicPerPeriod: number;
  averageAllowancePerPeriod: number;
  periodCount: number;
  totalForPeriodRange: number;
}

export interface BptExpenseNoteRow {
  date: string;
  documentNumber: string;
  payeeName: string;
  detail: string;
  amount: number;
}

export interface BptExpenseNoteSection {
  title: string;
  categoryCode: number | string;
  totalAmount: number;
  rows: BptExpenseNoteRow[];
}

export interface BptNotes {
  salaryRows: BptSalaryNoteRow[];
  totalSalary: number;
  sections: BptExpenseNoteSection[];
}

export interface MiraReportPreview {
  reportType: MiraReportType | string;
  meta: MiraReportMeta;
  assumptions: string[];
  inputTaxStatement?: MiraInputTaxStatement;
  outputTaxStatement?: MiraOutputTaxStatement;
  bptIncomeStatement?: BptIncomeStatement;
  bptNotes?: BptNotes;
}

export enum BptPeriodMode {
  Quarter = 1,
  Year = 2,
  CustomRange = 3
}

export type BptClassificationGroup =
  'Revenue'
  | 'SalesReturnAllowance'
  | 'OtherIncome'
  | 'CostOfGoodsSold'
  | 'OperatingExpense'
  | 'Excluded';

export type BptSourceModule =
  'Invoice'
  | 'SalesAdjustment'
  | 'OtherIncome'
  | 'ReceivedInvoice'
  | 'ExpenseEntry'
  | 'RentEntry'
  | 'Payroll'
  | 'BptAdjustment';

export type SalesAdjustmentType = 'Return' | 'Allowance';

export interface BptReportMeta {
  title: string;
  periodLabel: string;
  rangeStart: string;
  rangeEnd: string;
  generatedAtUtc: string;
  taxableActivityNumber: string;
  companyName: string;
  companyTinNumber: string;
}

export interface BptTraceTransaction {
  sourceDocumentId?: string;
  sourceModule: BptSourceModule;
  sourceDocumentNumber: string;
  transactionDate: string;
  financialYear: number;
  counterpartyName: string;
  description: string;
  currency: string;
  exchangeRate: number;
  amountOriginal: number;
  amountMvr: number;
  sourceStatus: string;
  classificationGroup: BptClassificationGroup;
  bptCategoryId?: string;
  bptCategoryCode: string;
  bptCategoryName: string;
  isAdjustment: boolean;
  notes?: string;
}

export interface BptReport {
  meta: BptReportMeta;
  importantNotes: string[];
  statement: BptIncomeStatement;
  notes: BptNotes;
  transactions: BptTraceTransaction[];
}

export interface BptCategoryLookup {
  id: string;
  name: string;
  code: string;
  classificationGroup: BptClassificationGroup;
}

export interface BptExpenseMapping {
  id?: string;
  expenseCategoryId: string;
  expenseCategoryName: string;
  expenseCategoryCode: string;
  bptCategoryId: string;
  bptCategoryCode: string;
  bptCategoryName: string;
  classificationGroup: BptClassificationGroup;
  sourceModule?: BptSourceModule;
  isSystem: boolean;
  isActive: boolean;
  notes?: string;
}

export interface BptExchangeRate {
  id: string;
  rateDate: string;
  currency: string;
  rateToMvr: number;
  source?: string;
  notes?: string;
  isActive: boolean;
}

export interface SalesAdjustmentRecord {
  id: string;
  adjustmentNumber: string;
  adjustmentType: SalesAdjustmentType;
  transactionDate: string;
  relatedInvoiceId?: string;
  relatedInvoiceNumber?: string;
  customerId?: string;
  customerName?: string;
  currency: string;
  exchangeRate: number;
  amountOriginal: number;
  amountMvr: number;
  approvalStatus: ApprovalStatus;
  notes?: string;
}

export interface OtherIncomeEntryRecord {
  id: string;
  entryNumber: string;
  transactionDate: string;
  customerId?: string;
  counterpartyName?: string;
  description: string;
  currency: string;
  exchangeRate: number;
  amountOriginal: number;
  amountMvr: number;
  approvalStatus: ApprovalStatus;
  notes?: string;
}

export interface BptAdjustmentRecord {
  id: string;
  adjustmentNumber: string;
  transactionDate: string;
  description: string;
  bptCategoryId: string;
  bptCategoryName: string;
  bptCategoryCode: string;
  classificationGroup: BptClassificationGroup;
  currency: string;
  exchangeRate: number;
  amountOriginal: number;
  amountMvr: number;
  approvalStatus: ApprovalStatus;
  notes?: string;
}

export interface BugReportRequest {
  subject: string;
  description: string;
  pageUrl?: string;
}

export interface Customer {
  id: string;
  name: string;
  tinNumber?: string;
  phone?: string;
  email?: string;
  references: string[];
}

export interface Vessel {
  id: string;
  name: string;
  registrationNumber?: string;
  issuedDate?: string;
  passengerCapacity?: number;
  vesselType?: string;
  homePort?: string;
  ownerName?: string;
  contactPhone?: string;
  notes?: string;
}

export interface DeliveryNoteItem {
  id?: string;
  details: string;
  qty: number;
  rate: number;
  total?: number;
  cashPayment: number;
  vesselPayment: number;
}

export interface DeliveryNote {
  id: string;
  deliveryNoteNo: string;
  poNumber?: string;
  hasPoAttachment: boolean;
  poAttachmentFileName?: string;
  poAttachmentContentType?: string;
  poAttachmentSizeBytes?: number;
  date: string;
  currency: string;
  customerId: string;
  customerName: string;
  vesselId?: string;
  vesselName?: string;
  vesselPaymentFee: number;
  vesselPaymentInvoiceNumber?: string;
  hasVesselPaymentInvoiceAttachment: boolean;
  vesselPaymentInvoiceAttachmentFileName?: string;
  vesselPaymentInvoiceAttachmentContentType?: string;
  vesselPaymentInvoiceAttachmentSizeBytes?: number;
  notes?: string;
  invoiceNo?: string;
  invoiceId?: string;
  totalAmount: number;
  items: DeliveryNoteItem[];
}

export interface DeliveryNoteListItem {
  id: string;
  deliveryNoteNo: string;
  poNumber?: string;
  hasPoAttachment: boolean;
  poAttachmentFileName?: string;
  date: string;
  currency: string;
  details: string;
  qty: number;
  customer: string;
  vessel?: string;
  rate: number;
  total: number;
  invoiceNo?: string;
  cashPayment: number;
  vesselPayment: number;
  vesselPaymentInvoiceNumber?: string;
  hasVesselPaymentInvoiceAttachment: boolean;
  vesselPaymentInvoiceAttachmentFileName?: string;
}

export interface DeliveryNoteAttachment {
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export type PaymentStatus = 'Unpaid' | 'Partial' | 'Paid';
export type PaymentMethod = 'Cash' | 'Card' | 'Transfer' | 'Cheque';
export type ApprovalStatus = 'Draft' | 'Approved' | 'Rejected';
export type ReceivedInvoiceStatus = 'Unpaid' | 'Partial' | 'Paid' | 'Overdue';
export type PaymentVoucherStatus = 'Draft' | 'Approved' | 'Posted' | 'Cancelled';
export type ExpenseSourceType = 'ReceivedInvoice' | 'Rent' | 'Payroll' | 'Manual';
export type DocumentEmailStatus = 'Pending' | 'Emailed';

export interface InvoiceItem {
  id?: string;
  description: string;
  qty: number;
  rate: number;
  total?: number;
}

export interface InvoicePayment {
  id?: string;
  currency?: string;
  amount: number;
  paymentDate: string;
  method: PaymentMethod;
  reference?: string;
  notes?: string;
}

export interface Invoice {
  id: string;
  invoiceNo: string;
  quotationId?: string;
  quotationNo?: string;
  poNumber?: string;
  customerId: string;
  customerName: string;
  customerEmail?: string;
  customerPhone?: string;
  deliveryNoteId?: string;
  courierId?: string;
  courierName?: string;
  dateIssued: string;
  dateDue: string;
  currency: string;
  subtotal: number;
  taxRate: number;
  taxAmount: number;
  grandTotal: number;
  amountPaid: number;
  balance: number;
  paymentStatus: PaymentStatus;
  emailStatus: DocumentEmailStatus;
  lastEmailedAt?: string;
  notes?: string;
  items: InvoiceItem[];
  payments: InvoicePayment[];
}

export interface InvoiceListItem {
  id: string;
  invoiceNo: string;
  quotationId?: string;
  quotationNo?: string;
  customer: string;
  courierId?: string;
  courierName?: string;
  currency: string;
  amount: number;
  dateIssued: string;
  dateDue: string;
  paymentStatus: PaymentStatus;
  emailStatus: DocumentEmailStatus;
  lastEmailedAt?: string;
}

export interface QuotationItem {
  id?: string;
  description: string;
  qty: number;
  rate: number;
  total?: number;
}

export interface Quotation {
  id: string;
  quotationNo: string;
  convertedInvoiceId?: string;
  convertedInvoiceNo?: string;
  poNumber?: string;
  customerId: string;
  customerName: string;
  customerTinNumber?: string;
  customerPhone?: string;
  customerEmail?: string;
  courierId?: string;
  courierName?: string;
  dateIssued: string;
  validUntil: string;
  currency: string;
  subtotal: number;
  taxRate: number;
  taxAmount: number;
  grandTotal: number;
  emailStatus: DocumentEmailStatus;
  lastEmailedAt?: string;
  notes?: string;
  items: QuotationItem[];
}

export interface QuotationListItem {
  id: string;
  quotationNo: string;
  convertedInvoiceId?: string;
  convertedInvoiceNo?: string;
  customer: string;
  courierId?: string;
  courierName?: string;
  currency: string;
  amount: number;
  dateIssued: string;
  validUntil: string;
  emailStatus: DocumentEmailStatus;
  lastEmailedAt?: string;
}

export interface QuotationConversionResult {
  invoiceId: string;
  invoiceNo: string;
  alreadyConverted: boolean;
}

export interface PurchaseOrderItem {
  id?: string;
  description: string;
  qty: number;
  rate: number;
  total?: number;
}

export interface PurchaseOrder {
  id: string;
  purchaseOrderNo: string;
  supplierId: string;
  supplierName: string;
  supplierTinNumber?: string;
  supplierContactNumber?: string;
  supplierEmail?: string;
  courierId?: string;
  courierName?: string;
  dateIssued: string;
  requiredDate: string;
  currency: string;
  subtotal: number;
  taxRate: number;
  taxAmount: number;
  grandTotal: number;
  emailStatus: DocumentEmailStatus;
  lastEmailedAt?: string;
  notes?: string;
  items: PurchaseOrderItem[];
}

export interface PurchaseOrderListItem {
  id: string;
  purchaseOrderNo: string;
  supplier: string;
  courierId?: string;
  courierName?: string;
  currency: string;
  amount: number;
  dateIssued: string;
  requiredDate: string;
  emailStatus: DocumentEmailStatus;
  lastEmailedAt?: string;
}

export interface Supplier {
  id: string;
  name: string;
  tinNumber?: string;
  contactNumber?: string;
  email?: string;
  address?: string;
  notes?: string;
  isActive: boolean;
  receivedInvoiceCount: number;
  outstandingAmount: number;
}

export interface SupplierLookup {
  id: string;
  name: string;
}

export interface ExpenseCategory {
  id: string;
  name: string;
  code: string;
  description?: string;
  bptCategoryCode: string;
  isActive: boolean;
  isSystem: boolean;
  sortOrder: number;
  usageCount: number;
}

export interface ExpenseCategoryLookup {
  id: string;
  name: string;
  code: string;
  bptCategoryCode: string;
}

export interface ReceivedInvoiceItem {
  id?: string;
  description: string;
  uom?: string;
  qty: number;
  rate: number;
  discountAmount: number;
  lineTotal?: number;
  gstRate: number;
  gstAmount?: number;
}

export interface ReceivedInvoicePayment {
  id: string;
  paymentDate: string;
  amount: number;
  method: PaymentMethod;
  reference?: string;
  notes?: string;
  paymentVoucherId?: string;
  paymentVoucherNumber?: string;
}

export interface ReceivedInvoiceAttachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
}

export interface ReceivedInvoiceListItem {
  id: string;
  invoiceNumber: string;
  supplierId: string;
  supplierName: string;
  invoiceDate: string;
  dueDate: string;
  currency: string;
  totalAmount: number;
  balanceDue: number;
  paymentStatus: ReceivedInvoiceStatus;
  approvalStatus: ApprovalStatus;
  expenseCategoryId: string;
  expenseCategoryName: string;
  isTaxClaimable: boolean;
  isOverdue: boolean;
  attachmentCount: number;
}

export interface ReceivedInvoice {
  id: string;
  invoiceNumber: string;
  supplierId: string;
  supplierName: string;
  supplierTin?: string;
  supplierContactNumber?: string;
  supplierEmail?: string;
  invoiceDate: string;
  dueDate: string;
  outlet?: string;
  description?: string;
  notes?: string;
  currency: string;
  subtotal: number;
  discountAmount: number;
  gstRate: number;
  gstAmount: number;
  totalAmount: number;
  balanceDue: number;
  paymentStatus: ReceivedInvoiceStatus;
  paymentMethod?: PaymentMethod;
  receiptReference?: string;
  settlementReference?: string;
  bankName?: string;
  bankAccountDetails?: string;
  miraTaxableActivityNumber?: string;
  revenueCapitalClassification: 'Revenue' | 'Capital';
  expenseCategoryId: string;
  expenseCategoryName: string;
  isTaxClaimable: boolean;
  approvalStatus: ApprovalStatus;
  approvedByUserId?: string;
  approvedAt?: string;
  items: ReceivedInvoiceItem[];
  payments: ReceivedInvoicePayment[];
  attachments: ReceivedInvoiceAttachment[];
}

export interface PaymentVoucherListItem {
  id: string;
  voucherNumber: string;
  date: string;
  payTo: string;
  paymentMethod: PaymentMethod;
  amount: number;
  status: PaymentVoucherStatus;
  bank?: string;
  linkedReceivedInvoiceNumber?: string;
}

export interface PaymentVoucher {
  id: string;
  voucherNumber: string;
  date: string;
  payTo: string;
  details: string;
  paymentMethod: PaymentMethod;
  accountNumber?: string;
  chequeNumber?: string;
  bank?: string;
  amount: number;
  amountInWords: string;
  approvedBy?: string;
  receivedBy?: string;
  linkedReceivedInvoiceId?: string;
  linkedReceivedInvoiceNumber?: string;
  linkedExpenseEntryId?: string;
  linkedExpenseDocumentNumber?: string;
  notes?: string;
  status: PaymentVoucherStatus;
  approvedAt?: string;
  postedAt?: string;
}

export interface ExpenseLedgerRow {
  sourceType: ExpenseSourceType;
  sourceId: string;
  documentNumber: string;
  transactionDate: string;
  expenseCategoryId?: string;
  expenseCategoryName: string;
  bptCategoryCode: string;
  supplierId?: string;
  supplierName?: string;
  payeeName: string;
  currency: string;
  netAmount: number;
  taxAmount: number;
  grossAmount: number;
  claimableTaxAmount: number;
  pendingAmount: number;
  description?: string;
  notes?: string;
}

export interface ExpenseEntryDetail {
  id: string;
  documentNumber: string;
  transactionDate: string;
  expenseCategoryId: string;
  expenseCategoryName: string;
  supplierId?: string;
  supplierName?: string;
  payeeName: string;
  currency: string;
  netAmount: number;
  taxAmount: number;
  grossAmount: number;
  claimableTaxAmount: number;
  pendingAmount: number;
  description?: string;
  notes?: string;
}

export interface ExpenseSummaryBucket {
  label: string;
  netAmount: number;
  taxAmount: number;
  grossAmount: number;
}

export interface ExpenseSummary {
  totalNetAmount: number;
  totalTaxAmount: number;
  totalGrossAmount: number;
  totalPendingAmount: number;
  byCategory: ExpenseSummaryBucket[];
  byMonth: ExpenseSummaryBucket[];
}

export interface RentEntryListItem {
  id: string;
  rentNumber: string;
  date: string;
  propertyName: string;
  payTo: string;
  currency: string;
  amount: number;
  expenseCategoryId: string;
  expenseCategoryName: string;
  approvalStatus: ApprovalStatus;
}

export interface RentEntry extends RentEntryListItem {
  notes?: string;
}

export interface StatementRow {
  index: number;
  date?: string;
  description: string;
  reference?: string;
  currency: 'MVR' | 'USD';
  amount: number;
  payments: number;
  receivedOn?: string;
  balance: number;
}

export interface StatementCurrencyTotals {
  mvr: number;
  usd: number;
}

export interface AccountStatement {
  customerId: string;
  customerName: string;
  year: number;
  statementNo: string;
  openingBalance: StatementCurrencyTotals;
  totalInvoiced: StatementCurrencyTotals;
  totalReceived: StatementCurrencyTotals;
  totalPending: StatementCurrencyTotals;
  rows: StatementRow[];
}

export interface Staff {
  id: string;
  staffId: string;
  staffName: string;
  idNumber?: string;
  phoneNumber?: string;
  email?: string;
  hiredDate?: string;
  designation?: string;
  workSite?: string;
  bankName?: 'BML' | 'MIB';
  accountName?: string;
  accountNumber?: string;
  basic: number;
  serviceAllowance: number;
  otherAllowance: number;
  phoneAllowance: number;
  foodRate: number;
}

export interface PayrollEntry {
  id: string;
  staffId: string;
  staffCode: string;
  staffName: string;
  idNumber?: string;
  phoneNumber?: string;
  email?: string;
  hiredDate?: string;
  designation?: string;
  workSite?: string;
  bankName?: 'BML' | 'MIB';
  accountName?: string;
  accountNumber?: string;
  basic: number;
  serviceAllowance: number;
  otherAllowance: number;
  phoneAllowance: number;
  grossBase: number;
  grossAllowances: number;
  subTotal: number;
  periodDays: number;
  attendedDays: number;
  absentDays: number;
  ratePerDay: number;
  absentDeduction: number;
  foodAllowanceDays: number;
  foodAllowanceRate: number;
  foodAllowance: number;
  overtimePay: number;
  pensionDeduction: number;
  salaryAdvanceDeduction: number;
  totalPay: number;
  netPayable: number;
}

export interface PayrollPeriod {
  id: string;
  year: number;
  month: number;
  startDate: string;
  endDate: string;
  periodDays: number;
  status: 'Draft' | 'Finalized';
  totalNetPayable: number;
}

export interface PayrollPeriodDetail extends PayrollPeriod {
  entries: PayrollEntry[];
}

export interface SalarySlip {
  payrollEntryId: string;
  slipNo: string;
  periodStart: string;
  periodEnd: string;
  staffName: string;
  staffCode: string;
  idNumber?: string;
  phoneNumber?: string;
  email?: string;
  hiredDate?: string;
  designation?: string;
  workSite?: string;
  bankName?: 'BML' | 'MIB';
  accountName?: string;
  accountNumber?: string;
  basicSalary: number;
  serviceAllowance: number;
  otherAllowance: number;
  phoneAllowance: number;
  foodAllowance: number;
  overtimePay: number;
  foodAllowanceDays: number;
  foodAllowanceRate: number;
  attendedDays: number;
  absentDays: number;
  ratePerDay: number;
  absentDeduction: number;
  pensionDeduction: number;
  salaryAdvanceDeduction: number;
  totalSalary: number;
  totalDeduction: number;
  totalPayable: number;
}

export type StaffConductFormType = 'Warning' | 'Disciplinary';
export type StaffConductSeverity = 'Low' | 'Medium' | 'High' | 'Critical';
export type StaffConductStatus = 'Open' | 'Acknowledged' | 'Resolved';

export interface StaffConductSummary {
  totalForms: number;
  warningCount: number;
  disciplinaryCount: number;
  openCount: number;
  acknowledgedCount: number;
  resolvedCount: number;
}

export interface StaffConductStaffOption {
  id: string;
  staffId: string;
  staffName: string;
  designation?: string;
  workSite?: string;
}

export interface StaffConductListItem {
  id: string;
  formNumber: string;
  formType: StaffConductFormType;
  issueDate: string;
  incidentDate: string;
  staffId: string;
  staffCode: string;
  staffName: string;
  designation?: string;
  workSite?: string;
  subject: string;
  severity: StaffConductSeverity;
  status: StaffConductStatus;
  issuedBy: string;
  isAcknowledgedByStaff: boolean;
  followUpDate?: string;
  resolvedDate?: string;
}

export interface StaffConductDetail extends StaffConductListItem {
  idNumber?: string;
  incidentDetails: string;
  actionTaken: string;
  requiredImprovement?: string;
  witnessedBy?: string;
  acknowledgedDate?: string;
  employeeRemarks?: string;
  resolutionNotes?: string;
}

export interface TenantSettings {
  username: string;
  companyName: string;
  companyEmail: string;
  companyPhone: string;
  tinNumber: string;
  businessRegistrationNumber: string;
  invoicePrefix: string;
  deliveryNotePrefix: string;
  quotePrefix: string;
  purchaseOrderPrefix: string;
  receivedInvoicePrefix: string;
  paymentVoucherPrefix: string;
  rentEntryPrefix: string;
  warningFormPrefix: string;
  statementPrefix: string;
  salarySlipPrefix: string;
  isTaxApplicable: boolean;
  defaultTaxRate: number;
  defaultDueDays: number;
  defaultCurrency: string;
  taxableActivityNumber: string;
  isInputTaxClaimEnabled: boolean;
  bmlMvrAccountName: string;
  bmlMvrAccountNumber: string;
  bmlUsdAccountName: string;
  bmlUsdAccountNumber: string;
  mibMvrAccountName: string;
  mibMvrAccountNumber: string;
  mibUsdAccountName: string;
  mibUsdAccountNumber: string;
  invoiceOwnerName: string;
  invoiceOwnerIdCard: string;
  quotationEmailBodyTemplate: string;
  invoiceEmailBodyTemplate: string;
  purchaseOrderEmailBodyTemplate: string;
  logoUrl?: string;
  companyStampUrl?: string;
  companySignatureUrl?: string;
}

export interface DocumentEmailRequest {
  ccEmail?: string;
  body?: string;
}

export interface TenantLogoUpload {
  url: string;
  relativePath: string;
  fileName: string;
}

export type SignupRequestStatus = 'Pending' | 'Accepted' | 'Rejected';
export type BusinessAccountStatus = 'Active' | 'Disabled';

export interface PortalAdminDashboard {
  totalBusinesses: number;
  pendingSignupRequests: number;
  activeBusinesses: number;
  disabledBusinesses: number;
  totalStaffAcrossBusinesses: number;
  totalVesselsAcrossBusinesses: number;
  recentSignupRequests: PortalAdminRecentSignupRequest[];
  recentActions: PortalAdminAuditLog[];
}

export interface PortalAdminRecentSignupRequest {
  id: string;
  requestDate: string;
  companyName: string;
  requestedByName: string;
  status: SignupRequestStatus;
}

export interface SignupRequestListItem {
  id: string;
  requestDate: string;
  companyName: string;
  companyEmail: string;
  companyPhone: string;
  tinNumber: string;
  businessRegistrationNumber: string;
  requestedByName: string;
  requestedByEmail: string;
  status: SignupRequestStatus;
}

export interface SignupRequestDetail extends SignupRequestListItem {
  rejectionReason?: string;
  reviewNotes?: string;
  reviewedAt?: string;
  reviewedByUserId?: string;
  reviewedByUserName?: string;
  approvedTenantId?: string;
}

export interface SignupRequestCounts {
  pending: number;
  accepted: number;
  rejected: number;
}

export interface PortalAdminBusinessListItem {
  tenantId: string;
  companyName: string;
  companyEmail: string;
  companyPhone: string;
  tinNumber: string;
  businessRegistrationNumber: string;
  status: BusinessAccountStatus;
  staffCount: number;
  vesselCount: number;
  createdAt: string;
  lastActivityAt?: string;
}

export interface PortalAdminBusinessDetail extends PortalAdminBusinessListItem {
  approvedAt?: string;
  disabledReason?: string;
  disabledAt?: string;
  customerCount: number;
  invoiceCount: number;
  primaryAdmin?: PortalAdminBusinessUser;
}

export interface PortalAdminBusinessUser {
  id: string;
  tenantId: string;
  companyName: string;
  fullName: string;
  email: string;
  role: string;
  isActive: boolean;
  lastLoginAt?: string;
}

export interface PortalAdminAuditLog {
  id: string;
  performedAt: string;
  actionType: string;
  targetType: string;
  targetId?: string;
  targetName?: string;
  relatedTenantId?: string;
  performedByName?: string;
  details?: string;
}

export type PortalAdminInvoiceStatus = 'Draft' | 'Issued' | 'Emailed' | 'EmailFailed' | 'Cancelled';
export type PortalAdminInvoiceEmailStatus = 'Sent' | 'Failed';

export interface PortalAdminBillingDashboard {
  invoicesGeneratedThisMonth: number;
  totalBilledMvrThisMonth: number;
  totalBilledUsdThisMonth: number;
  totalEmailedThisMonth: number;
  pendingEmailCount: number;
  recentInvoices: PortalAdminBillingInvoiceListItem[];
}

export interface PortalAdminBillingSettings {
  basicSoftwareFee: number;
  vesselFee: number;
  staffFee: number;
  invoicePrefix: string;
  startingSequenceNumber: number;
  defaultCurrency: 'MVR' | 'USD';
  defaultDueDays: number;
  accountName: string;
  accountNumber: string;
  bankName?: string;
  branch?: string;
  paymentInstructions?: string;
  invoiceFooterNote?: string;
  invoiceTerms?: string;
  logoUrl?: string;
  emailFromName?: string;
  replyToEmail?: string;
  autoGenerationEnabled: boolean;
  autoEmailEnabled: boolean;
}

export interface PortalAdminBillingLogoUpload {
  url: string;
  relativePath: string;
  fileName: string;
}

export interface PortalAdminBillingBusinessOption {
  tenantId: string;
  companyName: string;
  companyEmail: string;
  isActive: boolean;
  staffCount: number;
  vesselCount: number;
}

export type PortalAdminEmailAudienceMode = 'AllBusinesses' | 'SelectedBusinesses';
export type PortalAdminEmailRecipientStatus = 'Sent' | 'Failed';

export interface PortalAdminEmailBusinessOption {
  tenantId: string;
  companyName: string;
  companyEmail: string;
  status: BusinessAccountStatus;
  activeAdminCount: number;
  primaryAdminName?: string;
  primaryAdminEmail?: string;
}

export interface PortalAdminEmailCampaign {
  id: string;
  subject: string;
  audienceMode: PortalAdminEmailAudienceMode;
  ccAdminUsers: boolean;
  includeDisabledBusinesses: boolean;
  requestedCompanyCount: number;
  sentCompanyCount: number;
  failedCompanyCount: number;
  sentAt: string;
  sentByName?: string;
}

export interface PortalAdminEmailCampaignSendCompanyResult {
  tenantId: string;
  companyName: string;
  toEmail: string;
  ccAdminCount: number;
  status: PortalAdminEmailRecipientStatus;
  errorMessage?: string;
}

export interface PortalAdminEmailCampaignSendResult {
  campaignId: string;
  requestedCompanyCount: number;
  sentCompanyCount: number;
  failedCompanyCount: number;
  results: PortalAdminEmailCampaignSendCompanyResult[];
}

export interface PortalAdminBillingInvoiceListItem {
  id: string;
  invoiceNumber: string;
  billingMonth: string;
  invoiceDate: string;
  dueDate: string;
  tenantId: string;
  companyName: string;
  total: number;
  currency: 'MVR' | 'USD';
  status: PortalAdminInvoiceStatus;
  createdAt: string;
  sentAt?: string;
  isCustom: boolean;
}

export interface PortalAdminBillingInvoiceLineItem {
  id: string;
  description: string;
  quantity: number;
  rate: number;
  amount: number;
  sortOrder: number;
}

export interface PortalAdminBillingInvoiceEmailLog {
  id: string;
  toEmail: string;
  ccEmail?: string;
  subject: string;
  attemptedAt: string;
  status: PortalAdminInvoiceEmailStatus;
  errorMessage?: string;
}

export interface PortalAdminBillingInvoiceDetail extends PortalAdminBillingInvoiceListItem {
  companyEmail: string;
  companyPhone: string;
  companyTinNumber: string;
  companyRegistrationNumber: string;
  companyAdminName?: string;
  companyAdminEmail?: string;
  baseSoftwareFee: number;
  vesselCount: number;
  vesselRate: number;
  vesselAmount: number;
  staffCount: number;
  staffRate: number;
  staffAmount: number;
  subtotal: number;
  notes?: string;
  lineItems: PortalAdminBillingInvoiceLineItem[];
  emailLogs: PortalAdminBillingInvoiceEmailLog[];
}

export interface PortalAdminBillingGenerateInvoiceRequest {
  tenantId: string;
  billingMonth: string;
  allowDuplicateForMonth: boolean;
  previewOnly: boolean;
}

export interface PortalAdminBillingGenerateBulkInvoicesRequest {
  billingMonth: string;
  includeDisabledBusinesses: boolean;
  allowDuplicateForMonth: boolean;
  previewOnly: boolean;
  tenantIds: string[];
}

export interface PortalAdminBillingCustomInvoiceLineItemRequest {
  description: string;
  quantity: number;
  rate: number;
}

export interface PortalAdminBillingCustomInvoiceRequest {
  tenantIds: string[];
  billingMonth: string;
  currency?: 'MVR' | 'USD';
  invoiceDate?: string;
  dueDate?: string;
  softwareFee?: number;
  vesselFee?: number;
  staffFee?: number;
  saveAsBusinessCustomRate: boolean;
  allowDuplicateForMonth: boolean;
  previewOnly: boolean;
  notes?: string;
  lineItems: PortalAdminBillingCustomInvoiceLineItemRequest[];
}

export interface PortalAdminBillingGeneratedInvoice {
  invoiceId?: string;
  invoiceNumber: string;
  tenantId: string;
  companyName: string;
  billingMonth: string;
  staffCount: number;
  vesselCount: number;
  total: number;
  currency: 'MVR' | 'USD';
}

export interface PortalAdminBillingSkippedInvoice {
  tenantId: string;
  companyName: string;
  reason: string;
}

export interface PortalAdminBillingGenerationResult {
  previewOnly: boolean;
  generatedCount: number;
  skippedCount: number;
  invoices: PortalAdminBillingGeneratedInvoice[];
  skipped: PortalAdminBillingSkippedInvoice[];
}

export interface PortalAdminBillingResetInvoicesResult {
  deletedCount: number;
}

export interface PortalAdminBillingStatementMonth {
  month: number;
  label: string;
  invoiceCount: number;
  totalMvr: number;
  totalUsd: number;
  emailedCount: number;
  pendingCount: number;
}

export interface PortalAdminBillingYearlyStatement {
  tenantId: string;
  companyName: string;
  year: number;
  totalInvoices: number;
  emailedInvoices: number;
  pendingInvoices: number;
  totalInvoiced: StatementCurrencyTotals;
  months: PortalAdminBillingStatementMonth[];
  invoices: PortalAdminBillingInvoiceListItem[];
}

export interface PortalAdminBillingCustomRate {
  id: string;
  tenantId: string;
  companyName: string;
  softwareFee: number;
  vesselFee: number;
  staffFee: number;
  currency: 'MVR' | 'USD';
  isActive: boolean;
  effectiveFrom?: string;
  effectiveTo?: string;
  notes?: string;
}

export interface PortalAdminBillingUpsertCustomRateRequest {
  tenantId: string;
  softwareFee: number;
  vesselFee: number;
  staffFee: number;
  currency: 'MVR' | 'USD';
  isActive: boolean;
  effectiveFrom?: string;
  effectiveTo?: string;
  notes?: string;
}
