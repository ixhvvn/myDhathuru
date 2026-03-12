import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { PortalAdminAuditLog, PortalAdminDashboard } from '../../../core/models/app.models';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';

@Component({
  selector: 'app-portal-admin-dashboard-page',
  standalone: true,
  imports: [CommonModule, DatePipe, AppCardComponent, AppLoaderComponent, AppEmptyStateComponent],
  template: `
    <section class="page-head">
      <h1>Portal Admin Dashboard</h1>
      <p>Cross-tenant platform health, approvals, and account control metrics.</p>
    </section>

    <app-loader *ngIf="loading()"></app-loader>

    <ng-container *ngIf="!loading() && data() as dashboard">
      <section class="kpi-grid">
        <app-card class="kpi"><h3>Total Businesses</h3><strong>{{ dashboard.totalBusinesses }}</strong></app-card>
        <app-card class="kpi warning"><h3>Pending Requests</h3><strong>{{ dashboard.pendingSignupRequests }}</strong></app-card>
        <app-card class="kpi success"><h3>Active Businesses</h3><strong>{{ dashboard.activeBusinesses }}</strong></app-card>
        <app-card class="kpi danger"><h3>Disabled Businesses</h3><strong>{{ dashboard.disabledBusinesses }}</strong></app-card>
        <app-card class="kpi"><h3>Total Staff</h3><strong>{{ dashboard.totalStaffAcrossBusinesses }}</strong></app-card>
        <app-card class="kpi"><h3>Total Vessels</h3><strong>{{ dashboard.totalVesselsAcrossBusinesses }}</strong></app-card>
      </section>

      <section class="grid-two">
        <app-card>
          <div class="section-head">
            <h2>Recent Signup Requests</h2>
          </div>
          <app-empty-state
            *ngIf="dashboard.recentSignupRequests.length === 0"
            title="No signup requests yet"
            description="New business signup requests will appear here.">
          </app-empty-state>

          <div class="table-wrap" *ngIf="dashboard.recentSignupRequests.length > 0">
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Company</th>
                  <th>Requested By</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let request of dashboard.recentSignupRequests">
                  <td>{{ request.requestDate | date:'yyyy-MM-dd HH:mm' }}</td>
                  <td>{{ request.companyName }}</td>
                  <td>{{ request.requestedByName }}</td>
                  <td><span class="status" [attr.data-status]="request.status">{{ request.status }}</span></td>
                </tr>
              </tbody>
            </table>
          </div>
        </app-card>

        <app-card>
          <div class="section-head">
            <h2>Latest Admin Actions</h2>
          </div>
          <app-empty-state
            *ngIf="latestActions().length === 0"
            title="No actions logged yet"
            description="Sensitive portal admin actions will be listed here.">
          </app-empty-state>

          <ul class="audit-list" *ngIf="latestActions().length > 0">
            <li *ngFor="let action of latestActions()">
              <div>
                <strong>{{ action.actionType }}</strong>
                <p>{{ action.targetName || action.targetType }} · {{ action.performedByName || 'System' }}</p>
              </div>
              <span>{{ action.performedAt | date:'yyyy-MM-dd HH:mm' }}</span>
            </li>
          </ul>
        </app-card>
      </section>
    </ng-container>
  `,
  styles: `
    .page-head h1 {
      margin: 0;
      font-family: var(--font-heading);
      font-size: 1.55rem;
      color: #2f4269;
      font-weight: 600;
    }
    .page-head p {
      margin: .3rem 0 0;
      color: #61739a;
    }
    .kpi-grid {
      margin-top: .9rem;
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: .72rem;
    }
    .kpi {
      --card-padding: .86rem .92rem;
      display: grid;
      gap: .26rem;
      background: linear-gradient(155deg, rgba(244,247,255,.95), rgba(231,239,255,.88));
    }
    .kpi.warning { background: linear-gradient(155deg, rgba(255,244,229,.9), rgba(255,236,204,.86)); }
    .kpi.success { background: linear-gradient(155deg, rgba(229,255,245,.88), rgba(209,248,231,.86)); }
    .kpi.danger { background: linear-gradient(155deg, rgba(255,236,242,.9), rgba(255,220,230,.86)); }
    .kpi h3 {
      margin: 0;
      color: #5970a0;
      font-size: .82rem;
      text-transform: uppercase;
      letter-spacing: .04em;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .kpi strong {
      color: #263a63;
      font-size: 1.65rem;
      line-height: 1.1;
      font-family: var(--font-heading);
      font-weight: 600;
    }
    .grid-two {
      margin-top: .85rem;
      display: grid;
      grid-template-columns: 1.1fr .9fr;
      gap: .72rem;
    }
    .section-head h2 {
      margin: 0 0 .6rem;
      color: #30466f;
      font-family: var(--font-heading);
      font-size: 1.08rem;
      font-weight: 600;
    }
    .table-wrap {
      border: 1px solid #d9e3fa;
      border-radius: 14px;
      overflow: hidden;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: .85rem;
    }
    th,
    td {
      padding: .52rem .62rem;
      text-align: left;
      border-bottom: 1px solid #e1e8fb;
      color: #41567f;
    }
    th {
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .76rem;
      text-transform: uppercase;
      color: #5d73a1;
      background: #f4f8ff;
      letter-spacing: .04em;
    }
    tbody tr:last-child td {
      border-bottom: none;
    }
    .status {
      display: inline-flex;
      padding: .2rem .5rem;
      border-radius: 999px;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .74rem;
      border: 1px solid transparent;
    }
    .status[data-status='Pending'] {
      color: #a17017;
      border-color: rgba(228, 185, 105, .5);
      background: rgba(255, 230, 180, .46);
    }
    .status[data-status='Accepted'] {
      color: #2d976e;
      border-color: rgba(123, 214, 179, .52);
      background: rgba(199, 243, 223, .72);
    }
    .status[data-status='Rejected'] {
      color: #ba4d6d;
      border-color: rgba(230, 145, 171, .52);
      background: rgba(255, 215, 227, .72);
    }
    .audit-list {
      margin: 0;
      padding: 0;
      list-style: none;
      display: grid;
      gap: .46rem;
    }
    .audit-list li {
      border: 1px solid #dce5fa;
      border-radius: 12px;
      background: linear-gradient(145deg, #f8fbff, #eff4ff);
      padding: .52rem .6rem;
      display: flex;
      justify-content: space-between;
      gap: .7rem;
    }
    .audit-list strong {
      display: block;
      color: #32486f;
      font-family: var(--font-heading);
      font-weight: 600;
      font-size: .85rem;
    }
    .audit-list p {
      margin: .15rem 0 0;
      color: #60739c;
      font-size: .78rem;
      line-height: 1.35;
    }
    .audit-list span {
      white-space: nowrap;
      color: #6579a4;
      font-size: .74rem;
      align-self: start;
    }
    @media (max-width: 1450px) {
      .kpi-grid {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .grid-two {
        grid-template-columns: 1fr;
      }
    }
    @media (max-width: 700px) {
      .kpi-grid {
        grid-template-columns: 1fr;
      }
    }
  `
})
export class PortalAdminDashboardPageComponent {
  private readonly api = inject(PortalAdminApiService);
  readonly loading = signal(true);
  readonly data = signal<PortalAdminDashboard | null>(null);
  readonly latestActions = computed<PortalAdminAuditLog[]>(() => this.data()?.recentActions.slice(0, 5) ?? []);

  constructor() {
    this.api.getDashboard().subscribe({
      next: (result) => {
        this.data.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }
}

