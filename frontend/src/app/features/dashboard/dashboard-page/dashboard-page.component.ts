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
  DashboardTrend,
  DashboardTopCustomer
} from '../../../core/models/app.models';
import { ToastService } from '../../../core/services/toast.service';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppCurrencyPipe } from '../../../shared/pipes/currency.pipe';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { PortalApiService } from '../../services/portal-api.service';

type MetricTone = 'indigo' | 'teal' | 'sky' | 'violet' | 'rose' | 'emerald';
type TrendDirection = 'up' | 'down' | 'neutral';
type SupportedCurrency = 'MVR' | 'USD';

interface TrendChip {
  label: string;
  direction: TrendDirection;
}

interface MetricValueLine {
  label?: string;
  value: string;
  trend?: TrendChip;
}

interface MetricCard {
  label: string;
  subtitle: string;
  lines: MetricValueLine[];
  tone: MetricTone;
  summaryTrend?: TrendChip;
  currencyMode?: boolean;
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
  imports: [
    CommonModule,
    NgApexchartsModule,
    AppCardComponent,
    AppCurrencyPipe,
    AppPageHeaderComponent,
    AppEmptyStateComponent
  ],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss'
})
export class DashboardPageComponent implements OnInit {
  readonly loading = signal(true);
  readonly analytics = signal<DashboardAnalytics | null>(null);
  readonly skeletonMetricCards = Array.from({ length: 6 });
  readonly vesselPalette = ['#6f7ff5', '#59c7e4', '#59c79e', '#8c7dfa', '#ff9fb5', '#7ed4a6', '#9eb4ff'];
  readonly selectedVesselCurrency = signal<SupportedCurrency>('MVR');

  private readonly compactFormatter = new Intl.NumberFormat('en-US', {
    notation: 'compact',
    maximumFractionDigits: 1
  });

  readonly summary = computed(() => this.analytics()?.summary ?? null);
  readonly topCustomers = computed(() => this.analytics()?.topCustomers ?? []);
  readonly salesTimeline = computed(() => this.analytics()?.salesLast6Months ?? []);
  readonly vesselSales = computed(() => this.analytics()?.vesselSales ?? []);

  readonly metricCards = computed<MetricCard[]>(() => {
    const summary = this.summary();
    if (!summary) {
      return [];
    }

    return [
      {
        label: 'Current Month Invoices',
        subtitle: 'Invoices created this month',
        lines: [{ value: summary.currentMonthInvoices.toString() }],
        tone: 'indigo',
        summaryTrend: this.mapTrend(summary.invoicesTrend)
      },
      {
        label: 'Current Month Sales',
        subtitle: 'Invoice sales recorded this month',
        lines: [
          {
            label: 'MVR',
            value: this.formatCurrency(summary.currentMonthSales.mvr, 'MVR'),
            trend: this.mapTrend(summary.salesTrend.mvr)
          },
          {
            label: 'USD',
            value: this.formatCurrency(summary.currentMonthSales.usd, 'USD'),
            trend: this.mapTrend(summary.salesTrend.usd)
          }
        ],
        tone: 'teal',
        currencyMode: true
      },
      {
        label: 'Current Month Pending',
        subtitle: 'Outstanding balances from this month',
        lines: [
          {
            label: 'MVR',
            value: this.formatCurrency(summary.currentMonthPending.mvr, 'MVR'),
            trend: this.mapTrend(summary.pendingTrend.mvr)
          },
          {
            label: 'USD',
            value: this.formatCurrency(summary.currentMonthPending.usd, 'USD'),
            trend: this.mapTrend(summary.pendingTrend.usd)
          }
        ],
        tone: 'sky',
        currencyMode: true
      },
      {
        label: 'Current Month Delivery Notes',
        subtitle: 'Delivery notes created this month',
        lines: [{ value: summary.currentMonthDeliveryNotes.toString() }],
        tone: 'violet',
        summaryTrend: this.mapTrend(summary.deliveryNotesTrend)
      },
      {
        label: 'New Customers',
        subtitle: 'Customers added this month',
        lines: [{ value: summary.currentMonthNewCustomers.toString() }],
        tone: 'rose',
        summaryTrend: this.mapTrend(summary.newCustomersTrend)
      },
      {
        label: 'Payroll This Month',
        subtitle: 'Net payroll for current month',
        lines: [{ value: this.formatCurrency(summary.currentMonthPayroll, 'MVR') }],
        tone: 'emerald',
        summaryTrend: this.mapTrend(summary.payrollTrend)
      }
    ];
  });

  readonly hasSalesData = computed(() =>
    this.salesTimeline().some((month) => month.salesMvr > 0 || month.salesUsd > 0));

  readonly hasVesselData = computed(() =>
    this.vesselSales().some((vessel) => this.vesselAmount(vessel) > 0));

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
        height: 330,
        toolbar: { show: false },
        zoom: { enabled: false },
        fontFamily: 'Gotham, Segoe UI, sans-serif',
        foreColor: '#607197'
      },
      colors: ['#6f7ff5', '#56b8da'],
      dataLabels: { enabled: false },
      stroke: { curve: 'smooth', width: 3 },
      grid: {
        strokeDashArray: 4,
        borderColor: '#e3ebfb',
        padding: { left: 8, right: 8 }
      },
      xaxis: {
        categories: labels,
        axisBorder: { show: false },
        axisTicks: { show: false },
        labels: {
          style: {
            fontSize: '12px',
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
          opacityFrom: 0.4,
          opacityTo: 0.06,
          stops: [0, 95, 100]
        }
      },
      markers: {
        size: 4,
        strokeColors: '#ffffff',
        strokeWidth: 2,
        hover: { size: 6 }
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
            chart: { height: 280 },
            legend: { position: 'bottom', horizontalAlign: 'center' }
          }
        }
      ]
    };
  });

  readonly vesselChartOptions = computed<VesselChartOptions>(() => {
    const vessels = this.vesselSales();
    const selectedCurrency = this.selectedVesselCurrency();
    const labels = vessels.map((vessel) => vessel.vesselName);
    const series = vessels.map((vessel) =>
      Number(selectedCurrency === 'USD' ? vessel.salesUsd : vessel.salesMvr));
    const total = series.reduce((sum, value) => sum + value, 0);

    return {
      series: series as ApexNonAxisChartSeries,
      chart: {
        type: 'donut',
        height: 330,
        fontFamily: 'Gotham, Segoe UI, sans-serif',
        foreColor: '#607197'
      },
      labels,
      colors: this.vesselPalette,
      legend: {
        position: 'bottom',
        horizontalAlign: 'center',
        labels: { colors: '#586a92' },
        formatter: (name: string, options: { seriesIndex: number }) => {
          const value = series[options.seriesIndex] ?? 0;
          return `${name}: ${this.formatCurrency(value, selectedCurrency)}`;
        }
      },
      tooltip: {
        y: {
          formatter: (value?: number) => this.formatCurrency(value ?? 0, selectedCurrency)
        }
      },
      dataLabels: {
        formatter: (value: number) => `${value.toFixed(1)}%`
      },
      plotOptions: {
        pie: {
          donut: {
            size: '68%',
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
      stroke: {
        width: 2,
        colors: ['#f8faff']
      },
      fill: {
        type: 'gradient'
      },
      responsive: [
        {
          breakpoint: 760,
          options: {
            chart: { height: 290 },
            legend: { position: 'bottom' }
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

  trackByCustomer(index: number, customer: DashboardTopCustomer): string {
    return `${customer.customerId}-${index}`;
  }

  setVesselCurrency(currency: SupportedCurrency): void {
    this.selectedVesselCurrency.set(currency);
  }

  vesselAmount(vessel: { salesMvr: number; salesUsd: number }): number {
    return this.selectedVesselCurrency() === 'USD' ? vessel.salesUsd : vessel.salesMvr;
  }

  vesselContribution(vessel: { contributionMvrPercentage: number; contributionUsdPercentage: number }): number {
    return this.selectedVesselCurrency() === 'USD'
      ? vessel.contributionUsdPercentage
      : vessel.contributionMvrPercentage;
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

  private mapTrend(trend: DashboardTrend): TrendChip {
    return {
      label: trend.label,
      direction: trend.direction
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
    return this.compactFormatter.format(value);
  }
}
