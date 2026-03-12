# myDhathuru

Production-ready multi-tenant portal for:

- Delivery notes
- Invoicing + payment recording
- Customer accounts / account statements
- Customer master data
- Payroll, salary runs, salary slips
- Tenant-specific settings
- Platform-level Portal Admin / Super Admin controls

This implementation was built from workbook-derived business intent and includes tenant isolation, JWT auth, PostgreSQL persistence, migrations, Angular UI, and Docker.

## Repository Structure

```text
.
+- backend/
|  +- Dockerfile
|  +- src/
|     +- MyDhathuru.Api/                # Controllers, middleware, Program bootstrap
|     +- MyDhathuru.Application/         # DTOs, interfaces, validators, shared contracts
|     +- MyDhathuru.Domain/              # Entities, enums, base abstractions
|     +- MyDhathuru.Infrastructure/      # EF Core, auth, services, PDF export, migrations
+- frontend/
|  +- Dockerfile
|  +- nginx.conf
|  +- src/app/
|     +- core/                           # Auth, interceptors, guards, inactivity, API helpers
|     +- shared/                         # Reusable UI components/pipes
|     +- layout/                         # App shell + portal admin shell
|     +- features/                       # Tenant + portal admin feature modules
+- infra/
|  +- nginx/
|     +- default.conf                    # Docker reverse proxy (localhost + host-aware routing)
|     +- production/
|        +- mydhathuru.vps.conf          # VPS hostname routing (www/app/admin)
|        +- snippets/mydhathuru-proxy-headers.conf
+- docs/
|  +- ARCHITECTURE.md
|  +- WORKBOOK_ASSUMPTIONS.md
+- docker-compose.yml
+- .env.example
+- .env.production.example
+- dotnet-tools.json                     # Local EF tool manifest
```

## Database Schema Summary

Core tables:

- Tenants
- Roles
- Users
- RefreshTokens
- PasswordResetTokens
- SignupRequests
- AdminAuditLogs
- TenantSettings
- DocumentSequences
- Customers
- CustomerContacts
- Vessels
- DeliveryNotes
- DeliveryNoteItems
- Invoices
- InvoiceItems
- InvoicePayments
- CustomerOpeningBalances
- Staff
- PayrollPeriods
- PayrollEntries
- SalarySlips

Entity conventions:

- `Id` (Guid)
- audit fields (`CreatedAt`, `UpdatedAt`, `CreatedByUserId`, `UpdatedByUserId`)
- soft-delete (`IsDeleted`)
- business-scoped entities inherit `TenantEntity` and include `TenantId`

Indexes include tenant-aware unique constraints for document numbers and master references.

## Tenant Isolation Strategy

1. JWT includes `tenant_id` claim.
2. `TenantResolutionMiddleware` sets current tenant from authenticated token only.
3. EF Core global query filters enforce `TenantId == CurrentTenant` on all `TenantEntity` types.
4. Services never trust client-provided tenant IDs.
5. Tenant-aware unique indexes prevent cross-tenant number collisions.
6. Document numbers are generated tenant-wise using `DocumentSequence`.

## Payroll Formula Assumptions Used

Derived from `Salary Details.xlsx` + `Salary template.xlsx` with broken links corrected:

- `SUB_TOTAL = BASIC + SERVICE_ALLOWANCE + OTHER_ALLOWANCE`
- `RATE_PER_DAY = SUB_TOTAL / PERIOD_DAYS`
- `ABSENT_DAYS = PERIOD_DAYS - ATTENDED_DAYS`
- `ABSENT_DEDUCTION = RATE_PER_DAY * ABSENT_DAYS` (display metric)
- `FOOD_ALLOWANCE = FOOD_ALLOWANCE_RATE * FOOD_ALLOWANCE_DAYS`
- `PENSION_DEDUCTION = BASIC * 0.07`
- `TOTAL_PAY = (RATE_PER_DAY * ATTENDED_DAYS) + PHONE_ALLOWANCE + FOOD_ALLOWANCE + OT_PAY`
- `NET_PAYABLE = TOTAL_PAY - PENSION_DEDUCTION - SALARY_ADVANCE`

Full assumptions: `docs/WORKBOOK_ASSUMPTIONS.md`.

## Authentication

- Login first page
- Public signup creates a `Pending` signup request (no immediate tenant activation)
- Portal Admin approves/rejects requests
  - Approve: creates tenant + admin user + tenant settings and sends approval email
  - Reject: stores required reason and sends rejection email
- JWT access token + refresh token rotation
- Role-based authorization (`SuperAdmin`, `Admin`, `Staff`)
- Forgot/reset password flow for tenant users and portal admins
- Frontend auto-logout after 1 hour inactivity

### Portal Admin Routes

- `/portal-admin/login`
- `/portal-admin/forgot-password`
- `/portal-admin/reset-password`
- `/portal-admin/dashboard`
- `/portal-admin/signup-requests`
- `/portal-admin/businesses`
- `/portal-admin/users`
- `/portal-admin/audit-logs`
- `/portal-admin/settings`

## API Coverage

Implemented endpoints for:

- Auth: signup request submit, login, refresh, forgot password, reset password, current profile
- Dashboard: live summary + analytics
- Customers + Vessels: CRUD/list
- Delivery Notes: CRUD/list/filter/create invoice/export
- Invoices: CRUD/list/filter/receive payment/payment history/export
- Account Statements: statement query/opening balance save/export
- Payroll: staff CRUD, period create/list/get/recalculate, entry update, salary slip export
- Settings: get/update/change password
- Portal Admin Auth: login, refresh, forgot password, reset password, me, change password
- Portal Admin Dashboard: summary KPIs + recent actions
- Portal Admin Signup Requests: list, counts, detail, approve, reject
- Portal Admin Businesses: list, detail, disable, enable, update login details, send reset link, users
- Portal Admin Audit Logs: list/filter

Swagger available in development at `/swagger`.

## PDF/Print Exports

Server-side PDF generation for:

- Delivery notes
- Invoices
- Account statements
- Salary slips
- Payroll period summary

## Run with Docker (Recommended)

1. Copy environment file:

```bash
cp .env.example .env
```

2. Update `JWT_KEY` in `.env` to a strong secret.
3. Configure SMTP values in `.env` (Gmail app password works with `smtp.gmail.com:587`).
4. Configure portal-admin bootstrap values in `.env`:
   - `PORTAL_ADMIN_EMAIL`
   - `PORTAL_ADMIN_PASSWORD`
   - `PORTAL_ADMIN_FULL_NAME`
5. Start services:

```bash
docker compose up --build
```

6. Access:

- Frontend app (direct container): `http://localhost:4201`
- Frontend (via reverse proxy): `http://localhost:81`
- Portal Admin Login: `http://localhost:81/portal-admin/login`
- Backend API (proxied): `http://localhost:81/api`
- Swagger (proxied): `http://localhost:81/swagger`
- Backend API (direct): `http://localhost:8081`
- Swagger (direct): `http://localhost:8081/swagger`

## Production Domain Routing (Squarespace + VPS)

This repo stays as one backend + one frontend codebase. Hostname routing is handled by NGINX.

Target domains:

- `www.mydhathuru.com` -> public marketing homepage
- `app.mydhathuru.com` -> business/customer login + `/app/...` portal
- `admin.mydhathuru.com` -> super admin login + `/portal-admin/...` portal
- `mydhathuru.com` -> redirects to `www.mydhathuru.com`

### Squarespace DNS records

In Squarespace DNS, point all records to the VPS public IP:

- `@` (A) -> `<VPS_PUBLIC_IP>`
- `www` (A) -> `<VPS_PUBLIC_IP>`
- `app` (A) -> `<VPS_PUBLIC_IP>`
- `admin` (A) -> `<VPS_PUBLIC_IP>`

### VPS NGINX setup

Use:

- `infra/nginx/production/mydhathuru.vps.bootstrap.conf` for first-time HTTP + ACME bootstrap
- `infra/nginx/production/mydhathuru.vps.conf`
- `infra/nginx/production/snippets/mydhathuru-proxy-headers.conf`

Copy these into the VPS NGINX config locations (for example `/etc/nginx/sites-available/` and `/etc/nginx/snippets/`).
For a fresh server, enable `mydhathuru.vps.bootstrap.conf` first, issue certificates, then switch the site to `mydhathuru.vps.conf` and reload NGINX.
Keep your existing `echoeswebsite` config as the `default_server` for raw `129.121.79.40` traffic. `myDhathuru` should only answer the three named hosts above.

The production config routes by `server_name` and:

- redirects apex to `www`
- redirects HTTP to HTTPS
- proxies frontend routes to `127.0.0.1:4201`
- proxies `/api` to `127.0.0.1:8081`
- blocks `/swagger` on public domains by default (intentionally not exposed)
- redirects wrong-area paths across hosts (`www` <-> `app` <-> `admin`) for cleaner UX

### Production env

Start from `.env.production.example` and set real secrets/credentials before deploy.
For a VPS that already serves another site on port `80`, run Compose with the production override so Docker only binds localhost ports:

```bash
docker compose \
  --env-file .env.production \
  -f docker-compose.yml \
  -f docker-compose.production.yml \
  up -d --build
```

Important keys:

- `FRONTEND_URL=https://app.mydhathuru.com`
- `ADMIN_FRONTEND_URL=https://admin.mydhathuru.com`
- `CORS_ALLOWED_ORIGIN_0..4` with `www/app/admin` origins
- `BACKEND_HOST_PORT` and `FRONTEND_APP_PORT` must match the upstreams in `infra/nginx/production/mydhathuru.vps.conf`
- `FRONTEND_HOST_PORT` is only used if you explicitly enable the optional Docker `reverse-proxy` service

Detailed same-VPS deployment steps: `docs/VPS_DEPLOYMENT.md`

## Run Locally Without Docker

### Prerequisites

- .NET SDK 10
- Node 24+
- npm 11+
- PostgreSQL 17+

### Backend

```bash
# from repo root
dotnet restore

dotnet build myDhathuru.slnx

# apply migration
dotnet dotnet-ef database update \
  --project backend/src/MyDhathuru.Infrastructure/MyDhathuru.Infrastructure.csproj \
  --startup-project backend/src/MyDhathuru.Api/MyDhathuru.Api.csproj

# run API
dotnet run --project backend/src/MyDhathuru.Api/MyDhathuru.Api.csproj
```

### Frontend

```bash
cd frontend
npm install
npm run start
```

Frontend dev server URL: `http://localhost:4200`

## Migrations

Latest migration added:

- `backend/src/MyDhathuru.Infrastructure/Persistence/Migrations/20260311174238_AddPortalAdminApprovalFlow.cs`

Create a new migration later:

```bash
dotnet dotnet-ef migrations add <MigrationName> \
  --project backend/src/MyDhathuru.Infrastructure/MyDhathuru.Infrastructure.csproj \
  --startup-project backend/src/MyDhathuru.Api/MyDhathuru.Api.csproj
```

## Notes

- No fake business documents are seeded by default.
- Roles are ensured at runtime (`Admin`, `Staff`, `SuperAdmin`).
- A bootstrap super admin account is ensured at startup from `PortalAdmin` configuration.
- Forgot password and signup decision emails are sent by SMTP.
- Workbook extraction artifact generated during analysis: `../workbook_analysis.json` (outside this repo path).
