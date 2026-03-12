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
  date: string;
  currency: string;
  customerId: string;
  customerName: string;
  vesselId?: string;
  vesselName?: string;
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
}

export type PaymentStatus = 'Unpaid' | 'Partial' | 'Paid';
export type PaymentMethod = 'Cash' | 'Card' | 'Transfer';

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
  poNumber?: string;
  customerId: string;
  customerName: string;
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
  notes?: string;
  items: InvoiceItem[];
  payments: InvoicePayment[];
}

export interface InvoiceListItem {
  id: string;
  invoiceNo: string;
  customer: string;
  courierId?: string;
  courierName?: string;
  currency: string;
  amount: number;
  dateIssued: string;
  dateDue: string;
  paymentStatus: PaymentStatus;
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

export interface TenantSettings {
  username: string;
  companyName: string;
  companyEmail: string;
  companyPhone: string;
  tinNumber: string;
  businessRegistrationNumber: string;
  invoicePrefix: string;
  deliveryNotePrefix: string;
  defaultTaxRate: number;
  defaultDueDays: number;
  defaultCurrency: string;
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
  logoUrl?: string;
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
