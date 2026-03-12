# Workbook-Derived Assumptions

This file captures workbook interpretation decisions made during implementation.

## Workbooks inspected

- `Delivery Note.xlsx`
- `Invoice - 2026.xlsm`
- `Sales History.xlsm`
- `Customer Accounts.xlsx`
- `Customers.xlsm`
- `Salary Details.xlsx`
- `Salary template.xlsx`

## Delivery Notes

Observed columns:

`#`, `Date`, `Details`, `Qty`, `Customer`, `Vessel`, `Rate`, `Total`, `Bill #`, `Cash Payment`, `Vessel Payment`

Workbook issue:

`Total` often equals `Rate` due table formula structure and frequent `Qty = 1` usage.

Implemented rule:

`Total = Qty * Rate` for each line item.

## Invoices

Observed invoice template concepts:

- Invoice number format `SL-2026-003`
- GST concept with configurable rate
- Due-day concept (example shows `7` days)

Workbook issue:

Template contains broken lookup references (`#REF!`) and non-portable formula links.

Implemented rule:

- Customer and item selection comes from persisted DB records.
- `TaxRate` and `DefaultDueDays` are configurable in tenant settings.
- Totals are computed in service layer and persisted.

## Sales History

Observed columns:

`Invoice No`, `Customer`, `Amount`, `Date issued`, `Date Due`, `Payment status`.

Implemented behavior:

Invoice list page exposes same core columns with live computed status from payment data.

## Customer Accounts / Statements

Observed structure:

- Header totals: previous balance, invoiced amount, payments received, total balance
- Row ledger: date, description, reference, amount, payments, received on, balance

Workbook issue:

Balance formulas are row-local (`Amount - Payments`) and not truly running balance across rows.

Implemented rule:

Chronological running ledger:

- Start with opening balance for selected year
- Add invoice rows
- Add payment rows
- Compute running balance cumulatively

## Customers

Observed columns:

`Customer Name`, `Tin No.`, `Customer Ref 1..4`

Implemented model:

- Main list fields: Name, Tin, Phone, Email
- Optional references persisted as `CustomerContact` entries

## Salary Details

Observed payroll headers include:

`BASIC`, `SERVICE Allowance`, `Other Allowance`, `Phone allowance`, `SUB TOTAL`, `Attended`, `Rate / Days`, `No.Of.Days (food allowance)`, `Absent`, `Absent Days`, `Food A Rate`, `Food Allowance`, `OT Pay`, `PENSION`, `Sallary Advance`, `Total Pay`, `ACCOUNT NUMBER`, `Total Sallary`

Workbook inconsistencies:

- Several formulas point to fixed cells (`H28`, `H29`, etc.) with unstable references.
- Mixed terminology (`Total Pay` / `Total Salary`) and spelling inconsistencies.

Implemented formulas:

- `SUB_TOTAL = BASIC + SERVICE_ALLOWANCE + OTHER_ALLOWANCE`
- `RATE_PER_DAY = SUB_TOTAL / PERIOD_DAYS`
- `ABSENT_DAYS = PERIOD_DAYS - ATTENDED_DAYS`
- `ABSENT_DEDUCTION = RATE_PER_DAY * ABSENT_DAYS` (shown for transparency)
- `FOOD_ALLOWANCE = FOOD_ALLOWANCE_RATE * FOOD_ALLOWANCE_DAYS`
- `PENSION_DEDUCTION = BASIC * 0.07`
- `TOTAL_PAY = (RATE_PER_DAY * ATTENDED_DAYS) + PHONE_ALLOWANCE + FOOD_ALLOWANCE + OT_PAY`
- `NET_PAYABLE = TOTAL_PAY - PENSION_DEDUCTION - SALARY_ADVANCE`

Reasoning:

This preserves workbook intent while removing broken cell-link dependence and making calculation deterministic.

## Salary Slip Template

Observed sections:

- Company header
- Staff details
- Earnings
- Deductions
- Total payable
- Account details

Implemented salary slip output follows the same business layout with modernized PDF rendering.
