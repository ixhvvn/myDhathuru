import { CommonModule } from '@angular/common';
import { Component, HostListener, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import { PortalAdminAuthService } from '../../core/services/portal-admin-auth.service';

type AdminNavItem = {
  path: string;
  label: string;
  iconPaths: string[];
};

@Component({
  selector: 'app-portal-admin-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="admin-shell" [class.menu-open]="mobileMenuOpen()">
      <header class="mobile-topbar">
        <button type="button" class="menu-btn" [class.active]="mobileMenuOpen()" (click)="toggleMobileMenu()">
          <span></span><span></span><span></span>
        </button>
        <div class="mobile-brand">
          <img src="/logo.svg" alt="myDhathuru logo">
          <strong>Portal Admin</strong>
        </div>
        <button type="button" class="top-logout" (click)="logout()">Logout</button>
      </header>

      <div class="mobile-overlay" [class.visible]="mobileMenuOpen()" (click)="closeMobileMenu()"></div>

      <aside class="sidebar" [class.mobile-open]="mobileMenuOpen()">
        <div class="sidebar-header">
          <div class="brand">
            <img src="/logo.svg" alt="myDhathuru logo">
            <div>
              <h2>myDhathuru</h2>
              <p>Portal Admin</p>
            </div>
          </div>

          <button type="button" class="sidebar-close" aria-label="Close navigation menu" (click)="closeMobileMenu()">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M6 6l12 12"></path>
              <path d="M18 6L6 18"></path>
            </svg>
          </button>
        </div>

        <section class="nav-panel">
          <div class="nav-panel__head">
            <span>Portal Navigation</span>
            <small>{{ navItems.length }} modules</small>
          </div>

          <nav>
            <a
              *ngFor="let item of navItems"
              [routerLink]="item.path"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: true }"
              (click)="onNavItemClick()">
              <span class="icon" aria-hidden="true">
                <svg viewBox="0 0 24 24">
                  <path *ngFor="let iconPath of item.iconPaths" [attr.d]="iconPath"></path>
                </svg>
              </span>
              <span>{{ item.label }}</span>
            </a>
          </nav>
        </section>

        <div class="footer">
          <div class="footer-card">
            <span class="footer-chip">Portal Super Admin</span>
            <div class="user">
              <span class="avatar">{{ initials() }}</span>
              <div class="meta">
                <strong>{{ auth.user()?.fullName || 'Super Admin' }}</strong>
                <small>{{ auth.user()?.email }}</small>
              </div>
            </div>
            <button type="button" class="logout-btn" (click)="logout()">Logout</button>
          </div>
        </div>
      </aside>

      <main class="content">
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: `
    .admin-shell {
      display: grid;
      grid-template-columns: 300px 1fr;
      gap: 1rem;
      min-height: 100dvh;
      padding: 1rem;
      overflow: hidden;
      background:
        radial-gradient(circle at 8% 8%, rgba(126, 140, 247, .16), transparent 36%),
        radial-gradient(circle at 90% 10%, rgba(114, 219, 225, .16), transparent 40%),
        linear-gradient(165deg, #f7f9ff 0%, #eef4ff 100%);
    }
    .mobile-topbar,
    .mobile-overlay {
      display: none;
    }
    .sidebar {
      border-radius: 22px;
      border: 1px solid rgba(255,255,255,.82);
      background:
        radial-gradient(circle at 12% 10%, rgba(143, 161, 255, .16), transparent 34%),
        radial-gradient(circle at 88% 90%, rgba(108, 220, 206, .14), transparent 30%),
        linear-gradient(170deg, rgba(255,255,255,.88), rgba(243,248,255,.8));
      backdrop-filter: blur(12px);
      box-shadow: 0 24px 44px rgba(61, 83, 136, .14);
      padding: .78rem;
      display: grid;
      grid-template-rows: auto 1fr auto;
      gap: .72rem;
      height: calc(100dvh - 2rem);
      position: sticky;
      top: 1rem;
      overflow: hidden;
    }
    .sidebar-header {
      display: flex;
      align-items: center;
      gap: .5rem;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: .65rem;
      border: 1px solid rgba(194, 208, 242, .76);
      border-radius: 18px;
      background:
        linear-gradient(140deg, rgba(248,251,255,.98), rgba(236,243,255,.9)),
        radial-gradient(circle at 12% 16%, rgba(148, 169, 255, .12), transparent 34%);
      box-shadow:
        inset 0 1px 0 rgba(255,255,255,.9),
        0 10px 22px rgba(89, 110, 167, .08);
      padding: .6rem .66rem;
      flex: 1;
      min-width: 0;
    }
    .brand img {
      width: 42px;
      height: 42px;
      border-radius: 13px;
      border: 1px solid #d5dff6;
      object-fit: cover;
      background: #fff;
      box-shadow: 0 10px 20px rgba(103, 125, 184, .14);
    }
    .brand h2 {
      margin: 0;
      color: #304264;
      font-family: var(--font-heading);
      font-size: 1.08rem;
      font-weight: 600;
      line-height: 1.1;
    }
    .brand p {
      margin: .16rem 0 0;
      color: #60729a;
      font-size: .76rem;
    }
    .sidebar-close {
      display: none;
      width: 40px;
      height: 40px;
      border-radius: 14px;
      border: 1px solid rgba(194, 208, 242, .76);
      background: linear-gradient(145deg, rgba(248,251,255,.96), rgba(236,243,255,.88));
      color: #4f648f;
      align-items: center;
      justify-content: center;
      box-shadow: inset 0 1px 0 rgba(255,255,255,.9);
    }
    .sidebar-close svg {
      width: 18px;
      height: 18px;
      stroke: currentColor;
      stroke-width: 2.2;
      fill: none;
      stroke-linecap: round;
      stroke-linejoin: round;
    }
    .nav-panel {
      min-height: 0;
      display: grid;
      grid-template-rows: auto 1fr;
      gap: .55rem;
      padding: .58rem;
      border-radius: 20px;
      border: 1px solid rgba(198, 211, 242, .72);
      background:
        linear-gradient(180deg, rgba(255,255,255,.82), rgba(242,247,255,.72)),
        radial-gradient(circle at 100% 0%, rgba(130, 222, 211, .08), transparent 35%);
      box-shadow: inset 0 1px 0 rgba(255,255,255,.86);
    }
    .nav-panel__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: .5rem;
      padding: 0 .1rem;
    }
    .nav-panel__head span,
    .nav-panel__head small {
      color: #6177a4;
      font-size: .72rem;
      text-transform: uppercase;
      letter-spacing: .08em;
      font-weight: 700;
    }
    nav {
      display: grid;
      align-content: start;
      gap: .3rem;
      overflow: auto;
      padding-right: .15rem;
    }
    nav a {
      display: flex;
      align-items: center;
      gap: .62rem;
      color: #4e5f7f;
      text-decoration: none;
      border-radius: 15px;
      border: 1px solid transparent;
      padding: .48rem .56rem;
      font-family: var(--font-heading);
      font-weight: 600;
      transition: background .2s ease, color .2s ease, border-color .2s ease;
    }
    nav a .icon {
      width: 34px;
      height: 34px;
      border-radius: 11px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(145deg, rgba(225, 234, 255, .92), rgba(235, 246, 255, .94));
      border: 1px solid rgba(178, 194, 232, .64);
      color: #415b86;
      box-shadow: inset 0 1px 0 rgba(255, 255, 255, .82);
    }
    nav a .icon svg {
      width: 20px;
      height: 20px;
      stroke: currentColor;
      stroke-width: 2.2;
      fill: none;
      stroke-linecap: round;
      stroke-linejoin: round;
    }
    nav a:hover {
      border-color: rgba(90, 112, 152, .22);
      background: rgba(36, 52, 83, .06);
      color: #324565;
    }
    nav a:hover .icon {
      background: linear-gradient(145deg, rgba(218, 230, 255, .95), rgba(229, 245, 255, .95));
      border-color: rgba(164, 184, 228, .72);
      color: #36547f;
    }
    nav a.active {
      color: #2f476f;
      border-color: rgba(156, 177, 223, .62);
      background: linear-gradient(145deg, rgba(220, 232, 255, .74), rgba(231, 246, 255, .72));
      box-shadow: none;
    }
    nav a.active .icon {
      color: #2f4d79;
      border-color: #afc5ec;
      background: linear-gradient(145deg, #d4e3ff, #caf1ea);
      box-shadow:
        0 6px 12px rgba(117, 142, 194, .22),
        inset 0 1px 0 rgba(255, 255, 255, .9);
    }
    .footer {
      padding-top: .05rem;
    }
    .footer-card {
      display: grid;
      gap: .62rem;
      padding: .68rem;
      border-radius: 20px;
      border: 1px solid rgba(194, 208, 242, .76);
      background:
        radial-gradient(circle at 90% 12%, rgba(116, 217, 223, .16), transparent 34%),
        linear-gradient(150deg, rgba(255,255,255,.94), rgba(238,244,255,.86));
      box-shadow:
        inset 0 1px 0 rgba(255,255,255,.92),
        0 14px 30px rgba(87, 110, 173, .12);
    }
    .footer-chip {
      display: inline-flex;
      width: fit-content;
      align-items: center;
      border-radius: 999px;
      padding: .26rem .58rem;
      border: 1px solid rgba(164, 182, 236, .58);
      background: rgba(245, 248, 255, .86);
      color: #6076a2;
      font-size: .7rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: .08em;
    }
    .user {
      display: flex;
      align-items: center;
      gap: .55rem;
      border: 1px solid rgba(214, 224, 247, .8);
      border-radius: 16px;
      padding: .5rem .56rem;
      background: linear-gradient(135deg, rgba(255,255,255,.98), rgba(237,244,255,.88));
      min-width: 0;
      box-shadow: inset 0 1px 0 rgba(255,255,255,.86);
    }
    .avatar {
      width: 38px;
      height: 38px;
      border-radius: 13px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      color: #fff;
      background: linear-gradient(135deg, #7484f8, #67acef);
      font-family: var(--font-heading);
      font-size: .84rem;
      font-weight: 600;
      flex: 0 0 auto;
    }
    .meta {
      min-width: 0;
      color: #3c4b70;
    }
    .meta strong,
    .meta small {
      display: block;
      line-height: 1.2;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .meta strong {
      font-family: var(--font-heading);
      font-size: .9rem;
      font-weight: 600;
    }
    .meta small {
      color: #6578a1;
      font-size: .74rem;
    }
    .logout-btn {
      border: 1px solid #d5def6;
      border-radius: 14px;
      background: linear-gradient(135deg, #7585f8, #6689ed);
      color: #fff;
      font-family: var(--font-heading);
      font-size: .88rem;
      font-weight: 600;
      padding: .66rem .8rem;
      cursor: pointer;
      box-shadow: 0 12px 22px rgba(94, 113, 203, .22);
      width: 100%;
    }
    .logout-btn:hover {
      transform: translateY(-1px);
    }
    .content {
      border-radius: 22px;
      border: 1px solid rgba(255,255,255,.82);
      background: linear-gradient(165deg, rgba(255,255,255,.84), rgba(247,250,255,.76));
      box-shadow: var(--shadow-soft);
      padding: 1rem;
      overflow: auto;
      min-height: 0;
    }
    @media (max-width: 980px) {
      .admin-shell {
        display: block;
        padding: .7rem;
      }
      .mobile-topbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: .5rem;
        border-radius: 18px;
        border: 1px solid rgba(255,255,255,.82);
        background:
          radial-gradient(circle at 10% 20%, rgba(139, 159, 255, .14), transparent 32%),
          linear-gradient(150deg, rgba(255,255,255,.94), rgba(241,247,255,.86));
        box-shadow: 0 18px 30px rgba(73, 94, 148, .12);
        padding: .58rem .64rem;
        position: sticky;
        top: .35rem;
        z-index: 90;
      }
      .menu-btn {
        width: 40px;
        height: 40px;
        border-radius: 12px;
        border: 1px solid #d5e0f8;
        background: linear-gradient(145deg, #f4f7ff, #edf5ff);
        display: inline-flex;
        align-items: center;
        justify-content: center;
        flex-direction: column;
        gap: 4px;
        box-shadow: inset 0 1px 0 rgba(255,255,255,.9);
      }
      .menu-btn span {
        width: 16px;
        height: 2px;
        border-radius: 999px;
        background: #5d6f96;
        transition: transform .22s ease, opacity .2s ease;
      }
      .menu-btn.active span:nth-child(1) { transform: translateY(6px) rotate(45deg); }
      .menu-btn.active span:nth-child(2) { opacity: 0; }
      .menu-btn.active span:nth-child(3) { transform: translateY(-6px) rotate(-45deg); }
      .mobile-brand {
        display: flex;
        align-items: center;
        gap: .5rem;
        min-width: 0;
      }
      .mobile-brand img {
        width: 34px;
        height: 34px;
        border-radius: 10px;
      }
      .mobile-brand strong {
        color: #334868;
        font-family: var(--font-heading);
        font-size: .94rem;
        font-weight: 600;
        white-space: nowrap;
      }
      .top-logout {
        border: 1px solid #d4def6;
        border-radius: 10px;
        background: #f2f6ff;
        color: #516489;
        font-family: var(--font-heading);
        font-size: .8rem;
        font-weight: 600;
        padding: .4rem .58rem;
      }
      .mobile-overlay {
        display: block;
        position: fixed;
        inset: 0;
        background: rgba(30, 41, 68, .35);
        backdrop-filter: blur(2px);
        opacity: 0;
        visibility: hidden;
        pointer-events: none;
        transition: opacity .25s ease;
        z-index: 80;
      }
      .mobile-overlay.visible {
        opacity: 1;
        visibility: visible;
        pointer-events: auto;
      }
      .sidebar {
        position: fixed;
        left: 0;
        top: 0;
        width: min(86vw, 340px);
        height: 100dvh;
        border-radius: 0 20px 20px 0;
        z-index: 95;
        transform: translateX(-104%);
        opacity: 0;
        visibility: hidden;
        pointer-events: none;
        transition: transform .34s cubic-bezier(.22, 1, .36, 1), opacity .24s ease;
      }
      .sidebar-close {
        display: inline-flex;
      }
      .sidebar.mobile-open {
        transform: translateX(0);
        opacity: 1;
        visibility: visible;
        pointer-events: auto;
      }
      .content {
        margin-top: .7rem;
        padding: .82rem .75rem;
      }
    }
    @media (max-width: 560px) {
      .top-logout {
        display: none;
      }
      .mobile-brand strong {
        font-size: .88rem;
      }
      .nav-panel {
        padding: .5rem;
      }
    }
  `
})
export class PortalAdminShellComponent implements OnInit, OnDestroy {
  readonly auth = inject(PortalAdminAuthService);
  private readonly router = inject(Router);
  private routeSubscription: Subscription | null = null;
  readonly mobileMenuOpen = signal(false);
  readonly isMobile = signal(false);

  readonly navItems: AdminNavItem[] = [
    {
      path: '/portal-admin/dashboard',
      label: 'Dashboard',
      iconPaths: ['M3 10.2L12 3l9 7.2V20a1 1 0 0 1-1 1h-5.8v-6.4H9.8V21H4a1 1 0 0 1-1-1z']
    },
    {
      path: '/portal-admin/signup-requests',
      label: 'Signup Requests',
      iconPaths: ['M9 3h6v3H9z', 'M7 5h10a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z', 'M9 11h6', 'M9 15h4']
    },
    {
      path: '/portal-admin/businesses',
      label: 'Businesses',
      iconPaths: ['M4 20h16', 'M6 20V6h12v14', 'M9 9h2', 'M13 9h2', 'M9 13h2', 'M13 13h2']
    },
    {
      path: '/portal-admin/account-controls',
      label: 'Account Controls',
      iconPaths: ['M12 3v2', 'M12 19v2', 'M4.9 4.9l1.4 1.4', 'M17.7 17.7l1.4 1.4', 'M3 12h2', 'M19 12h2', 'M4.9 19.1l1.4-1.4', 'M17.7 6.3l1.4-1.4', 'M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7']
    },
    {
      path: '/portal-admin/business-users',
      label: 'Business Users',
      iconPaths: ['M16 21v-1.5a3 3 0 0 0-3-3H7a3 3 0 0 0-3 3V21', 'M10 11a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7', 'M17 11a2.5 2.5 0 1 0 0-5', 'M20 21v-1.2a2.8 2.8 0 0 0-2.1-2.7']
    },
    {
      path: '/portal-admin/billing',
      label: 'Billing',
      iconPaths: ['M4 20h16', 'M7 20V11', 'M12 20V7', 'M17 20V14']
    },
    {
      path: '/portal-admin/billing/invoices',
      label: 'Invoices',
      iconPaths: ['M7 3h8l5 5v13H7z', 'M15 3v5h5', 'M10 13h7', 'M10 17h7']
    },
    {
      path: '/portal-admin/billing/statements',
      label: 'Account Statements',
      iconPaths: ['M4 20h16', 'M7 3h10v5H7z', 'M7 11h10', 'M7 15h10']
    },
    {
      path: '/portal-admin/billing/generate',
      label: 'Generate Invoices',
      iconPaths: ['M7 3h8l5 5v13H7z', 'M15 3v5h5', 'M12 11v6', 'M9 14h6']
    },
    {
      path: '/portal-admin/billing/custom-rates',
      label: 'Custom Rates',
      iconPaths: ['M6 6h12v12H6z', 'M9 15l6-6', 'M9 9a1 1 0 1 0 0 .01', 'M15 15a1 1 0 1 0 0 .01']
    },
    {
      path: '/portal-admin/billing/settings',
      label: 'Billing Settings',
      iconPaths: ['M4 7h16', 'M4 12h10', 'M4 17h16', 'M16 12h4']
    },
    {
      path: '/portal-admin/audit-logs',
      label: 'Audit Logs',
      iconPaths: ['M8 6h11', 'M8 12h11', 'M8 18h11', 'M4.5 6h.01', 'M4.5 12h.01', 'M4.5 18h.01']
    },
    {
      path: '/portal-admin/settings',
      label: 'Settings',
      iconPaths: ['M12 3v2', 'M12 19v2', 'M4.9 4.9l1.4 1.4', 'M17.7 17.7l1.4 1.4', 'M3 12h2', 'M19 12h2', 'M4.9 19.1l1.4-1.4', 'M17.7 6.3l1.4-1.4', 'M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7']
    }
  ];

  ngOnInit(): void {
    this.syncViewport();
    this.routeSubscription = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => {
        if (this.mobileMenuOpen()) {
          this.closeMobileMenu();
        }
      });
  }

  ngOnDestroy(): void {
    this.routeSubscription?.unsubscribe();
    if (typeof document !== 'undefined') {
      document.body.style.overflow = '';
    }
  }

  initials(): string {
    const name = this.auth.user()?.fullName?.trim();
    if (!name) {
      return 'SA';
    }

    const parts = name.split(/\s+/).filter(Boolean);
    if (parts.length === 1) {
      return parts[0].slice(0, 2).toUpperCase();
    }

    return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  }

  logout(): void {
    this.auth.logout();
  }

  toggleMobileMenu(): void {
    if (!this.isMobile()) {
      return;
    }

    if (this.mobileMenuOpen()) {
      this.closeMobileMenu();
      return;
    }

    this.mobileMenuOpen.set(true);
    if (typeof document !== 'undefined') {
      document.body.style.overflow = 'hidden';
    }
  }

  onNavItemClick(): void {
    if (this.isMobile()) {
      this.closeMobileMenu();
    }
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
    if (typeof document !== 'undefined') {
      document.body.style.overflow = '';
    }
  }

  @HostListener('window:resize')
  onResize(): void {
    this.syncViewport();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.mobileMenuOpen()) {
      this.closeMobileMenu();
    }
  }

  private syncViewport(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const mobile = window.innerWidth <= 980;
    this.isMobile.set(mobile);
    if (!mobile) {
      this.closeMobileMenu();
    }
  }
}

