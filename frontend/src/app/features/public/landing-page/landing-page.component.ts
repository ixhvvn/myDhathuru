import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { AfterViewInit, Component, ElementRef, NgZone, OnDestroy, PLATFORM_ID, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HostContextService } from '../../../core/services/host-context.service';

type FeatureIcon = 'delivery' | 'invoice' | 'accounts' | 'customers' | 'payroll' | 'reports';

interface NavLink {
  readonly label: string;
  readonly target: string;
}

interface FeatureItem {
  readonly icon: FeatureIcon;
  readonly title: string;
  readonly description: string;
}

interface WorkflowStep {
  readonly title: string;
  readonly description: string;
}

interface ControlPoint {
  readonly title: string;
  readonly description: string;
}

interface SalesTrendPoint {
  readonly x: number;
  readonly y: number;
  readonly label: string;
  readonly value: number;
}

interface SalesTrendChart {
  readonly points: readonly SalesTrendPoint[];
  readonly gridLines: readonly number[];
  readonly linePath: string;
  readonly areaPath: string;
}

@Component({
  selector: 'app-landing-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './landing-page.component.html',
  styleUrl: './landing-page.component.scss'
})
export class LandingPageComponent implements AfterViewInit, OnDestroy {
  readonly navLinks: readonly NavLink[] = [
    { label: 'Home', target: 'home' },
    { label: 'Features', target: 'features' },
    { label: 'Pricing', target: 'pricing' },
    { label: 'Contact', target: 'contact' }
  ];

  readonly features: readonly FeatureItem[] = [
    {
      icon: 'delivery',
      title: 'Delivery Notes',
      description: 'Create and track vessel delivery records with clear statuses and audit history.'
    },
    {
      icon: 'invoice',
      title: 'Invoicing',
      description: 'Generate invoices quickly, record payments, and monitor outstanding balances.'
    },
    {
      icon: 'accounts',
      title: 'Customer Accounts',
      description: 'Monitor account balances and customer statement activity in one place.'
    },
    {
      icon: 'customers',
      title: 'Customers',
      description: 'Manage customer and company details across daily operations and billing workflows.'
    },
    {
      icon: 'payroll',
      title: 'Payroll',
      description: 'Run salary calculations and produce salary slips with organized payroll records.'
    },
    {
      icon: 'reports',
      title: 'Reports',
      description: 'Review performance dashboards and export reports to PDF and Excel formats.'
    }
  ];

  readonly workflowSteps: readonly WorkflowStep[] = [
    {
      title: 'Create delivery note',
      description: 'Capture vessel service details and quantities with structured records.'
    },
    {
      title: 'Convert to invoice',
      description: 'Generate billing directly from delivery activity to avoid duplicate work.'
    },
    {
      title: 'Track payment status',
      description: 'Monitor paid, pending, and overdue invoices in real time.'
    },
    {
      title: 'Monitor account balance',
      description: 'Keep customer account statements accurate and up to date.'
    },
    {
      title: 'Generate reports',
      description: 'Review business performance and export executive-ready summaries.'
    }
  ];

  readonly controlPoints: readonly ControlPoint[] = [
    {
      title: 'Centralized operations',
      description: 'Run delivery, invoicing, payroll, and reporting from a unified workspace.'
    },
    {
      title: 'Secure account-based access',
      description: 'Keep business data protected with role-based, authenticated access flows.'
    },
    {
      title: 'Scalable record management',
      description: 'Organize customer, payroll, and financial records as operations grow.'
    }
  ];

  readonly salesTrend = this.createSalesTrend(
    ['Oct', 'Nov', 'Dec', 'Jan', 'Feb', 'Mar'],
    [420, 455, 446, 518, 502, 549]
  );

  readonly currentYear = new Date().getFullYear();
  readonly businessSignInUrl = computed(() => this.hostContext.toBusinessUrl('/login'));
  readonly businessSignUpUrl = computed(() => this.hostContext.toBusinessUrl('/login?mode=signup'));

  private readonly document = inject(DOCUMENT);
  private readonly hostElement = inject(ElementRef) as ElementRef<HTMLElement>;
  private readonly zone = inject(NgZone);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly hostContext = inject(HostContextService);
  private revealObserver: IntersectionObserver | null = null;

  ngAfterViewInit(): void {
    this.initializeScrollReveal();
  }

  ngOnDestroy(): void {
    this.revealObserver?.disconnect();
  }

  scrollToSection(event: Event, sectionId: string): void {
    event.preventDefault();
    const target = this.document.getElementById(sectionId);
    target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  private initializeScrollReveal(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const host = this.hostElement.nativeElement as HTMLElement;
    const revealTargets = Array.from(host.querySelectorAll('[data-reveal]')) as HTMLElement[];
    if (revealTargets.length === 0) {
      return;
    }

    if (!('IntersectionObserver' in window)) {
      revealTargets.forEach((target) => target.classList.add('is-visible'));
      return;
    }

    this.zone.runOutsideAngular(() => {
      this.revealObserver = new IntersectionObserver(
        (entries) => {
          entries.forEach((entry) => {
            if (!entry.isIntersecting) {
              return;
            }

            const target = entry.target as HTMLElement;
            target.classList.add('is-visible');
            this.revealObserver?.unobserve(target);
          });
        },
        {
          threshold: 0.18,
          rootMargin: '0px 0px -10% 0px'
        }
      );

      revealTargets.forEach((target) => this.revealObserver?.observe(target));
    });
  }

  private createSalesTrend(labels: readonly string[], values: readonly number[]): SalesTrendChart {
    const top = 22;
    const bottom = 154;
    const left = 18;
    const right = 602;
    const minValue = Math.min(...values);
    const maxValue = Math.max(...values);
    const valuePadding = Math.max(12, Math.round((maxValue - minValue) * 0.15));
    const minRange = Math.max(0, minValue - valuePadding);
    const maxRange = maxValue + valuePadding;
    const range = Math.max(1, maxRange - minRange);
    const stepX = labels.length > 1 ? (right - left) / (labels.length - 1) : 0;

    const points: SalesTrendPoint[] = labels.map((label, index) => {
      const value = values[index] ?? values[values.length - 1] ?? 0;
      const x = left + stepX * index;
      const y = bottom - ((value - minRange) / range) * (bottom - top);
      return {
        x: Number(x.toFixed(2)),
        y: Number(y.toFixed(2)),
        label,
        value
      };
    });

    const linePath = points
      .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`)
      .join(' ');

    const areaPath = `M ${left} ${bottom} ${points
      .map((point) => `L ${point.x} ${point.y}`)
      .join(' ')} L ${right} ${bottom} Z`;

    const gridLines = Array.from({ length: 6 }, (_, index) =>
      Number((top + ((bottom - top) / 5) * index).toFixed(2))
    );

    return {
      points,
      gridLines,
      linePath,
      areaPath
    };
  }
}
