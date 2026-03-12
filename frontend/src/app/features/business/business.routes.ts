import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';
import { CustomersPageComponent } from '../customers/customers-page/customers-page.component';
import { DashboardPageComponent } from '../dashboard/dashboard-page/dashboard-page.component';
import { DeliveryNotesPageComponent } from '../delivery-notes/delivery-notes-page/delivery-notes-page.component';
import { InvoicesPageComponent } from '../invoices/invoices-page/invoices-page.component';
import { PayrollPageComponent } from '../payroll/payroll-page/payroll-page.component';
import { ReportsPageComponent } from '../reports/reports-page/reports-page.component';
import { SettingsPageComponent } from '../settings/settings-page/settings-page.component';
import { StatementsPageComponent } from '../statements/statements-page/statements-page.component';
import { AppShellComponent } from '../../layout/app-shell/app-shell.component';

export const businessRoutes: Routes = [
  {
    path: 'app',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: DashboardPageComponent },
      { path: 'delivery-notes', component: DeliveryNotesPageComponent },
      { path: 'sales-history', component: InvoicesPageComponent },
      { path: 'account-statements', component: StatementsPageComponent },
      { path: 'customers', component: CustomersPageComponent },
      { path: 'payroll', component: PayrollPageComponent },
      { path: 'reports', component: ReportsPageComponent },
      { path: 'settings', component: SettingsPageComponent }
    ]
  },
  { path: 'dashboard', redirectTo: 'app/dashboard', pathMatch: 'full' },
  { path: 'delivery-notes', redirectTo: 'app/delivery-notes', pathMatch: 'full' },
  { path: 'sales-history', redirectTo: 'app/sales-history', pathMatch: 'full' },
  { path: 'account-statements', redirectTo: 'app/account-statements', pathMatch: 'full' },
  { path: 'customers', redirectTo: 'app/customers', pathMatch: 'full' },
  { path: 'payroll', redirectTo: 'app/payroll', pathMatch: 'full' },
  { path: 'reports', redirectTo: 'app/reports', pathMatch: 'full' },
  { path: 'settings', redirectTo: 'app/settings', pathMatch: 'full' }
];
