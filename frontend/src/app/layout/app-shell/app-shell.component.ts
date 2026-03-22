import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { InactivityService } from '../../core/services/inactivity.service';
import { ToastService } from '../../core/services/toast.service';
import { extractApiError } from '../../core/utils/api-error.util';
import { PortalApiService } from '../../features/services/portal-api.service';

type NavItem = {
  path: string;
  label: string;
  iconPaths: string[];
};

type NavSection = {
  label: string;
  items: NavItem[];
};

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, ReactiveFormsModule],
  template: `
    <div
      class="shell"
      [class.shell-collapsed]="isDesktopCollapsed()"
      [class.menu-open]="mobileMenuOpen()"
      [class.sidebar-resizing]="sidebarResizeAnimating()">
      <header class="mobile-topbar">
        <button
          class="icon-btn"
          type="button"
          [class.active]="mobileMenuOpen()"
          aria-label="Toggle navigation menu"
          (click)="onMobileMenuButtonClick($event)">
          <span></span>
          <span></span>
          <span></span>
        </button>
        <div class="mobile-brand">
          <img class="logo" src="/newlogo.png" alt="myDhathuru logo">
          <strong>myDhathuru</strong>
        </div>
        <button class="logout-mini" type="button" (click)="logout()">Logout</button>
      </header>

      <div class="mobile-backdrop" [class.visible]="mobileMenuOpen()" (click)="onMobileBackdropClick($event)"></div>

      <aside
        class="sidebar"
        [class.mobile-open]="mobileMenuOpen()"
        [class.sidebar-collapsed]="isDesktopCollapsed()">
        <div class="sidebar-top" [class.compact-top]="isDesktopCollapsed()">
          <div class="brand">
            <img class="logo" src="/newlogo.png" alt="myDhathuru logo">
            <div class="brand-copy">
              <h2>myDhathuru</h2>
              <p>{{ authService.user()?.companyName }}</p>
            </div>
          </div>

          <button
            class="collapse-btn desktop-only"
            type="button"
            [attr.aria-label]="isDesktopCollapsed() ? 'Expand navigation' : 'Collapse navigation'"
            (click)="toggleSidebarCollapse()">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path *ngIf="!isDesktopCollapsed()" d="M15 6l-6 6 6 6"></path>
              <path *ngIf="isDesktopCollapsed()" d="M9 6l6 6-6 6"></path>
            </svg>
          </button>

          <button class="collapse-btn mobile-only" type="button" aria-label="Close navigation menu" (click)="onMobileCloseClick($event)">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M6 6l12 12"></path>
              <path d="M18 6l-12 12"></path>
            </svg>
          </button>
        </div>

        <nav>
          <section class="nav-section" *ngFor="let section of navSections">
            <p class="nav-section-title" *ngIf="!isDesktopCollapsed()">{{ section.label }}</p>
            <a
              *ngFor="let item of section.items"
              [routerLink]="item.path"
              routerLinkActive="active"
              [title]="isDesktopCollapsed() ? item.label : ''">
              <span class="nav-icon" aria-hidden="true">
                <svg viewBox="0 0 24 24">
                  <path *ngFor="let iconPath of item.iconPaths" [attr.d]="iconPath"></path>
                </svg>
              </span>
              <span class="nav-text">{{ item.label }}</span>
            </a>
          </section>
        </nav>

        <div class="sidebar-footer">
          <section class="support-card">
            <div class="support-card-head">
              <div class="support-icon" aria-hidden="true">
                <span>?</span>
              </div>
              <div class="support-copy">
                <h3>Assistance</h3>
                <p>Call for help or send a quick bug report.</p>
              </div>
            </div>
            <div class="support-actions">
              <a class="support-btn" [href]="supportPhoneLink">
                <span class="support-action-title">Call Support</span>
                <small>{{ supportPhone }}</small>
              </a>
              <button class="bug-btn" type="button" (click)="openBugDialog()">
                <span class="support-action-title">Report Bug</span>
                <small>Attach 1 image</small>
              </button>
            </div>
          </section>

          <p class="developed-by">developed by ixhvvn</p>

          <div class="user-box">
            <div class="user-profile">
              <span class="user-avatar">{{ userInitials() }}</span>
              <div class="user-meta">
                <strong>{{ profileDisplayName() }}</strong>
                <small>{{ authService.user()?.role }}</small>
              </div>
            </div>
            <button class="logout-btn" (click)="logout()" aria-label="Logout">Logout</button>
          </div>
        </div>
      </aside>

      <main class="content">
        <router-outlet></router-outlet>
      </main>

      <div class="bug-backdrop" *ngIf="bugDialogOpen()" (click)="closeBugDialog()"></div>
      <section class="bug-modal" *ngIf="bugDialogOpen()" role="dialog" aria-modal="true" aria-labelledby="bugModalTitle">
        <h3 id="bugModalTitle">Report a Bug</h3>
        <p>Send a detailed issue report to our support inbox. You can attach one image up to {{ bugAttachmentLimitLabel }}.</p>

        <form [formGroup]="bugForm" (ngSubmit)="submitBugReport()">
          <label for="bugSubject">Issue title</label>
          <input id="bugSubject" type="text" formControlName="subject" maxlength="160" placeholder="Short summary of the issue">
          <small class="field-error" *ngIf="bugForm.controls.subject.touched && bugForm.controls.subject.invalid">
            Issue title is required.
          </small>

          <label for="bugDescription">What happened?</label>
          <textarea id="bugDescription" formControlName="description" rows="5" maxlength="3000" placeholder="Describe the problem and how to reproduce it"></textarea>
          <small class="field-error" *ngIf="bugForm.controls.description.touched && bugForm.controls.description.invalid">
            Description is required.
          </small>

          <div class="bug-attachment-field">
            <span class="bug-attachment-label">Attach image (optional)</span>
            <input
              #bugImageInput
              class="bug-file-input"
              type="file"
              accept="image/png,image/jpeg,image/webp,image/gif"
              (change)="onBugAttachmentPicked($event)">

            <button
              type="button"
              class="bug-dropzone"
              [disabled]="bugSubmitPending()"
              [class.dragging]="bugAttachmentDragActive()"
              [class.disabled]="bugSubmitPending()"
              [attr.aria-label]="bugAttachmentFile() ? 'Replace attached image' : 'Attach image'"
              (click)="openBugAttachmentPicker()"
              (dragover)="onBugAttachmentDragOver($event)"
              (dragleave)="onBugAttachmentDragLeave($event)"
              (drop)="onBugAttachmentDrop($event)">
              <ng-container *ngIf="bugAttachmentPreviewUrl() as preview; else emptyBugAttachmentState">
                <img [src]="preview" alt="Attachment preview">
                <span>{{ bugAttachmentFile()?.name }}</span>
                <small>{{ bugAttachmentSizeLabel() }} · Drop another image or click to replace</small>
              </ng-container>
              <ng-template #emptyBugAttachmentState>
                <span>Drag and drop image here</span>
                <small>or click to browse (PNG, JPG, WEBP, GIF up to {{ bugAttachmentLimitLabel }})</small>
              </ng-template>
            </button>

            <button
              *ngIf="bugAttachmentFile()"
              class="bug-attachment-remove"
              type="button"
              [disabled]="bugSubmitPending()"
              (click)="removeBugAttachment($event)">
              Remove image
            </button>
          </div>

          <div class="bug-actions">
            <button class="btn-secondary" type="button" (click)="closeBugDialog()">Cancel</button>
            <button class="btn-primary" type="submit" [disabled]="bugSubmitPending()">
              {{ bugSubmitPending() ? 'Sending...' : 'Send Report' }}
            </button>
          </div>
        </form>
      </section>
    </div>
  `,
  styles: `
    .shell {
      display: grid;
      grid-template-columns: 290px 1fr;
      gap: 1rem;
      height: 100dvh;
      min-height: 100vh;
      padding: 1rem;
      align-items: stretch;
      overflow: hidden;
      transition: grid-template-columns .15s cubic-bezier(.25,.8,.25,1);
    }
    .shell.shell-collapsed {
      grid-template-columns: 90px 1fr;
    }
    .mobile-topbar,
    .mobile-backdrop {
      display: none;
    }
    .sidebar {
      border: 1px solid rgba(255,255,255,.82);
      background: linear-gradient(165deg, rgba(255,255,255,.86), rgba(245,248,255,.74));
      backdrop-filter: blur(14px);
      box-shadow: var(--shadow-soft);
      color: var(--text-main);
      border-radius: 24px;
      padding: .85rem;
      display: flex;
      flex-direction: column;
      gap: .7rem;
      position: sticky;
      top: 1rem;
      height: calc(100dvh - 2rem);
      overflow: hidden;
      animation: soft-rise .45s ease both;
      contain: layout paint;
      transform: translateZ(0);
      backface-visibility: hidden;
    }
    .shell.sidebar-resizing {
      transition-duration: .12s;
    }
    .shell.sidebar-resizing .sidebar,
    .shell.sidebar-resizing .content {
      backdrop-filter: none;
      -webkit-backdrop-filter: none;
    }
    .shell.sidebar-resizing .sidebar {
      box-shadow: none;
    }
    .shell.sidebar-resizing .content {
      box-shadow: none;
    }
    .shell.sidebar-resizing nav a,
    .shell.sidebar-resizing .collapse-btn,
    .shell.sidebar-resizing .collapse-btn svg,
    .shell.sidebar-resizing .nav-icon {
      transition: none !important;
    }
    .sidebar-top {
      display: flex;
      align-items: center;
      gap: .5rem;
    }
    .sidebar-top.compact-top {
      justify-content: center;
    }
    .brand {
      display: flex;
      gap: .7rem;
      align-items: center;
      padding: .56rem .62rem;
      border-radius: 14px;
      background: linear-gradient(135deg, #f2f5ff, #ebf7ff);
      border: 1px solid #dbe4fb;
      flex: 1;
      min-width: 0;
    }
    .brand-copy {
      min-width: 0;
    }
    .collapse-btn {
      width: 36px;
      height: 36px;
      border-radius: 11px;
      border: 1px solid #d6dff6;
      background: linear-gradient(135deg, #f8fbff, #ecf3ff);
      color: #4d638f;
      cursor: pointer;
      flex: 0 0 auto;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      transition: transform .18s ease, box-shadow .18s ease;
    }
    .collapse-btn svg {
      width: 18px;
      height: 18px;
      stroke: currentColor;
      stroke-width: 2.1;
      fill: none;
      stroke-linecap: round;
      stroke-linejoin: round;
      transition: transform .25s cubic-bezier(.2,.8,.2,1);
    }
    .collapse-btn:hover {
      transform: translateY(-1px);
      box-shadow: 0 10px 20px rgba(95, 117, 181, .24);
    }
    .mobile-only {
      display: none;
    }
    .logo {
      width: 42px;
      height: 42px;
      border-radius: 12px;
      object-fit: cover;
      object-position: center;
      background: #fff;
      border: 1px solid #d5e0f4;
      box-shadow: 0 8px 18px rgba(95, 116, 173, 0.16);
      flex: 0 0 auto;
    }
    h2 {
      margin: 0;
      font-size: 1.06rem;
      font-family: var(--font-heading);
      font-weight: 600;
      color: #334163;
    }
    p {
      margin: .08rem 0 0;
      font-size: .74rem;
      color: var(--text-muted);
    }
    nav {
      display: grid;
      gap: .82rem;
      flex: 1 1 auto;
      min-height: 0;
      overflow: auto;
      padding-right: .12rem;
      scrollbar-width: thin;
    }
    .nav-section {
      display: grid;
      gap: .24rem;
    }
    .nav-section-title {
      margin: .2rem .48rem;
      font-size: .7rem;
      font-weight: 700;
      letter-spacing: .08em;
      text-transform: uppercase;
      color: #7a8db3;
    }
    nav a {
      color: #4c5d7d;
      text-decoration: none;
      padding: .52rem .64rem;
      border-radius: 12px;
      transition: background .2s ease, color .2s ease, border-color .2s ease;
      font-size: .96rem;
      font-weight: 600;
      display: flex;
      align-items: center;
      gap: .58rem;
      border: 1px solid transparent;
    }
    .nav-icon {
      width: 36px;
      height: 36px;
      border-radius: 11px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(145deg, rgba(225, 234, 255, .92), rgba(235, 246, 255, .94));
      color: #415b86;
      flex: 0 0 auto;
      border: 1px solid rgba(178, 194, 232, .64);
    }
    .nav-icon svg {
      width: 20px;
      height: 20px;
      stroke: currentColor;
      stroke-width: 2.2;
      fill: none;
      stroke-linecap: round;
      stroke-linejoin: round;
    }
    .nav-text {
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    nav a:hover {
      background: rgba(36, 52, 83, .06);
      border-color: rgba(90, 112, 152, .22);
      color: #324565;
    }
    nav a.active {
      background: linear-gradient(145deg, rgba(220, 232, 255, .74), rgba(231, 246, 255, .72));
      color: #2f476f;
      border-color: rgba(156, 177, 223, .62);
    }
    nav a.active .nav-icon {
      color: #2f4d79;
      background: linear-gradient(145deg, #d4e3ff, #caf1ea);
      border-color: #afc5ec;
      box-shadow: 0 6px 12px rgba(117, 142, 194, .22);
    }
    .sidebar-footer {
      display: grid;
      gap: .45rem;
      margin-top: auto;
      padding-top: .1rem;
      flex: 0 0 auto;
    }
    .support-card {
      display: grid;
      gap: .58rem;
    }
    .support-card-head {
      display: flex;
      align-items: flex-start;
      gap: .56rem;
      min-width: 0;
    }
    .support-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: 0 0 auto;
    }
    .support-copy {
      display: grid;
      gap: .18rem;
      min-width: 0;
    }
    .support-card h3 {
      margin: 0;
    }
    .support-card p {
      margin: 0;
    }
    .support-actions {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: .42rem;
    }
    .support-btn,
    .bug-btn {
      text-align: left;
      text-decoration: none;
      cursor: pointer;
      align-content: center;
      min-height: 0;
    }
    .developed-by {
      margin: 0;
      text-align: center;
    }
    .user-box {
      margin-top: 0;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: .55rem;
      min-width: 0;
    }
    .user-profile {
      display: flex;
      align-items: center;
      gap: .46rem;
      flex: 1;
      min-width: 0;
    }
    .user-avatar {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: 0 0 auto;
    }
    .user-meta {
      min-width: 0;
    }
    .user-meta strong {
      display: block;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      line-height: 1.1;
    }
    .user-meta small {
      display: block;
      opacity: .82;
      margin-top: .1rem;
      line-height: 1.1;
    }
    .user-box strong,
    .user-box small {
      font-family: var(--font-heading);
      font-weight: 600;
      color: #3a4868;
    }
    .logout-btn {
      cursor: pointer;
      flex: 0 0 auto;
      min-width: 96px;
    }
    .sidebar.sidebar-collapsed {
      align-items: center;
      padding: .75rem .5rem;
    }
    .sidebar.sidebar-collapsed .brand,
    .sidebar.sidebar-collapsed .sidebar-footer,
    .sidebar.sidebar-collapsed .mobile-only {
      display: none;
    }
    .sidebar.sidebar-collapsed .sidebar-top {
      justify-content: center;
      width: 100%;
    }
    .sidebar.sidebar-collapsed nav {
      width: 100%;
      gap: .34rem;
    }
    .sidebar.sidebar-collapsed nav a {
      justify-content: center;
      padding: .58rem .36rem;
      transform: none;
    }
    .sidebar.sidebar-collapsed .nav-text {
      display: none;
    }
    .sidebar.sidebar-collapsed .nav-section {
      gap: .18rem;
    }
    .sidebar.sidebar-collapsed .nav-icon {
      width: 34px;
      height: 34px;
      border-radius: 11px;
    }
    .content {
      padding: 1.05rem;
      overflow-x: hidden;
      overflow-y: auto;
      min-height: 0;
      min-width: 0;
      border-radius: 24px;
      border: 1px solid rgba(255,255,255,.8);
      background: linear-gradient(165deg, rgba(255,255,255,.82), rgba(247,250,255,.72));
      backdrop-filter: blur(8px);
      box-shadow: var(--shadow-soft);
      animation: soft-rise .5s ease both;
    }
    .bug-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(33, 44, 74, .45);
      backdrop-filter: blur(2px);
      z-index: 109;
    }
    .bug-modal {
      position: fixed;
      width: min(560px, calc(100vw - 1.4rem));
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      z-index: 110;
      border-radius: 20px;
      border: 1px solid #d5e2fb;
      background: #f8fbff;
      box-shadow: 0 30px 70px rgba(52, 72, 126, .32);
      padding: .9rem;
    }
    .bug-modal h3 {
      margin: 0;
      font-size: 1.1rem;
      font-family: var(--font-heading);
      font-weight: 600;
      color: #2e426f;
    }
    .bug-modal form {
      display: grid;
      gap: .44rem;
    }
    .bug-modal label {
      font-size: .82rem;
      font-family: var(--font-heading);
      font-weight: 600;
      color: #415579;
    }
    .bug-modal input,
    .bug-modal textarea {
      width: 100%;
      border: 1px solid #cfdcf6;
      border-radius: 10px;
      padding: .58rem .65rem;
      background: #fff;
      color: #31425f;
      font-size: .88rem;
      font-family: var(--font-body);
    }
    .bug-modal input:focus,
    .bug-modal textarea:focus { outline: none; border-color: #7e8df7; }
    .field-error {
      color: #bf4a6b;
      font-size: .74rem;
    }
    .bug-actions {
      display: flex;
      justify-content: flex-end;
      gap: .55rem;
      margin-top: .28rem;
    }
    .btn-secondary,
    .btn-primary {
      border-radius: 10px;
      padding: .52rem .8rem;
      font-family: var(--font-heading);
      font-weight: 600;
      cursor: pointer;
      border: 1px solid transparent;
      transition: transform .15s ease;
    }
    .btn-secondary {
      border-color: #d7e2fa;
      background: linear-gradient(135deg, #f3f7ff, #edf4ff);
      color: #4f6088;
    }
    .btn-primary {
      background: linear-gradient(135deg, #7385f7, #67a2ef);
      color: #fff;
    }
    .btn-primary:disabled {
      opacity: .7;
      cursor: not-allowed;
      transform: none;
    }
    @keyframes mobile-sidebar-enter {
      from {
        opacity: 0;
        transform: translateY(8px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    @media (max-height: 900px) and (min-width: 981px) {
      .sidebar {
        gap: .58rem;
        padding: .75rem;
      }
      nav a {
        padding: .45rem .56rem;
        font-size: .91rem;
      }
      .nav-icon {
        width: 28px;
        height: 28px;
      }
      .sidebar-footer {
        gap: .34rem;
      }
      .support-card {
        padding: .58rem;
      }
      .support-actions {
        gap: .34rem;
      }
      .support-card h3 {
        font-size: .92rem;
      }
      .support-card p {
        font-size: .75rem;
      }
      .support-btn,
      .bug-btn {
        padding: .4rem .56rem;
      }
      .user-box {
        padding-top: .56rem;
      }
      .brand p {
        display: none;
      }
    }

    @media (max-width: 980px) {
      .shell {
        display: block;
        height: auto;
        min-height: 100dvh;
        padding: .75rem;
        overflow: visible;
      }
      .mobile-topbar {
        display: flex;
        position: sticky;
        top: 0;
        z-index: 80;
        align-items: center;
        justify-content: space-between;
        gap: .65rem;
        padding: .62rem .72rem;
        border: 1px solid rgba(255,255,255,.84);
        border-radius: 18px;
        background:
          radial-gradient(circle at 10% 20%, rgba(139, 159, 255, .16), transparent 32%),
          linear-gradient(150deg, rgba(255,255,255,.96), rgba(241,247,255,.9));
        box-shadow: 0 18px 30px rgba(73, 94, 148, .12);
      }
      .icon-btn {
        width: 40px;
        height: 40px;
        border: 1px solid #d6e0f7;
        border-radius: 10px;
        background: linear-gradient(145deg, #f4f6ff, #ecf5ff);
        display: inline-flex;
        align-items: center;
        justify-content: center;
        flex-direction: column;
        gap: 4px;
        cursor: pointer;
        transition: transform .22s ease, box-shadow .22s ease, background .22s ease;
      }
      .icon-btn span {
        width: 17px;
        height: 2px;
        border-radius: 999px;
        background: #5f6e95;
        transition: transform .24s ease, opacity .2s ease;
        transform-origin: center;
      }
      .icon-btn.active span:nth-child(1) {
        transform: translateY(6px) rotate(45deg);
      }
      .icon-btn.active span:nth-child(2) {
        opacity: 0;
      }
      .icon-btn.active span:nth-child(3) {
        transform: translateY(-6px) rotate(-45deg);
      }
      .mobile-brand {
        display: flex;
        align-items: center;
        gap: .55rem;
        color: #33405f;
        min-width: 0;
      }
      .mobile-brand .logo {
        width: 36px;
        height: 36px;
        border-radius: 11px;
      }
      .mobile-brand strong {
        font-family: var(--font-heading);
        font-size: 1rem;
        font-weight: 600;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .logout-mini {
        border: 1px solid #d9e1fa;
        border-radius: 9px;
        background: linear-gradient(135deg, #f1f4ff, #e8f2ff);
        color: #4a5a81;
        padding: .42rem .58rem;
        font-size: .82rem;
        font-family: var(--font-heading);
        font-weight: 600;
      }
      .mobile-backdrop {
        display: block;
        position: fixed;
        inset: 0;
        z-index: 70;
        background: rgba(30, 41, 68, .38);
        opacity: 0;
        visibility: hidden;
        pointer-events: none;
        transition: opacity .18s ease, visibility 0s linear .18s;
      }
      .mobile-backdrop.visible {
        opacity: 1;
        visibility: visible;
        pointer-events: auto;
        transition-delay: 0s;
      }
      .sidebar {
        position: fixed;
        inset: 0 auto 0 0;
        width: min(86vw, 340px);
        height: 100dvh;
        top: 0;
        z-index: 90;
        border: 1px solid rgba(208, 220, 246, .9);
        box-shadow: 0 24px 48px rgba(69, 92, 150, .32);
        background:
          radial-gradient(circle at 12% 10%, rgba(143, 161, 255, .18), transparent 34%),
          radial-gradient(circle at 88% 90%, rgba(108, 220, 206, .16), transparent 30%),
          linear-gradient(168deg, rgba(255,255,255,.98), rgba(239,245,255,.96));
        overflow: hidden;
        border-radius: 0 20px 20px 0;
        gap: .68rem;
        padding: .78rem;
        animation: none;
        backdrop-filter: none;
        -webkit-backdrop-filter: none;
        opacity: 0;
        visibility: hidden;
        pointer-events: none;
        transform: translate3d(-104%, 0, 0);
        will-change: transform, opacity;
        backface-visibility: hidden;
        contain: layout paint;
        transition:
          transform .2s cubic-bezier(.25, .8, .25, 1),
          opacity .16s ease,
          visibility 0s linear .2s;
      }
      .desktop-only {
        display: none;
      }
      .mobile-only {
        display: inline-flex;
      }
      .sidebar.mobile-open {
        transform: translate3d(0, 0, 0);
        opacity: 1;
        visibility: visible;
        pointer-events: auto;
        transition-delay: 0s;
      }
      .sidebar.mobile-open nav {
        animation: mobile-sidebar-enter .2s ease both;
      }
      .sidebar.mobile-open .sidebar-footer {
        animation: mobile-sidebar-enter .24s ease both;
      }
      nav {
        grid-template-columns: 1fr;
      }
      nav a {
        padding: .47rem .58rem;
      }
      .sidebar-footer {
        gap: .38rem;
      }
      .support-card {
        padding: .58rem;
        gap: .3rem;
      }
      .support-actions {
        gap: .34rem;
      }
      .support-card h3 {
        font-size: .9rem;
      }
      .support-card p {
        font-size: .75rem;
      }
      .support-btn,
      .bug-btn {
        padding: .4rem .56rem;
        font-size: .74rem;
      }
      .developed-by {
        font-size: .72rem;
      }
      .content {
        margin-top: .75rem;
        border-radius: 20px;
        padding: .88rem .72rem 1rem;
      }
      .user-box {
        gap: .5rem;
      }
      .logout-btn {
        padding: .5rem .72rem;
      }
      .bug-modal {
        width: min(94vw, 560px);
        padding: .92rem;
      }
    }

    @media (max-width: 520px) {
      .shell {
        padding: .5rem;
      }
      .mobile-topbar {
        padding: .56rem .58rem;
      }
      .mobile-brand strong {
        font-size: .92rem;
      }
      .logout-mini {
        padding: .38rem .5rem;
        font-size: .76rem;
      }
      .sidebar {
        width: min(88vw, 320px);
      }
      .support-actions {
        grid-template-columns: 1fr;
      }
      .user-box {
        flex-direction: column;
        align-items: stretch;
      }
      .logout-btn {
        width: 100%;
      }
      .content {
        padding: .78rem .6rem .9rem;
        border-radius: 16px;
      }
      .bug-actions {
        flex-direction: column-reverse;
      }
      .btn-primary,
      .btn-secondary {
        width: 100%;
      }
    }
  `
})
export class AppShellComponent implements OnInit, OnDestroy {
  private static readonly maxBugAttachmentSizeBytes = 4 * 1024 * 1024;
  private static readonly sidebarResizeAnimationDurationMs = 140;
  private static readonly allowedBugAttachmentMimeTypes = new Set([
    'image/png',
    'image/jpeg',
    'image/webp',
    'image/gif'
  ]);
  private static readonly allowedBugAttachmentExtensions = new Set(['.png', '.jpg', '.jpeg', '.webp', '.gif']);

  readonly authService = inject(AuthService);
  private readonly portalApiService = inject(PortalApiService);
  private readonly toastService = inject(ToastService);
  private readonly inactivityService = inject(InactivityService);
  private readonly formBuilder = inject(FormBuilder);
  readonly mobileMenuOpen = signal(false);
  readonly sidebarCollapsed = signal(false);
  readonly sidebarResizeAnimating = signal(false);
  readonly isMobileView = signal(false);
  readonly bugDialogOpen = signal(false);
  readonly bugSubmitPending = signal(false);
  readonly bugAttachmentDragActive = signal(false);
  readonly bugAttachmentFile = signal<File | null>(null);
  readonly bugAttachmentPreviewUrl = signal<string | null>(null);
  readonly bugAttachmentLimitLabel = `${AppShellComponent.maxBugAttachmentSizeBytes / (1024 * 1024)} MB`;
  readonly supportPhone = '+9607515618';
  readonly supportPhoneLink = 'tel:+9607515618';
  private readonly sidebarStorageKey = 'mydhathuru-sidebar-collapsed';
  private readonly usernameStorageKey = 'mydhathuru-username';
  private sidebarResizeTimeout: ReturnType<typeof setTimeout> | null = null;
  private bugAttachmentObjectUrl: string | null = null;
  readonly preferredUsername = signal<string>('');
  readonly isDesktopCollapsed = computed(() => this.sidebarCollapsed() && !this.isMobileView());

  @ViewChild('bugImageInput')
  private bugImageInput?: ElementRef<HTMLInputElement>;

  readonly bugForm = this.formBuilder.nonNullable.group({
    subject: ['', [Validators.required, Validators.maxLength(160)]],
    description: ['', [Validators.required, Validators.maxLength(3000)]]
  });

  readonly navSections: NavSection[] = [
    {
      label: 'Dashboard',
      items: [
        {
          path: '/app/dashboard',
          label: 'Dashboard',
          iconPaths: ['M3 10.2L12 3l9 7.2V20a1 1 0 0 1-1 1h-5.8v-6.4H9.8V21H4a1 1 0 0 1-1-1z']
        }
      ]
    },
    {
      label: 'Sales & Receivables',
      items: [
        {
          path: '/app/delivery-notes',
          label: 'Delivery Notes',
          iconPaths: ['M7 3h8l5 5v13H7z', 'M15 3v5h5', 'M10 13h7', 'M10 17h7']
        },
        {
          path: '/app/customers',
          label: 'Customers',
          iconPaths: ['M16 21v-1.7a3.6 3.6 0 0 0-3.6-3.6H7.6A3.6 3.6 0 0 0 4 19.3V21', 'M10 12a3.6 3.6 0 1 0 0-7.2 3.6 3.6 0 0 0 0 7.2', 'M18 8v5', 'M20.5 10.5H15.5']
        },
        {
          path: '/app/quote',
          label: 'Quotations',
          iconPaths: ['M7 3h8l5 5v13H7z', 'M15 3v5h5', 'M9 13h6', 'M9 17h5']
        },
        {
          path: '/app/sales-history',
          label: 'Invoices Issued',
          iconPaths: ['M4 20h16', 'M6 15l3-3 3 2 5-6', 'M16 8h4v4']
        },
        {
          path: '/app/account-statements',
          label: 'Customer Statements',
          iconPaths: ['M5 4h14a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1z', 'M8 8h8', 'M8 12h8', 'M8 16h5']
        }
      ]
    },
    {
      label: 'Purchases & Expenses',
      items: [
        {
          path: '/app/po',
          label: 'Purchase Orders',
          iconPaths: ['M6 3h9l5 5v13H6z', 'M15 3v5h5', 'M9 13h7', 'M9 17h6']
        },
        {
          path: '/app/suppliers',
          label: 'Suppliers',
          iconPaths: ['M4 7h16v13H4z', 'M8 7V4h8v3', 'M8 12h8', 'M8 16h5']
        },
        {
          path: '/app/received-invoices',
          label: 'Received Invoices',
          iconPaths: ['M7 3h8l5 5v13H7z', 'M15 3v5h5', 'M9 13h7', 'M9 17h7']
        },
        {
          path: '/app/payment-vouchers',
          label: 'Payment Vouchers',
          iconPaths: ['M4 6h16v12H4z', 'M7 10h10', 'M7 14h7']
        },
        {
          path: '/app/rent',
          label: 'Rent',
          iconPaths: ['M4 11l8-6 8 6', 'M6 10.5V20h12v-9.5', 'M10 20v-5h4v5']
        },
        {
          path: '/app/bpt',
          label: 'BPT',
          iconPaths: ['M5 20h14', 'M7 16l3-4 3 2 4-6', 'M16 6h3v3', 'M5 4h14v16H5z']
        },
        {
          path: '/app/mira',
          label: 'MIRA',
          iconPaths: ['M5 4h14v16H5z', 'M8 8h8', 'M8 12h8', 'M8 16h5', 'M15 4v4h4']
        },
        {
          path: '/app/expense-ledger',
          label: 'Expense Ledger',
          iconPaths: ['M5 20h14', 'M7 20V8', 'M12 20V4', 'M17 20V12']
        },
        {
          path: '/app/expense-categories',
          label: 'Expense Categories',
          iconPaths: ['M4 7h7v5H4z', 'M13 7h7v5h-7z', 'M4 14h7v5H4z', 'M13 14h7v5h-7z']
        }
      ]
    },
    {
      label: 'Payroll & HR',
      items: [
        {
          path: '/app/payroll',
          label: 'Payroll & Staff',
          iconPaths: ['M3 7h18v12H3z', 'M3 11h18', 'M15 15h4']
        },
        {
          path: '/app/staff-conduct',
          label: 'Disciplinary & Warning',
          iconPaths: ['M6 4h12v16H6z', 'M9 8h6', 'M9 12h6', 'M9 16h4', 'M5 5l2-2', 'M19 5l-2-2']
        }
      ]
    },
    {
      label: 'Reports',
      items: [
        {
          path: '/app/reports',
          label: 'Reports',
          iconPaths: ['M4 20h16', 'M7 20V12', 'M12 20V8', 'M17 20V5']
        }
      ]
    },
    {
      label: 'Settings',
      items: [
        {
          path: '/app/settings',
          label: 'Settings',
          iconPaths: ['M12 3v2', 'M12 19v2', 'M4.9 4.9l1.4 1.4', 'M17.7 17.7l1.4 1.4', 'M3 12h2', 'M19 12h2', 'M4.9 19.1l1.4-1.4', 'M17.7 6.3l1.4-1.4', 'M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7']
        }
      ]
    }
  ];

  ngOnInit(): void {
    this.hydrateSidebarPreference();
    this.hydrateUsernamePreference();
    this.syncViewportState();
    this.syncUsernameFromSettings();
    this.inactivityService.start();
  }

  ngOnDestroy(): void {
    this.inactivityService.stop();
    this.clearSidebarResizeTimeout();
    this.setBodyScrollLock(false);
    this.clearBugAttachment();
  }

  logout(): void {
    this.closeMobileMenu();
    this.authService.logout();
  }

  openMobileMenu(): void {
    if (!this.isMobileView()) {
      return;
    }
    this.mobileMenuOpen.set(true);
    this.setBodyScrollLock(true);
  }

  onMobileMenuButtonClick(event: Event): void {
    event.preventDefault();
    event.stopPropagation();

    if (this.mobileMenuOpen()) {
      this.closeMobileMenu();
      return;
    }

    this.openMobileMenu();
  }

  toggleSidebarCollapse(): void {
    if (this.isMobileView()) {
      return;
    }

    this.startSidebarResizeAnimation();

    this.sidebarCollapsed.update((value) => {
      const next = !value;
      if (typeof window !== 'undefined') {
        window.localStorage.setItem(this.sidebarStorageKey, next ? '1' : '0');
      }
      return next;
    });
  }

  closeMobileMenu(): void {
    if (!this.isMobileView()) {
      this.mobileMenuOpen.set(false);
      this.setBodyScrollLock(false);
      return;
    }

    if (!this.mobileMenuOpen()) {
      return;
    }

    this.mobileMenuOpen.set(false);
    this.setBodyScrollLock(false);
  }

  onMobileCloseClick(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.closeMobileMenu();
  }

  onMobileBackdropClick(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.closeMobileMenu();
  }

  @HostListener('window:resize')
  onViewportResize(): void {
    this.syncViewportState();
  }

  @HostListener('document:keydown.escape')
  onEscapePressed(): void {
    if (this.mobileMenuOpen()) {
      this.closeMobileMenu();
    }
  }

  @HostListener('window:mydhathuru-username-updated', ['$event'])
  onUsernameUpdated(event: Event): void {
    const detail = (event as CustomEvent<string>).detail;
    const value = typeof detail === 'string' ? detail.trim() : '';
    this.preferredUsername.set(value);
  }

  openBugDialog(): void {
    this.bugForm.reset({
      subject: '',
      description: ''
    });
    this.clearBugAttachment(true);
    this.bugAttachmentDragActive.set(false);
    this.bugDialogOpen.set(true);
  }

  closeBugDialog(): void {
    if (this.bugSubmitPending()) {
      return;
    }

    this.bugDialogOpen.set(false);
    this.bugAttachmentDragActive.set(false);
  }

  bugAttachmentSizeLabel(): string {
    const file = this.bugAttachmentFile();
    if (!file || file.size <= 0) {
      return '';
    }

    const sizeInMb = file.size / (1024 * 1024);
    return `${sizeInMb.toFixed(2)} MB`;
  }

  openBugAttachmentPicker(): void {
    if (this.bugSubmitPending()) {
      return;
    }

    this.bugImageInput?.nativeElement.click();
  }

  onBugAttachmentPicked(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.item(0);
    if (!file) {
      return;
    }

    this.setBugAttachment(file);
  }

  onBugAttachmentDragOver(event: DragEvent): void {
    event.preventDefault();
    if (this.bugSubmitPending()) {
      return;
    }

    this.bugAttachmentDragActive.set(true);
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'copy';
    }
  }

  onBugAttachmentDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.bugAttachmentDragActive.set(false);
  }

  onBugAttachmentDrop(event: DragEvent): void {
    event.preventDefault();
    this.bugAttachmentDragActive.set(false);
    if (this.bugSubmitPending()) {
      return;
    }

    const file = event.dataTransfer?.files?.item(0);
    if (!file) {
      this.toastService.error('Drop an image file to attach.');
      return;
    }

    this.setBugAttachment(file);
  }

  removeBugAttachment(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (this.bugSubmitPending()) {
      return;
    }

    this.clearBugAttachment(true);
  }

  submitBugReport(): void {
    if (this.bugForm.invalid || this.bugSubmitPending()) {
      this.bugForm.markAllAsTouched();
      return;
    }

    this.bugSubmitPending.set(true);
    const pageUrl = typeof window !== 'undefined' ? window.location.href : undefined;
    const { subject, description } = this.bugForm.getRawValue();

    this.portalApiService
      .reportBug({
        subject: subject.trim(),
        description: description.trim(),
        pageUrl
      }, this.bugAttachmentFile())
      .pipe(finalize(() => this.bugSubmitPending.set(false)))
      .subscribe({
        next: () => {
          this.clearBugAttachment(true);
          this.bugDialogOpen.set(false);
          this.toastService.success('Bug report sent successfully.');
        },
        error: (error) => {
          this.toastService.error(extractApiError(error, 'Unable to send bug report.'));
        }
      });
  }

  private setBugAttachment(file: File): void {
    if (file.size === 0) {
      this.toastService.error('Selected image file is empty.');
      this.resetBugAttachmentInput();
      return;
    }

    if (file.size > AppShellComponent.maxBugAttachmentSizeBytes) {
      this.toastService.error(`Image file must be ${this.bugAttachmentLimitLabel} or smaller.`);
      this.resetBugAttachmentInput();
      return;
    }

    if (!this.isAllowedBugAttachmentType(file)) {
      this.toastService.error('Supported image formats are PNG, JPG, WEBP, and GIF.');
      this.resetBugAttachmentInput();
      return;
    }

    this.clearBugAttachment();
    this.bugAttachmentFile.set(file);

    if (typeof URL !== 'undefined' && typeof URL.createObjectURL === 'function') {
      this.bugAttachmentObjectUrl = URL.createObjectURL(file);
      this.bugAttachmentPreviewUrl.set(this.bugAttachmentObjectUrl);
    }
  }

  private isAllowedBugAttachmentType(file: File): boolean {
    const mimeType = file.type.toLowerCase().trim();
    if (mimeType) {
      return AppShellComponent.allowedBugAttachmentMimeTypes.has(mimeType);
    }

    const extension = this.getFileExtension(file.name);
    return !!extension && AppShellComponent.allowedBugAttachmentExtensions.has(extension);
  }

  private getFileExtension(fileName: string): string | null {
    const lastDotIndex = fileName.lastIndexOf('.');
    if (lastDotIndex <= 0 || lastDotIndex === fileName.length - 1) {
      return null;
    }

    return fileName.slice(lastDotIndex).toLowerCase();
  }

  private clearBugAttachment(resetInput = false): void {
    this.bugAttachmentFile.set(null);
    this.bugAttachmentPreviewUrl.set(null);

    if (typeof URL !== 'undefined' && this.bugAttachmentObjectUrl && typeof URL.revokeObjectURL === 'function') {
      URL.revokeObjectURL(this.bugAttachmentObjectUrl);
    }
    this.bugAttachmentObjectUrl = null;

    if (resetInput) {
      this.resetBugAttachmentInput();
    }
  }

  private resetBugAttachmentInput(): void {
    if (this.bugImageInput) {
      this.bugImageInput.nativeElement.value = '';
    }
  }

  userInitials(): string {
    const profileName = this.profileDisplayName();
    if (!profileName) {
      return 'MD';
    }

    const parts = profileName.split(/\s+/).filter(Boolean);
    if (parts.length === 1) {
      return parts[0].slice(0, 2).toUpperCase();
    }

    return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  }

  profileDisplayName(): string {
    const preferred = this.preferredUsername();
    if (preferred) {
      return preferred;
    }

    return this.authService.user()?.fullName ?? 'myDhathuru User';
  }

  private hydrateSidebarPreference(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const value = window.localStorage.getItem(this.sidebarStorageKey);
    this.sidebarCollapsed.set(value === '1');
  }

  private hydrateUsernamePreference(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const value = window.localStorage.getItem(this.usernameStorageKey);
    this.preferredUsername.set(value?.trim() ?? '');
  }

  private syncUsernameFromSettings(): void {
    this.portalApiService.getSettings().subscribe({
      next: (settings) => {
        const username = settings.username?.trim() ?? '';
        this.preferredUsername.set(username);

        if (typeof window !== 'undefined') {
          if (username) {
            window.localStorage.setItem(this.usernameStorageKey, username);
          } else {
            window.localStorage.removeItem(this.usernameStorageKey);
          }
        }
      },
      error: () => {
        // Keep existing display name fallback when settings cannot be fetched.
      }
    });
  }

  private syncViewportState(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const mobile = window.innerWidth <= 980;
    this.isMobileView.set(mobile);
    if (mobile) {
      this.sidebarCollapsed.set(false);
      return;
    }

    this.closeMobileMenu();
  }

  private setBodyScrollLock(locked: boolean): void {
    if (typeof document === 'undefined') {
      return;
    }

    document.body.style.overflow = locked ? 'hidden' : '';
  }

  private startSidebarResizeAnimation(): void {
    this.clearSidebarResizeTimeout();
    this.sidebarResizeAnimating.set(true);
    this.sidebarResizeTimeout = setTimeout(() => {
      this.sidebarResizeAnimating.set(false);
      this.sidebarResizeTimeout = null;
    }, AppShellComponent.sidebarResizeAnimationDurationMs);
  }

  private clearSidebarResizeTimeout(): void {
    if (!this.sidebarResizeTimeout) {
      this.sidebarResizeAnimating.set(false);
      return;
    }

    clearTimeout(this.sidebarResizeTimeout);
    this.sidebarResizeTimeout = null;
    this.sidebarResizeAnimating.set(false);
  }
}

