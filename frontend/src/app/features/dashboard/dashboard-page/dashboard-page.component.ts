import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import {
  ApexAxisChartSeries,
  ApexChart,
  ApexDataLabels,
  ApexFill,
  ApexGrid,
  ApexLegend,
  ApexMarkers,
  ApexNonAxisChartSeries,
  ApexPlotOptions,
  ApexResponsive,
  ApexStroke,
  ApexTooltip,
  ApexXAxis,
  ApexYAxis,
  NgApexchartsModule
} from 'ng-apexcharts';
import {
  DashboardAnalytics,
  DashboardTopCustomer,
  DashboardVesselSales
} from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { PortalApiService } from '../../services/portal-api.service';

type MetricTone = 'indigo' | 'teal' | 'sky' | 'violet' | 'rose' | 'emerald';
type SupportedCurrency = 'MVR' | 'USD';

interface FocusCard {
  label: string;
  value: string;
  meta: string;
  tone: MetricTone;
}

interface CollectionRing {
  label: string;
  percent: number;
  settled: string;
  pending: string;
  tone: MetricTone;
}

interface ActivityLane {
  label: string;
  value: string;
  meta: string;
  percent: number;
  tone: MetricTone;
}

interface SignalPanel {
  label: string;
  value: string;
  meta: string;
  tone: MetricTone;
}

type SalesChartOptions = {
  series: ApexAxisChartSeries;
  chart: ApexChart;
  colors: string[];
  dataLabels: ApexDataLabels;
  stroke: ApexStroke;
  grid: ApexGrid;
  xaxis: ApexXAxis;
  yaxis: ApexYAxis;
  tooltip: ApexTooltip;
  fill: ApexFill;
  markers: ApexMarkers;
  legend: ApexLegend;
  responsive: ApexResponsive[];
};

type VesselChartOptions = {
  series: ApexNonAxisChartSeries;
  chart: ApexChart;
  labels: string[];
  colors: string[];
  legend: ApexLegend;
  tooltip: ApexTooltip;
  dataLabels: ApexDataLabels;
  plotOptions: ApexPlotOptions;
  stroke: ApexStroke;
  fill: ApexFill;
  responsive: ApexResponsive[];
};

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, NgApexchartsModule, AppCardComponent, AppCurrencyPipe, AppEmptyStateComponent],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss'
})
export class DashboardPageComponent implements OnInit {
  readonly loading = signal(true);
  readonly analytics = signal<DashboardAnalytics | null>(null);
  readonly selectedVesselCurrency = signal<SupportedCurrency>('MVR');
  readonly vesselPalette = ['#6f7ff5', '#59c7e4', '#59c79e', '#8c7dfa', '#ff9fb5', '#7ed4a6', '#9eb4ff'];
  readonly toneBackgrounds: Record<MetricTone, string> = {
    indigo: 'linear-gradient(150deg, rgba(239,244,255,.98), rgba(228,234,255,.92))',
    teal: 'linear-gradient(150deg, rgba(236,251,248,.98), rgba(216,242,235,.92))',
    sky: 'linear-gradient(150deg, rgba(238,247,255,.98), rgba(220,238,255,.92))',
    violet: 'linear-gradient(150deg, rgba(244,241,255,.98), rgba(232,226,255,.92))',
    rose: 'linear-gradient(150deg, rgba(255,241,246,.98), rgba(255,229,239,.92))',
    emerald: 'linear-gradient(150deg, rgba(238,251,246,.98), rgba(218,241,232,.92))'
  };
  readonly toneColors: Record<MetricTone, string> = {
    indigo: '#6f7ff5',
    teal: '#4dbfba',
    sky: '#58b4de',
    violet: '#8d79ef',
    rose: '#e986a6',
    emerald: '#54ba87'
  };

  private readonly compactFormatter = new Intl.NumberFormat('en-US', {
    notation: 'compact',
    maximumFractionDigits: 1
  });

  readonly summary = computed(() => this.analytics()?.summary ?? null);
  readonly topCustomers = computed(() => this.analytics()?.topCustomers ?? []);
  readonly salesTimeline = computed(() => this.analytics()?.salesLast6Months ?? []);
  readonly vesselSales = computed(() => this.analytics()?.vesselSales ?? []);
  readonly activeVesselCount = computed(() =>
    this.vesselSales().filter((vessel) => vessel.salesMvr > 0 || vessel.salesUsd > 0).length);
  readonly activePeriodLabel = computed(() => {
    const timeline = this.salesTimeline();
    return timeline.length ? timeline[timeline.length - 1].label : 'Current month';
  });

  readonly focusCards = computed<FocusCard[]>(() => {
    const summary = this.summary();
    if (!summary) {
      return [];
    }

    return [
      {
        label: 'Invoices',
        value: summary.currentMonthInvoices.toString(),
        meta: summary.invoicesTrend.label,
        tone: 'indigo'
      },
      {
        label: 'Delivery Notes',
        value: summary.currentMonthDeliveryNotes.toString(),
        meta: summary.deliveryNotesTrend.label,
        tone: 'violet'
      },
      {
        label: 'MVR Sales',
        value: this.formatCurrency(summary.currentMonthSales.mvr, 'MVR'),
        meta: summary.salesTrend.mvr.label,
        tone: 'teal'
      },
      {
        label: 'USD Sales',
        value: this.formatCurrency(summary.currentMonthSales.usd, 'USD'),
        meta: summary.salesTrend.usd.label,
        tone: 'sky'
      }
    ];
  });

  readonly collectionRings = computed<CollectionRing[]>(() => {
    const summary = this.summary();
    if (!summary) {
      return [];
    }

    return [
      this.buildCollectionRing('MVR Collection', summary.currentMonthSales.mvr, summary.currentMonthPending.mvr, 'MVR', 'teal'),
      this.buildCollectionRing('USD Collection', summary.currentMonthSales.usd, summary.currentMonthPending.usd, 'USD', 'sky')
    ];
  });

  readonly activityLanes = computed<ActivityLane[]>(() => {
    const summary = this.summary();
    if (!summary) {
      return [];
    }

    const lanes = [
      {
        label: 'New Customers',
        rawValue: summary.currentMonthNewCustomers,
        value: summary.currentMonthNewCustomers.toString(),
        meta: summary.newCustomersTrend.label,
        tone: 'rose' as MetricTone
      },
      {
        label: 'Active Vessels',
        rawValue: this.activeVesselCount(),
        value: this.activeVesselCount().toString(),
        meta: `${this.vesselSales().length} vessel entries in mix view`,
        tone: 'emerald' as MetricTone
      },
      {
        label: 'Pending MVR',
        rawValue: summary.currentMonthPending.mvr,
        value: this.formatCompactNumber(summary.currentMonthPending.mvr),
        meta: summary.pendingTrend.mvr.label,
        tone: 'sky' as MetricTone
      },
      {
        label: 'Payroll',
        rawValue: summary.currentMonthPayroll,
        value: this.formatCompactNumber(summary.currentMonthPayroll),
        meta: summary.payrollTrend.label,
        tone: 'violet' as MetricTone
      }
    ];

    const max = Math.max(...lanes.map((lane) => lane.rawValue), 1);

    return lanes.map((lane) => ({
      label: lane.label,
      value: lane.value,
      meta: lane.meta,
      percent: Math.max(10, Math.round((lane.rawValue / max) * 100)),
      tone: lane.tone
    }));
  });

  readonly compactTopCustomers = computed(() => this.topCustomers().slice(0, 2));

  readonly visibleVessels = computed(() => {
    const currency = this.selectedVesselCurrency();
    return [...this.vesselSales()]
      .sort((a, b) => this.vesselAmount(b, currency) - this.vesselAmount(a, currency))
      .slice(0, 4);
  });

  readonly signalPanels = computed<SignalPanel[]>(() => {
    const summary = this.summary();
    if (!summary) {
      return [];
    }

    const leadCustomer = this.compactTopCustomers()[0] ?? null;
    const leadVessel = this.visibleVessels()[0] ?? null;
    const averageMvr = this.salesTimeline().length
      ? this.salesTimeline().reduce((sum, month) => sum + month.salesMvr, 0) / this.salesTimeline().length
      : 0;

    return [
      {
        label: 'Pending Exposure',
        value: `${this.formatCurrency(summary.currentMonthPending.mvr, 'MVR')} | ${this.formatCurrency(summary.currentMonthPending.usd, 'USD')}`,
        meta: 'Current-month receivables still open.',
        tone: 'sky'
      },
      {
        label: 'Lead Account',
        value: leadCustomer?.customerName ?? 'Awaiting invoice momentum',
        meta: leadCustomer
          ? `${leadCustomer.invoiceCount} invoice${leadCustomer.invoiceCount === 1 ? '' : 's'} | ${this.formatCurrency(leadCustomer.salesMvr, 'MVR')}`
          : 'Top customer visibility appears after invoicing begins.',
        tone: 'teal'
      },
      {
        label: 'Lead Vessel',
        value: leadVessel?.vesselName ?? 'No vessel sales yet',
        meta: leadVessel
          ? `${this.formatCurrency(leadVessel.salesMvr, 'MVR')} | ${this.formatCurrency(leadVessel.salesUsd, 'USD')}`
          : `Average MVR month ${this.formatCurrency(averageMvr, 'MVR')}`,
        tone: 'emerald'
      }
    ];
  });

  readonly hasSalesData = computed(() =>
    this.salesTimeline().some((month) => month.salesMvr > 0 || month.salesUsd > 0));

  readonly hasVesselData = computed(() =>
    this.vesselSales().some((vessel) => this.vesselAmount(vessel, this.selectedVesselCurrency()) > 0));

  readonly salesChartOptions = computed<SalesChartOptions>(() => {
    const timeline = this.salesTimeline();
    const labels = timeline.map((month) => month.label);
    const mvrValues = timeline.map((month) => Number(month.salesMvr ?? 0));
    const usdValues = timeline.map((month) => Number(month.salesUsd ?? 0));

    return {
      series: [
        { name: 'MVR Sales', data: mvrValues },
        { name: 'USD Sales', data: usdValues }
      ],
      chart: {
        type: 'area',
        height: 236,
        toolbar: { show: false },
        zoom: { enabled: false },
        fontFamily: 'Gotham, Segoe UI, sans-serif',
        foreColor: '#607197',
        sparkline: { enabled: false }
      },
      colors: ['#6f7ff5', '#56b8da'],
      dataLabels: { enabled: false },
      stroke: { curve: 'smooth', width: 3 },
      grid: {
        strokeDashArray: 4,
        borderColor: '#e3ebfb',
        padding: { left: 6, right: 6, top: 2, bottom: 0 }
      },
      xaxis: {
        categories: labels,
        axisBorder: { show: false },
        axisTicks: { show: false },
        labels: {
          style: {
            fontSize: '11px',
            colors: Array.from({ length: labels.length }, () => '#6c7ea7')
          }
        }
      },
      yaxis: {
        labels: {
          formatter: (value: number) => this.formatCompactNumber(value),
          style: { colors: ['#6c7ea7'] }
        }
      },
      tooltip: {
        theme: 'light',
        y: {
          formatter: (value: number, context?: { seriesIndex: number }) =>
            this.formatCurrency(value ?? 0, context?.seriesIndex === 1 ? 'USD' : 'MVR')
        }
      },
      fill: {
        type: 'gradient',
        gradient: {
          shadeIntensity: 1,
          opacityFrom: 0.34,
          opacityTo: 0.05,
          stops: [0, 92, 100]
        }
      },
      markers: {
        size: 3,
        strokeColors: '#ffffff',
        strokeWidth: 2,
        hover: { size: 5 }
      },
      legend: {
        show: true,
        position: 'top',
        horizontalAlign: 'left',
        labels: { colors: '#586a92' }
      },
      responsive: [
        {
          breakpoint: 760,
          options: {
            chart: { height: 218 },
            legend: { position: 'bottom', horizontalAlign: 'center' }
          }
        }
      ]
    };
  });

  readonly vesselChartOptions = computed<VesselChartOptions>(() => {
    const vessels = this.visibleVessels();
    const selectedCurrency = this.selectedVesselCurrency();
    const labels = vessels.map((vessel) => vessel.vesselName);
    const series = vessels.map((vessel) => Number(this.vesselAmount(vessel, selectedCurrency)));
    const total = series.reduce((sum, value) => sum + value, 0);

    return {
      series: series as ApexNonAxisChartSeries,
      chart: {
        type: 'donut',
        height: 196,
        fontFamily: 'Gotham, Segoe UI, sans-serif',
        foreColor: '#607197'
      },
      labels,
      colors: this.vesselPalette,
      legend: { show: false },
      tooltip: {
        y: {
          formatter: (value?: number) => this.formatCurrency(value ?? 0, selectedCurrency)
        }
      },
      dataLabels: {
        formatter: (value: number) => `${value.toFixed(0)}%`
      },
      plotOptions: {
        pie: {
          donut: {
            size: '70%',
            labels: {
              show: true,
              total: {
                show: true,
                label: `Total ${selectedCurrency}`,
                formatter: () => this.formatCurrency(total, selectedCurrency)
              },
              value: {
                show: true,
                formatter: (value: string) => this.formatCurrency(Number(value), selectedCurrency)
              }
            }
          }
        }
      },
      stroke: { width: 2, colors: ['#f8faff'] },
      fill: { type: 'gradient' },
      responsive: [
        {
          breakpoint: 760,
          options: {
            chart: { height: 208 }
          }
        }
      ]
    };
  });

  private readonly portalApi = inject(PortalApiService);
  private readonly toast = inject(ToastService);

  ngOnInit(): void {
    this.loadAnalytics();
  }

  setVesselCurrency(currency: SupportedCurrency): void {
    this.selectedVesselCurrency.set(currency);
  }

  toneBackground(tone: MetricTone): string {
    return this.toneBackgrounds[tone];
  }

  toneColor(tone: MetricTone): string {
    return this.toneColors[tone];
  }

  customerBarWidth(customer: DashboardTopCustomer): number {
    return Math.max(12, Math.max(customer.contributionMvrPercentage, customer.contributionUsdPercentage));
  }

  vesselAmount(vessel: DashboardVesselSales, currency: SupportedCurrency = this.selectedVesselCurrency()): number {
    return currency === 'USD' ? vessel.salesUsd : vessel.salesMvr;
  }

  private loadAnalytics(): void {
    this.loading.set(true);

    this.portalApi.getDashboardAnalytics(5).subscribe({
      next: (analytics) => {
        this.analytics.set(analytics);
        this.loading.set(false);
      },
      error: () => {
        this.analytics.set(null);
        this.loading.set(false);
        this.toast.error('Failed to load dashboard analytics.');
      }
    });
  }

  private buildCollectionRing(
    label: string,
    invoiced: number,
    pending: number,
    currency: SupportedCurrency,
    tone: MetricTone
  ): CollectionRing {
    const settled = Math.max((invoiced ?? 0) - (pending ?? 0), 0);
    const percent = invoiced > 0 ? Math.round((settled / invoiced) * 100) : 0;

    return {
      label,
      percent,
      settled: `${this.formatCurrency(settled, currency)} settled`,
      pending: `${this.formatCurrency(pending ?? 0, currency)} pending`,
      tone
    };
  }

  private formatCurrency(value: number, currencyCode: SupportedCurrency): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: currencyCode,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value ?? 0);
  }

  private formatCompactNumber(value: number): string {
    return this.compactFormatter.format(value ?? 0);
  }
}
