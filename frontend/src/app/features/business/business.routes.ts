import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';
import { CustomersPageComponent } from '../customers/customers-page/customers-page.component';
import { DashboardPageComponent } from '../dashboard/dashboard-page/dashboard-page.component';
import { DeliveryNotesPageComponent } from '../delivery-notes/delivery-notes-page/delivery-notes-page.component';
import { ExpenseCategoriesPageComponent } from '../expense-categories/expense-categories-page/expense-categories-page.component';
import { ExpensesPageComponent } from '../expenses/expenses-page/expenses-page.component';
import { InvoicesPageComponent } from '../invoices/invoices-page/invoices-page.component';
import { MiraPageComponent } from '../mira/mira-page/mira-page.component';
import { PaymentVouchersPageComponent } from '../payment-vouchers/payment-vouchers-page/payment-vouchers-page.component';
import { PayrollPageComponent } from '../payroll/payroll-page/payroll-page.component';
import { PoPageComponent } from '../po/po-page/po-page.component';
import { QuotePageComponent } from '../quote/quote-page/quote-page.component';
import { ReceivedInvoicesPageComponent } from '../received-invoices/received-invoices-page/received-invoices-page.component';
import { ReportsPageComponent } from '../reports/reports-page/reports-page.component';
import { RentPageComponent } from '../rent/rent-page/rent-page.component';
import { SettingsPageComponent } from '../settings/settings-page/settings-page.component';
import { StaffConductPageComponent } from '../staff-conduct/staff-conduct-page/staff-conduct-page.component';
import { StatementsPageComponent } from '../statements/statements-page/statements-page.component';
import { SuppliersPageComponent } from '../suppliers/suppliers-page/suppliers-page.component';
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
      { path: 'suppliers', component: SuppliersPageComponent },
      { path: 'received-invoices', component: ReceivedInvoicesPageComponent },
      { path: 'payment-vouchers', component: PaymentVouchersPageComponent },
      { path: 'rent', component: RentPageComponent },
      { path: 'bpt', loadComponent: () => import('../bpt/bpt-page/bpt-page.component').then((m) => m.BptPageComponent) },
      { path: 'mira', component: MiraPageComponent },
      { path: 'expense-ledger', component: ExpensesPageComponent },
      { path: 'expense-categories', component: ExpenseCategoriesPageComponent },
      { path: 'po', component: PoPageComponent },
      { path: 'quote', component: QuotePageComponent },
      { path: 'account-statements', component: StatementsPageComponent },
      { path: 'customers', component: CustomersPageComponent },
      { path: 'payroll', component: PayrollPageComponent },
      { path: 'staff-conduct', component: StaffConductPageComponent },
      { path: 'reports', component: ReportsPageComponent },
      { path: 'settings', component: SettingsPageComponent }
    ]
  },
  { path: 'dashboard', redirectTo: 'app/dashboard', pathMatch: 'full' },
  { path: 'delivery-notes', redirectTo: 'app/delivery-notes', pathMatch: 'full' },
  { path: 'sales-history', redirectTo: 'app/sales-history', pathMatch: 'full' },
  { path: 'suppliers', redirectTo: 'app/suppliers', pathMatch: 'full' },
  { path: 'received-invoices', redirectTo: 'app/received-invoices', pathMatch: 'full' },
  { path: 'payment-vouchers', redirectTo: 'app/payment-vouchers', pathMatch: 'full' },
  { path: 'rent', redirectTo: 'app/rent', pathMatch: 'full' },
  { path: 'bpt', redirectTo: 'app/bpt', pathMatch: 'full' },
  { path: 'mira', redirectTo: 'app/mira', pathMatch: 'full' },
  { path: 'expense-ledger', redirectTo: 'app/expense-ledger', pathMatch: 'full' },
  { path: 'expense-categories', redirectTo: 'app/expense-categories', pathMatch: 'full' },
  { path: 'po', redirectTo: 'app/po', pathMatch: 'full' },
  { path: 'quote', redirectTo: 'app/quote', pathMatch: 'full' },
  { path: 'account-statements', redirectTo: 'app/account-statements', pathMatch: 'full' },
  { path: 'customers', redirectTo: 'app/customers', pathMatch: 'full' },
  { path: 'payroll', redirectTo: 'app/payroll', pathMatch: 'full' },
  { path: 'staff-conduct', redirectTo: 'app/staff-conduct', pathMatch: 'full' },
  { path: 'reports', redirectTo: 'app/reports', pathMatch: 'full' },
  { path: 'settings', redirectTo: 'app/settings', pathMatch: 'full' }
];
