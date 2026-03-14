import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ElementRef,
  NgZone,
  OnDestroy,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal
} from '@angular/core';
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
export class DashboardPageComponent implements OnInit, AfterViewInit, OnDestroy {
  readonly loading = signal(true);
  readonly analytics = signal<DashboardAnalytics | null>(null);
  readonly salesChartHeight = signal(356);
  readonly vesselPaletteMvr = ['#6f7ff5', '#59c7e4', '#59c79e', '#8c7dfa', '#ff9fb5', '#7ed4a6', '#9eb4ff'];
  readonly vesselPaletteUsd = ['#55b7dd', '#6fdad0', '#7fc8ff', '#67c1b8', '#8fdcf5', '#9cc8ff', '#8be7db'];
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
  private readonly zone = inject(NgZone);
  private resizeObserver?: ResizeObserver;
  private resizeFrame: number | null = null;

  @ViewChild('salesBoard', { read: ElementRef }) private salesBoardRef?: ElementRef<HTMLElement>;
  @ViewChild('salesBoardHead', { read: ElementRef }) private salesBoardHeadRef?: ElementRef<HTMLElement>;
  @ViewChild('coverageBoard', { read: ElementRef }) private coverageBoardRef?: ElementRef<HTMLElement>;

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

  readonly visibleVesselsMvr = computed(() => this.sortedVesselsByCurrency('MVR'));
  readonly visibleVesselsUsd = computed(() => this.sortedVesselsByCurrency('USD'));
  readonly visibleVesselsTotalMvr = computed(() =>
    this.visibleVesselsMvr().reduce((sum, vessel) => sum + this.vesselAmount(vessel, 'MVR'), 0));
  readonly visibleVesselsTotalUsd = computed(() =>
    this.visibleVesselsUsd().reduce((sum, vessel) => sum + this.vesselAmount(vessel, 'USD'), 0));

  readonly hasSalesData = computed(() =>
    this.salesTimeline().some((month) => month.salesMvr > 0 || month.salesUsd > 0));

  readonly hasVesselDataMvr = computed(() => this.visibleVesselsMvr().length > 0);
  readonly hasVesselDataUsd = computed(() => this.visibleVesselsUsd().length > 0);

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
        height: this.salesChartHeight(),
        parentHeightOffset: 0,
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
            legend: { position: 'bottom', horizontalAlign: 'center' }
          }
        }
      ]
    };
  });

  readonly vesselChartOptionsMvr = computed<VesselChartOptions>(() =>
    this.buildVesselChartOptions('MVR', this.visibleVesselsMvr()));
  readonly vesselChartOptionsUsd = computed<VesselChartOptions>(() =>
    this.buildVesselChartOptions('USD', this.visibleVesselsUsd()));

  private readonly portalApi = inject(PortalApiService);
  private readonly toast = inject(ToastService);

  ngOnInit(): void {
    this.loadAnalytics();
  }

  ngAfterViewInit(): void {
    this.startSalesBoardSync();
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();

    if (typeof window !== 'undefined') {
      window.removeEventListener('resize', this.handleWindowResize);

      if (this.resizeFrame !== null) {
        window.cancelAnimationFrame(this.resizeFrame);
      }
    }
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

  vesselAmount(vessel: DashboardVesselSales, currency: SupportedCurrency): number {
    return currency === 'USD' ? vessel.salesUsd : vessel.salesMvr;
  }

  private sortedVesselsByCurrency(currency: SupportedCurrency): DashboardVesselSales[] {
    return [...this.vesselSales()]
      .filter((vessel) => this.vesselAmount(vessel, currency) > 0)
      .sort((a, b) => this.vesselAmount(b, currency) - this.vesselAmount(a, currency))
      .slice(0, 4);
  }

  private buildVesselChartOptions(currency: SupportedCurrency, vessels: DashboardVesselSales[]): VesselChartOptions {
    const labels = vessels.map((vessel) => vessel.vesselName);
    const series = vessels.map((vessel) => Number(this.vesselAmount(vessel, currency)));
    const palette = currency === 'USD' ? this.vesselPaletteUsd : this.vesselPaletteMvr;

    return {
      series: series as ApexNonAxisChartSeries,
      chart: {
        type: 'donut',
        height: 208,
        fontFamily: 'Gotham, Segoe UI, sans-serif',
        foreColor: '#607197'
      },
      labels,
      colors: palette,
      legend: { show: false },
      tooltip: {
        y: {
          formatter: (value?: number) => this.formatCurrency(value ?? 0, currency)
        }
      },
      dataLabels: {
        enabled: true,
        formatter: (value: number) => (value >= 4 ? `${Math.round(value)}%` : ''),
        style: {
          fontSize: '11px',
          fontWeight: '700',
          colors: ['#ffffff']
        },
        background: {
          enabled: false
        },
        dropShadow: {
          enabled: false
        }
      },
      plotOptions: {
        pie: {
          expandOnClick: false,
          donut: {
            size: '74%',
            labels: {
              show: false
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
            chart: { height: 214 }
          }
        }
      ]
    };
  }

  vesselLegendColor(currency: SupportedCurrency, index: number): string {
    const palette = currency === 'USD' ? this.vesselPaletteUsd : this.vesselPaletteMvr;
    return palette[index % palette.length];
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

  private readonly handleWindowResize = (): void => {
    this.scheduleSalesBoardSync();
  };

  private startSalesBoardSync(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const salesBoard = this.salesBoardRef?.nativeElement;
    const salesHead = this.salesBoardHeadRef?.nativeElement;
    const coverageBoard = this.coverageBoardRef?.nativeElement;

    if (!salesBoard || !salesHead || !coverageBoard) {
      return;
    }

    if (typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(() => this.scheduleSalesBoardSync());
      this.resizeObserver.observe(salesBoard);
      this.resizeObserver.observe(salesHead);
      this.resizeObserver.observe(coverageBoard);
    }

    window.addEventListener('resize', this.handleWindowResize, { passive: true });
    this.scheduleSalesBoardSync();
  }

  private scheduleSalesBoardSync(): void {
    if (typeof window === 'undefined') {
      return;
    }

    if (this.resizeFrame !== null) {
      window.cancelAnimationFrame(this.resizeFrame);
    }

    this.resizeFrame = window.requestAnimationFrame(() => {
      this.resizeFrame = null;
      this.zone.run(() => this.syncSalesBoardHeight());
    });
  }

  private syncSalesBoardHeight(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const viewportWidth = window.innerWidth;
    const nextHeight =
      viewportWidth > 1360
        ? this.measureWideSalesChartHeight()
        : viewportWidth > 1040
          ? 318
          : viewportWidth > 860
            ? 292
            : viewportWidth > 720
              ? 260
              : 228;

    if (Math.abs(this.salesChartHeight() - nextHeight) > 1) {
      this.salesChartHeight.set(nextHeight);
    }
  }

  private measureWideSalesChartHeight(): number {
    const salesBoard = this.salesBoardRef?.nativeElement;
    const salesHead = this.salesBoardHeadRef?.nativeElement;
    const coverageBoard = this.coverageBoardRef?.nativeElement;

    if (!salesBoard || !salesHead || !coverageBoard || typeof window === 'undefined') {
      return 356;
    }

    const salesCard = salesBoard.querySelector('.card') as HTMLElement | null;
    const coverageCard = coverageBoard.querySelector('.card') as HTMLElement | null;

    if (!salesCard || !coverageCard) {
      return 356;
    }

    const salesCardStyles = window.getComputedStyle(salesCard);
    const paddingTop = parseFloat(salesCardStyles.paddingTop) || 0;
    const paddingBottom = parseFloat(salesCardStyles.paddingBottom) || 0;
    const rowGap = parseFloat(salesCardStyles.rowGap || salesCardStyles.gap) || 0;
    const coverageContentHeight = coverageCard.scrollHeight;
    const availableHeight = coverageContentHeight - paddingTop - paddingBottom - salesHead.offsetHeight - rowGap;

    return Math.max(336, Math.round(availableHeight));
  }
}
