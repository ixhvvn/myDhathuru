# Portal Finance And Compliance Expansion Plan

## Current Repo State

### Frontend

- Business routes are currently flat in `frontend/src/app/features/business/business.routes.ts`.
- The sidebar in `frontend/src/app/layout/app-shell/app-shell.component.ts` is a flat `navItems` array.
- Existing tenant-facing features are:
  - dashboard
  - customers
  - delivery notes
  - invoices (`sales-history`)
  - quotations
  - purchase orders
  - customer statements
  - payroll
  - reports
  - settings
- Existing UI patterns are:
  - standalone Angular components
  - page headers via `AppPageHeaderComponent`
  - cards via `AppCardComponent`
  - tables via `AppDataTableComponent`
  - services through `PortalApiService`
  - detail drawers / modal-style overlays

### Backend

- Architecture is already split cleanly across:
  - `MyDhathuru.Domain`
  - `MyDhathuru.Application`
  - `MyDhathuru.Infrastructure`
  - `MyDhathuru.Api`
- Existing tenant-scoped document modules are:
  - customers
  - vessels
  - delivery notes
  - invoices
  - quotations
  - purchase orders
  - payroll
  - customer statements
  - sales reports
- Existing infrastructure patterns:
  - tenant scoping through `TenantEntity` plus global query filter in `ApplicationDbContext`
  - soft delete via `AuditableEntity.IsDeleted`
  - audit fields (`CreatedAt`, `UpdatedAt`, `CreatedByUserId`, `UpdatedByUserId`) applied centrally in `ApplicationDbContext.SaveChanges`
  - document numbering through `DocumentSequence` and `DocumentNumberService`
  - PDFs generated in `PdfExportService`
  - sales Excel/PDF exports generated in `ReportService`
  - API validation through FluentValidation + `ValidationActionFilter`

### Current Gaps Against Requested Scope

- No supplier / purchase-side data model exists.
- No received invoices / AP / expense payment workflow exists.
- No tenant-side business audit log exists. Only `AdminAuditLog` exists for super-admin actions.
- No expense ledger abstraction exists.
- No rent module exists.
- No payment voucher module exists.
- No disciplinary / warning module exists.
- Customer statements are year-based and invoice/payment focused only; they do not yet include:
  - richer filters
  - overdue / pending-only views
  - AR aging buckets
  - customer detail statement page
- Reports are currently sales-only.
- Settings currently manage invoice, delivery note, quotation, PO, and salary prefixes, but not the broader document sequence set required for purchase/compliance workflows.
- Sidebar is not grouped and will not scale to the requested module count.

## Uploaded Template Findings

### MIRA Input Tax Workbook

Source file:
- `D:/projects/Software Engineering/Dhathuru/New files/MIRA -Input_Tax_Statement_v25.1 - 4th 2025.xlsx`

Observed columns from row 1:
- `#`
- `Supplier TIN`
- `Supplier Name`
- `Supplier Invoice Number`
- `Invoice Date`
- `Invoice Total (excluding GST)`
- `GST Charged at 6%`
- `GST Charged at 8%`
- `GST Charged at 12%`
- `GST Charged at 16%`
- `GST Charged at 17%`
- `Your Taxable Activity Number`
- `Revenue / Capital`

Implication:
- Purchase-side tax records must preserve per-rate GST amounts, not just one aggregate GST amount.

### MIRA Output Tax Workbook

Source file:
- `D:/projects/Software Engineering/Dhathuru/New files/MIRA-Output_Tax_Statement_v25.1 - 4th 2025.xlsx`

Observed sheets:
- `TaxInvoices`
- `OtherTransactions`

Observed `TaxInvoices` columns:
- `Customer TIN`
- `Customer Name`
- `Invoice No.`
- `Invoice Date`
- `Value of Supplies Subject to GST at 8% or 17% (excluding GST)`
- `Value of Zero-Rated Supplies`
- `Value of Exempt Supplies`
- `Value of Out-of-Scope Supplies`
- `Your Taxable Activity No.`

Observed `OtherTransactions` columns:
- `Your Taxable Activity No.`
- `Value of Supplies Subject to GST at 8% or 17% (excluding GST)`
- `Value of Zero-Rated Supplies`
- `Value of Exempt Supplies`
- `Value of Out-of-Scope Supplies`

Implication:
- Sales invoices need tax treatment classification beyond the current single-tax-rate model if the module is to support exempt / zero-rated / out-of-scope reporting cleanly.

### BPT / Income Statement PDFs

Source files:
- `D:/projects/Software Engineering/Dhathuru/New files/Income statement 2024 RNZ.pdf`
- `D:/projects/Software Engineering/Dhathuru/New files/Notes For BPT 2024 Rnz.pdf`

Observed expense category structure:
- Salaries
- Insurance
- Legal and Professional Fees
- Licenses and Fees
- For Office Use
- Rent
- Repairs and Maintenance
- Supplies
- Telephone
- Travel
- Utilities
- Ferry Hired
- Diesel Charges

Implication:
- Expense categories and BPT categories should be related but not identical.
- BPT reporting needs category mapping rather than assuming one shared label set for every expense view.

## Recommended Delivery Phases

This scope is too large to ship safely as one undifferentiated change. The clean implementation path is:

### Phase 1: Purchases And Expense Foundation

- tenant audit log
- suppliers
- expense categories
- received invoices
- received invoice payments
- received invoice attachments
- payment vouchers
- expense ledger
- rent entries
- navbar restructure

### Phase 2: Sales Statement And Compliance Expansion

- customer statement detail page
- AR aging
- pending / overdue filters
- MIRA input tax
- MIRA output tax
- tax preview/export pages

### Phase 3: Payroll HR And BPT

- disciplinary / warning forms
- payroll-linked expense rollups
- BPT category mapping
- BPT summary and supporting schedules
- settings sub-pages for document sequences, tax settings, user/role access

## Exact Backend Module Structure

### Domain

Add under `backend/src/MyDhathuru.Domain/Enums`:

- `ApprovalStatus.cs`
- `ReceivedInvoiceStatus.cs`
- `ReceivedInvoicePaymentMethod.cs`
- `PaymentVoucherStatus.cs`
- `ExpenseSourceType.cs`
- `ExpenseClassificationType.cs`
- `ExpenseDocumentType.cs`
- `TaxTreatmentType.cs`
- `RevenueCapitalType.cs`
- `DisciplinaryActionType.cs`
- `DisciplinaryStatus.cs`
- `BusinessAuditActionType.cs`
- `BptCategoryCode.cs`

Extend `DocumentType.cs` with:

- `ReceivedInvoice`
- `PaymentVoucher`
- `SupplierPayment`
- `RentEntry`
- `WarningForm`

Add under `backend/src/MyDhathuru.Domain/Entities`:

- `BusinessAuditLog.cs`
- `Supplier.cs`
- `ExpenseCategory.cs`
- `ReceivedInvoice.cs`
- `ReceivedInvoiceItem.cs`
- `ReceivedInvoicePayment.cs`
- `ReceivedInvoiceAttachment.cs`
- `PaymentVoucher.cs`
- `PaymentVoucherApprovalHistory.cs`
- `ExpenseEntry.cs`
- `RentEntry.cs`
- `EmployeeDisciplinaryAction.cs`
- `EmployeeWarningForm.cs`
- `BptCategory.cs`
- `BptManualAdjustment.cs`
- `BptReportSnapshot.cs`

Extend existing entities:

- `TenantSettings.cs`
  - add prefixes for received invoice, payment voucher, rent, warning form
  - add tax settings fields:
    - taxable activity number
    - default output tax treatment
    - whether input tax claiming is enabled
- `Invoice.cs`
  - add supply classification fields for GST output reporting:
    - taxable supplies amount excluding GST
    - zero rated amount
    - exempt amount
    - out of scope amount
    - taxable activity number snapshot if needed
- `Staff.cs`
  - optional navigation collections for discipline / warning history

### Application

Add folders under `backend/src/MyDhathuru.Application`:

- `Suppliers`
- `ReceivedInvoices`
- `PaymentVouchers`
- `Expenses`
- `TaxCompliance`
- `Bpt`
- `HrDiscipline`
- `AuditLogs`

Recommended per module structure:

- `Dtos/`
- `Validators/`
- `Queries/` only if the team later separates handlers

Add interfaces under `Application/Common/Interfaces`:

- `ISupplierService.cs`
- `IReceivedInvoiceService.cs`
- `IPaymentVoucherService.cs`
- `IExpenseService.cs`
- `ITaxComplianceService.cs`
- `IBptService.cs`
- `IHrDisciplineService.cs`
- `IBusinessAuditLogService.cs`

Extend current DTO modules:

- `Settings/Dtos/SettingsDtos.cs`
- `Reports/Dtos/*`
- `Statements/Dtos/*`

### Infrastructure

Add services under `backend/src/MyDhathuru.Infrastructure/Services`:

- `BusinessAuditLogService.cs`
- `SupplierService.cs`
- `ReceivedInvoiceService.cs`
- `PaymentVoucherService.cs`
- `ExpenseService.cs`
- `TaxComplianceService.cs`
- `BptService.cs`
- `HrDisciplineService.cs`

Extend:

- `ApplicationDbContext.cs`
- `DocumentNumberService.cs`
- `PdfExportService.cs`
- `ReportService.cs`
- `StatementService.cs`
- `SettingsService.cs`
- `DashboardService.cs`
- `DependencyInjection.cs`

### API

Add controllers under `backend/src/MyDhathuru.Api/Controllers`:

- `SuppliersController.cs`
- `ReceivedInvoicesController.cs`
- `PaymentVouchersController.cs`
- `ExpensesController.cs`
- `CustomerStatementsController.cs`
- `TaxComplianceController.cs`
- `BptController.cs`
- `PayrollDisciplinaryActionsController.cs`
- `BusinessAuditLogsController.cs`

Notes:

- `AccountStatementsController.cs` should likely be replaced or extended by `CustomerStatementsController.cs`.
- Keep policy usage aligned to current policies:
  - `StaffOrAdmin` for read/query/report pages where staff access is acceptable
  - `AdminOnly` for create/update/delete/approve/configuration actions that affect finance/compliance

## Exact Frontend Module Structure

Add feature folders under `frontend/src/app/features`:

- `received-invoices/`
- `payment-vouchers/`
- `expenses/`
- `rent/`
- `discipline/`
- `tax-compliance/`
- `bpt/`
- `suppliers/`
- `user-access/`

Recommended page layout per feature:

- `*-page/` for list + filters + summary
- `*-detail-page/` or detail drawer for view
- keep page components standalone to match current app style

Concrete component targets:

- `received-invoices/received-invoices-page/received-invoices-page.component.ts`
- `payment-vouchers/payment-vouchers-page/payment-vouchers-page.component.ts`
- `expenses/expense-ledger-page/expense-ledger-page.component.ts`
- `rent/rent-page/rent-page.component.ts`
- `discipline/discipline-page/discipline-page.component.ts`
- `statements/customer-statements-page/customer-statements-page.component.ts`
- `tax-compliance/mira-input-tax-page/mira-input-tax-page.component.ts`
- `tax-compliance/mira-output-tax-page/mira-output-tax-page.component.ts`
- `bpt/bpt-summary-page/bpt-summary-page.component.ts`
- `suppliers/suppliers-page/suppliers-page.component.ts`
- `settings/document-sequences-page/document-sequences-page.component.ts`
- `settings/tax-settings-page/tax-settings-page.component.ts`
- `settings/user-access-page/user-access-page.component.ts`

Extend:

- `frontend/src/app/features/services/portal-api.service.ts`
- `frontend/src/app/core/models/app.models.ts`
- `frontend/src/app/features/business/business.routes.ts`
- `frontend/src/app/layout/app-shell/app-shell.component.ts`

## Navbar Restructure Model

Replace the current flat `NavItem[]` model with grouped navigation:

- `Dashboard`
- `Sales & Receivables`
  - customers
  - quotations
  - invoices issued
  - customer statements
  - collections / receipts
- `Purchases & Expenses`
  - received invoices
  - payment vouchers
  - rent
  - expense ledger
  - expense categories
  - supplier payments
- `Payroll & HR`
  - staff
  - payroll
  - salary slips
  - discipline / warning forms
- `Tax & Compliance`
  - MIRA output tax
  - MIRA input tax
  - BPT
- `Reports`
  - AR aging / pending invoices
  - expense summary
  - payroll summary
  - tax reports
  - BPT summary
- `Settings`
  - business settings
  - document sequences
  - tax settings
  - user / role access

Implementation note:

- the sidebar component should move from one flat `navItems` array to:
  - `NavGroup`
  - `NavItem`
  - optional role restrictions per item/group
- breadcrumbs should be route-data-driven rather than hardcoded labels

## Required Shared Backend Additions

### Tenant Audit Logging

Create a tenant-scoped business audit log:

- `BusinessAuditLog`
  - tenant scoped
  - actor user id
  - action type
  - target type
  - target id
  - summary
  - json details
  - performed at

Create `IBusinessAuditLogService` with a write helper used by:

- received invoice create/update/delete/payment
- payment voucher create/update/approve/cancel/post
- expense category create/update/delete
- disciplinary create/update/status change
- rent create/update/delete
- BPT manual adjustments
- settings updates related to tax/document sequences/access

### Expense Posting Strategy

Use `ExpenseEntry` as the normalized ledger table.

Each source writes ledger rows:

- received invoice
- rent entry
- payroll period
- payment voucher if needed as settlement/document trace

Fields should include:

- tenant id
- source type
- source id
- document number
- transaction date
- category id
- BPT category id
- supplier/payee label
- currency
- amount excluding GST
- GST amount
- gross amount
- claimable GST amount
- pending balance if relevant
- notes

This keeps reporting, BPT, and summaries database-driven without duplicating UI logic.

## Required Data/Export Adjustments

### Customer Statements

Refactor current statement generation to support:

- date range
- pending only
- overdue only
- running balance
- debit / credit presentation
- pending amount per invoice
- aging buckets:
  - current
  - 1-30
  - 31-60
  - 61-90
  - 90+

### MIRA Input Tax

Input tax preview/export should read from:

- `ReceivedInvoice`
- optionally `ExpenseEntry` for claimable non-invoice purchase-side tax records if the business records them

### MIRA Output Tax

Output tax preview/export should read from:

- `Invoice`
- invoice tax treatment fields
- non-invoice taxable sales only if a real persisted transaction type exists

### BPT

BPT summary should read from:

- expense ledger
- payroll rollup
- rent
- manual adjustments

It should not depend on frontend aggregates.

## Recommended First Implementation Slice

The first production-safe slice should be:

1. business audit log
2. grouped sidebar
3. suppliers
4. expense categories
5. received invoices
6. received invoice payments
7. payment vouchers
8. expense ledger
9. rent

That slice unlocks:

- purchase-side operations
- AP tracking
- MIRA input tax groundwork
- BPT groundwork
- cleaner navigation

The remaining compliance and HR pages can then reuse the same persisted data model instead of layering more one-off tables later.
