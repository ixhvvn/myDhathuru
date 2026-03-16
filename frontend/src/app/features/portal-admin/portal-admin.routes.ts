import { Routes } from '@angular/router';
import { adminAuthHostGuard } from '../../core/guards/host-routing.guard';
import { portalAdminAuthPageGuard } from '../../core/guards/portal-admin-auth-page.guard';
import { portalAdminAuthGuard } from '../../core/guards/portal-admin-auth.guard';
import { AuthLayoutComponent } from '../../layout/auth-layout/auth-layout.component';
import { PortalAdminShellComponent } from '../../layout/portal-admin-shell/portal-admin-shell.component';
import { PortalAdminAccountControlsPageComponent } from './account-controls/portal-admin-account-controls-page.component';
import { PortalAdminAuditLogsPageComponent } from './audit-logs/portal-admin-audit-logs-page.component';
import { PortalAdminLoginPageComponent } from './auth/portal-admin-login-page.component';
import { PortalAdminResetPasswordPageComponent } from './auth/portal-admin-reset-password-page.component';
import { PortalAdminBillingCustomRatesPageComponent } from './billing/portal-admin-billing-custom-rates-page.component';
import { PortalAdminBillingDashboardPageComponent } from './billing/portal-admin-billing-dashboard-page.component';
import { PortalAdminBillingGeneratePageComponent } from './billing/portal-admin-billing-generate-page.component';
import { PortalAdminBillingInvoicesPageComponent } from './billing/portal-admin-billing-invoices-page.component';
import { PortalAdminBillingSettingsPageComponent } from './billing/portal-admin-billing-settings-page.component';
import { PortalAdminBillingStatementsPageComponent } from './billing/portal-admin-billing-statements-page.component';
import { PortalAdminBusinessesPageComponent } from './businesses/portal-admin-businesses-page.component';
import { PortalAdminDashboardPageComponent } from './dashboard/portal-admin-dashboard-page.component';
import { PortalAdminEmailServicePageComponent } from './email-service/portal-admin-email-service-page.component';
import { PortalAdminSettingsPageComponent } from './settings/portal-admin-settings-page.component';
import { PortalAdminSignupRequestsPageComponent } from './signup-requests/portal-admin-signup-requests-page.component';
import { PortalAdminUsersPageComponent } from './users/portal-admin-users-page.component';

export const portalAdminRoutes: Routes = [
  {
    path: 'portal-admin',
    component: AuthLayoutComponent,
    canActivate: [adminAuthHostGuard],
    children: [
      { path: 'login', component: PortalAdminLoginPageComponent, canActivate: [portalAdminAuthPageGuard] },
      { path: 'forgot-password', component: PortalAdminLoginPageComponent, canActivate: [portalAdminAuthPageGuard] },
      { path: 'reset-password', component: PortalAdminResetPasswordPageComponent, canActivate: [portalAdminAuthPageGuard] }
    ]
  },
  {
    path: 'portal-admin',
    component: PortalAdminShellComponent,
    canActivate: [portalAdminAuthGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: PortalAdminDashboardPageComponent },
      { path: 'signup-requests', component: PortalAdminSignupRequestsPageComponent },
      { path: 'businesses', component: PortalAdminBusinessesPageComponent },
      { path: 'business-users', component: PortalAdminUsersPageComponent },
      { path: 'email-service', component: PortalAdminEmailServicePageComponent },
      { path: 'account-controls', component: PortalAdminAccountControlsPageComponent },
      { path: 'billing', component: PortalAdminBillingDashboardPageComponent },
      { path: 'billing/dashboard', redirectTo: 'billing', pathMatch: 'full' },
      { path: 'billing/invoices', component: PortalAdminBillingInvoicesPageComponent },
      { path: 'billing/statements', component: PortalAdminBillingStatementsPageComponent },
      { path: 'billing/generate', component: PortalAdminBillingGeneratePageComponent },
      { path: 'billing/custom-rates', component: PortalAdminBillingCustomRatesPageComponent },
      { path: 'billing/settings', component: PortalAdminBillingSettingsPageComponent },
      { path: 'audit-logs', component: PortalAdminAuditLogsPageComponent },
      { path: 'settings', component: PortalAdminSettingsPageComponent },
      { path: 'users', redirectTo: 'business-users', pathMatch: 'full' }
    ]
  }
];
