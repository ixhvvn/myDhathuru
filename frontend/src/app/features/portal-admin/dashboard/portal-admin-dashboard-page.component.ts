import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { PortalAdminAuditLog, PortalAdminDashboard, SignupRequestStatus } from '../../../core/models/app.models';
import { PortalAdminApiService } from '../../services/portal-admin-api.service';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppLoaderComponent } from '../../../shared/components/app-loader/app-loader.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';

type AdminTone = 'indigo' | 'amber' | 'emerald' | 'rose' | 'sky' | 'violet';

interface StatusRing {
  label: string;
  percent: number;
  primary: string;
  secondary: string;
  tone: AdminTone;
}

interface SummaryMetric {
  label: string;
  value: string;
  meta: string;
  tone: AdminTone;
}

interface MixItem {
  label: string;
  count: number;
  percent: number;
  meta: string;
  tone: AdminTone;
}

interface ScaleBar {
  label: string;
  value: string;
  percent: number;
  meta: string;
  tone: AdminTone;
}

@Component({
  selector: 'app-portal-admin-dashboard-page',
  standalone: true,
  imports: [CommonModule, DatePipe, AppCardComponent, AppLoaderComponent, AppEmptyStateComponent],
  templateUrl: './portal-admin-dashboard-page.component.html',
  styleUrl: './portal-admin-dashboard-page.component.scss'
})
export class PortalAdminDashboardPageComponent {
  private readonly api = inject(PortalAdminApiService);

  readonly loading = signal(true);
  readonly data = signal<PortalAdminDashboard | null>(null);
  readonly latestActions = computed<PortalAdminAuditLog[]>(() => this.data()?.recentActions.slice(0, 3) ?? []);
  readonly toneBackgrounds: Record<AdminTone, string> = {
    indigo: 'linear-gradient(150deg, rgba(239,244,255,.98), rgba(228,234,255,.92))',
    amber: 'linear-gradient(150deg, rgba(255,248,234,.98), rgba(255,238,206,.92))',
    emerald: 'linear-gradient(150deg, rgba(238,251,246,.98), rgba(218,241,232,.92))',
    rose: 'linear-gradient(150deg, rgba(255,241,246,.98), rgba(255,229,239,.92))',
    sky: 'linear-gradient(150deg, rgba(238,247,255,.98), rgba(220,238,255,.92))',
    violet: 'linear-gradient(150deg, rgba(244,241,255,.98), rgba(232,226,255,.92))'
  };
  readonly toneColors: Record<AdminTone, string> = {
    indigo: '#6f7ff5',
    amber: '#e7a446',
    emerald: '#54ba87',
    rose: '#e986a6',
    sky: '#58b4de',
    violet: '#8d79ef'
  };

  readonly statusRings = computed<StatusRing[]>(() => {
    const dashboard = this.data();
    if (!dashboard) {
      return [];
    }

    const totalPipeline = dashboard.totalBusinesses + dashboard.pendingSignupRequests;
    const activationPercent = this.formatPercent(dashboard.activeBusinesses, dashboard.totalBusinesses);
    const approvalPercent = totalPipeline > 0
      ? Math.round((dashboard.totalBusinesses / totalPipeline) * 100)
      : 100;

    return [
      {
        label: 'Access health',
        percent: activationPercent,
        primary: `${dashboard.activeBusinesses} active`,
        secondary: `${dashboard.disabledBusinesses} disabled`,
        tone: 'emerald'
      },
      {
        label: 'Approval flow',
        percent: approvalPercent,
        primary: dashboard.pendingSignupRequests === 0 ? 'Queue clear' : `${dashboard.pendingSignupRequests} pending`,
        secondary: `${dashboard.totalBusinesses} approved tenants`,
        tone: 'amber'
      }
    ];
  });

  readonly summaryMetrics = computed<SummaryMetric[]>(() => {
    const dashboard = this.data();
    if (!dashboard) {
      return [];
    }

    return [
      {
        label: 'Businesses',
        value: dashboard.totalBusinesses.toString(),
        meta: 'Tenant accounts visible on platform.',
        tone: 'indigo'
      },
      {
        label: 'Staff footprint',
        value: dashboard.totalStaffAcrossBusinesses.toString(),
        meta: `${this.formatAverage(dashboard.totalStaffAcrossBusinesses, dashboard.totalBusinesses)} average staff per business.`,
        tone: 'sky'
      },
      {
        label: 'Fleet footprint',
        value: dashboard.totalVesselsAcrossBusinesses.toString(),
        meta: `${this.formatAverage(dashboard.totalVesselsAcrossBusinesses, dashboard.totalBusinesses)} average vessels per business.`,
        tone: 'violet'
      },
      {
        label: 'Audit flow',
        value: this.latestActions().length.toString(),
        meta: 'Recent control events visible in the live stream.',
        tone: 'amber'
      }
    ];
  });

  readonly actionMix = computed<MixItem[]>(() => {
    const actions = this.latestActions();
    if (!actions.length) {
      return [];
    }

    const counts = new Map<string, number>();
    for (const action of actions) {
      const label = this.formatActionType(action.actionType);
      counts.set(label, (counts.get(label) ?? 0) + 1);
    }

    const tones: AdminTone[] = ['indigo', 'sky', 'emerald', 'violet'];
    const total = actions.length;

    return [...counts.entries()]
      .sort((a, b) => b[1] - a[1])
      .slice(0, 4)
      .map(([label, count], index) => ({
        label,
        count,
        percent: Math.max(16, Math.round((count / total) * 100)),
        meta: `${count} of ${total} recent admin actions.`,
        tone: tones[index % tones.length]
      }));
  });

  readonly requestMix = computed<MixItem[]>(() => {
    const requests = this.data()?.recentSignupRequests ?? [];
    if (!requests.length) {
      return [];
    }

    const statuses: SignupRequestStatus[] = ['Pending', 'Accepted', 'Rejected'];
    const total = requests.length;

    return statuses.map((status) => {
      const count = requests.filter((request) => request.status === status).length;
      return {
        label: status,
        count,
        percent: count > 0 ? Math.max(16, Math.round((count / total) * 100)) : 0,
        meta: `${count} of ${total} recent requests.`,
        tone: this.statusTone(status)
      };
    });
  });

  readonly scaleBars = computed<ScaleBar[]>(() => {
    const dashboard = this.data();
    if (!dashboard) {
      return [];
    }

    const averageStaff = Number(this.formatAverage(dashboard.totalStaffAcrossBusinesses, dashboard.totalBusinesses));
    const averageVessels = Number(this.formatAverage(dashboard.totalVesselsAcrossBusinesses, dashboard.totalBusinesses));
    const rawBars = [
      {
        label: 'Business accounts',
        rawValue: dashboard.totalBusinesses,
        value: dashboard.totalBusinesses.toString(),
        meta: 'Registered tenant businesses on platform.',
        tone: 'indigo' as AdminTone
      },
      {
        label: 'Active access',
        rawValue: dashboard.activeBusinesses,
        value: dashboard.activeBusinesses.toString(),
        meta: `${this.formatPercent(dashboard.activeBusinesses, dashboard.totalBusinesses)}% of businesses currently enabled.`,
        tone: 'emerald' as AdminTone
      },
      {
        label: 'Staff footprint',
        rawValue: dashboard.totalStaffAcrossBusinesses,
        value: dashboard.totalStaffAcrossBusinesses.toString(),
        meta: `${averageStaff.toFixed(1)} staff average per business.`,
        tone: 'sky' as AdminTone
      },
      {
        label: 'Fleet footprint',
        rawValue: dashboard.totalVesselsAcrossBusinesses,
        value: dashboard.totalVesselsAcrossBusinesses.toString(),
        meta: `${averageVessels.toFixed(1)} vessels average per business.`,
        tone: 'violet' as AdminTone
      }
    ];

    const max = Math.max(...rawBars.map((item) => item.rawValue), 1);

    return rawBars.map((item) => ({
      label: item.label,
      value: item.value,
      meta: item.meta,
      percent: Math.max(14, Math.round((item.rawValue / max) * 100)),
      tone: item.tone
    }));
  });

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

  formatActionType(value: string): string {
    return value
      .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
      .replace(/[_-]+/g, ' ')
      .trim();
  }

  toneBackground(tone: AdminTone): string {
    return this.toneBackgrounds[tone];
  }

  toneColor(tone: AdminTone): string {
    return this.toneColors[tone];
  }

  private formatPercent(value: number, total: number): number {
    if (total <= 0) {
      return 0;
    }

    return Math.round((value / total) * 100);
  }

  private formatAverage(value: number, total: number): string {
    if (total <= 0) {
      return '0.0';
    }

    return (value / total).toFixed(1);
  }

  private statusTone(status: SignupRequestStatus): AdminTone {
    switch (status) {
      case 'Accepted':
        return 'emerald';
      case 'Rejected':
        return 'rose';
      default:
        return 'amber';
    }
  }
}
