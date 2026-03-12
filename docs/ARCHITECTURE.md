# myDhathuru Architecture

## Overview
myDhathuru is a multi-tenant full-stack portal with a .NET 10 backend and Angular 20 frontend.

- Backend: ASP.NET Core Web API (controllers), EF Core, PostgreSQL, JWT + refresh tokens, FluentValidation, Serilog, Swagger.
- Frontend: Angular standalone components, router guards, auth interceptor with refresh flow, inactivity logout.
- Infrastructure: Docker Compose for PostgreSQL + API + frontend.

## Backend Layers

- `MyDhathuru.Domain`: entities, enums, base entity abstractions.
- `MyDhathuru.Application`: DTO contracts, validation, service interfaces, shared models/exceptions.
- `MyDhathuru.Infrastructure`: EF Core DbContext, service implementations, auth/token/password logic, PDF exports.
- `MyDhathuru.Api`: controllers, middleware, program bootstrap.

## Shared Backend Foundations

- `BaseEntity`, `AuditableEntity`, `TenantEntity`
- `ApiResponse<T>`, `PagedResult<T>`
- `CurrentUserService`, `CurrentTenantService`
- `TenantResolutionMiddleware`
- `ExceptionHandlingMiddleware`
- `ValidationActionFilter`
- `DocumentNumberService`
- `PdfExportService`

## Tenant Isolation

1. Business entities derive from `TenantEntity` and include `TenantId`.
2. EF Core global query filters restrict all tenant entities to current authenticated tenant.
3. Tenant ID is resolved from trusted JWT claim (`tenant_id`) only.
4. Service methods never accept tenant id from client payload.
5. Tenant-aware unique indexes protect document numbering and master data.

## Document Numbering

`DocumentNumberService` generates tenant-specific numbers using `DocumentSequence` table + tenant settings prefixes.

Pattern:

`<PREFIX>-<YEAR>-<SEQUENCE_3_DIGITS>`

Examples:

- Delivery note: `DN-2026-001`
- Invoice: `SL-2026-001`
- Salary slip: `SAL-2026-001`

## Module Coverage

- Authentication: signup, login, refresh, forgot/reset password, current profile
- Dashboard: live summary metrics
- Customers: CRUD + optional references
- Vessels: CRUD
- Delivery notes: CRUD, filters, invoice generation, PDF export
- Invoices: CRUD, payments, payment history, PDF export
- Account statements: query by customer/year, opening balance save, PDF export
- Payroll: staff CRUD, period create/list/get, recalculate, entry update, salary slip generate/get/export
- Settings: tenant settings get/update, change password

## Frontend Structure

- `core`: auth, API service, interceptor, guard, inactivity service, toast system
- `shared`: reusable components (`app-button`, `app-card`, `app-loader`, `app-page-header`, `app-data-table`, `app-status-chip`, `app-confirm-dialog`, `app-empty-state`, `app-search-bar`, `app-date-badge`, `app-toast`)
- `layout`: sidebar shell
- `features`: auth/dashboard/customers/delivery-notes/invoices/statements/payroll/settings pages

## Export/PDF

PDFs are generated server-side via QuestPDF for:

- Delivery Notes
- Invoices
- Account Statements
- Salary Slips
