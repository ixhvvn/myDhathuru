import { Routes } from '@angular/router';
import { businessAuthHostGuard } from '../../core/guards/host-routing.guard';
import { AuthLayoutComponent } from '../../layout/auth-layout/auth-layout.component';
import { LoginPageComponent } from './login-page/login-page.component';
import { ResetPasswordPageComponent } from './reset-password-page/reset-password-page.component';

export const authRoutes: Routes = [
  {
    path: '',
    component: AuthLayoutComponent,
    canActivate: [businessAuthHostGuard],
    children: [
      { path: 'login', component: LoginPageComponent },
      { path: 'forgot-password', component: LoginPageComponent },
      { path: 'reset-password', component: ResetPasswordPageComponent }
    ]
  }
];
