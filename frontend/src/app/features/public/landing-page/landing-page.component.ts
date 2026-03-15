import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { AfterViewInit, Component, ElementRef, NgZone, OnDestroy, PLATFORM_ID, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HostContextService } from '../../../core/services/host-context.service';

type AccentTone = 'indigo' | 'mint' | 'lilac' | 'amber';

interface NavLink {
  readonly label: string;
  readonly target: string;
}

interface HeroSignal {
  readonly label: string;
  readonly title: string;
  readonly detail: string;
  readonly tone: AccentTone;
}

interface CapabilityCluster {
  readonly kicker: string;
  readonly title: string;
  readonly description: string;
  readonly heroSummary: string;
  readonly heroSlot: string;
  readonly token: string;
  readonly tone: AccentTone;
  readonly heroChips: readonly string[];
  readonly capabilities: readonly string[];
  readonly lane: readonly string[];
  readonly emphasis: string;
}

interface HeroOrbitNode {
  readonly label: string;
  readonly slot: string;
  readonly tone: AccentTone;
}

interface WorkflowStage {
  readonly lane: string;
  readonly title: string;
  readonly description: string;
}

interface VisibilityNode {
  readonly label: string;
  readonly detail: string;
  readonly slot: string;
  readonly tone: AccentTone;
}

interface MapInsight {
  readonly title: string;
  readonly detail: string;
}

interface ReportingHighlight {
  readonly tag: string;
  readonly title: string;
  readonly description: string;
  readonly tone: AccentTone;
}

interface ReportingWidget {
  readonly title: string;
  readonly detail: string;
  readonly tone: AccentTone;
}

interface ChartLane {
  readonly label: string;
  readonly height: number;
  readonly tone: AccentTone;
}

interface WorkflowBenefit {
  readonly title: string;
  readonly description: string;
}

interface StructureLane {
  readonly title: string;
  readonly detail: string;
  readonly items: readonly string[];
  readonly tone: AccentTone;
}

interface SubscriptionLayer {
  readonly title: string;
  readonly detail: string;
  readonly tone: AccentTone;
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
    { label: 'Workflow', target: 'workflow' },
    { label: 'Modules', target: 'modules' },
    { label: 'Visibility', target: 'visibility' },
    { label: 'Pricing', target: 'pricing' },
    { label: 'Contact', target: 'contact' }
  ];

  readonly heroTags: readonly string[] = [
    'Delivery notes to invoicing flow',
    'Customer statements and collections visibility',
    'Received invoices, vouchers, rent, and expenses',
    'Payroll, warning forms, and salary exports',
    'MIRA input/output tax and BPT reporting',
    'Tenant-scoped multi-business structure'
  ];

  readonly heroSignals: readonly HeroSignal[] = [
    {
      label: 'Sales & receivables',
      title: 'Delivery notes, quotations, invoices, and customer statements stay connected.',
      detail: 'Move from issued work to billed work and collection follow-up without re-entering the same record.',
      tone: 'indigo'
    },
    {
      label: 'Purchases & expenses',
      title: 'Received invoices, payment vouchers, rent, and expense tracking sit in one flow.',
      detail: 'Keep supplier-side records and operating expense visibility inside the same finance workspace.',
      tone: 'mint'
    },
    {
      label: 'Payroll & HR',
      title: 'Payroll cycles sit beside staff records, salary visibility, and conduct forms.',
      detail: 'Salary processing and disciplinary documentation stay organized in one controlled system.',
      tone: 'lilac'
    },
    {
      label: 'Tax & compliance',
      title: 'MIRA input tax, output tax, and BPT reporting pull from live records.',
      detail: 'Review period-based compliance views and export-ready statements without stitching together manual totals.',
      tone: 'amber'
    }
  ];

  readonly capabilityClusters: readonly CapabilityCluster[] = [
    {
      kicker: 'Sales & Receivables',
      title: 'Run the full customer-side document lane.',
      description: 'Manage customers, quotations, invoices issued, statements, and collections visibility from the same live workflow.',
      heroSummary: 'Customers, quotations, invoices issued, and statements.',
      heroSlot: 'north-west',
      token: 'SR',
      tone: 'indigo',
      heroChips: ['Customers', 'Quotations', 'Statements'],
      capabilities: ['Customers', 'Quotations', 'Invoices Issued', 'Customer Statements', 'Collections Visibility'],
      lane: ['Quote', 'Issue', 'Review', 'Collect'],
      emphasis: 'Built for day-to-day commercial flow, not isolated invoice screens.'
    },
    {
      kicker: 'Purchases & Expenses',
      title: 'Bring supplier-side control into daily finance work.',
      description: 'Record received invoices, issue payment vouchers, manage rent, and classify operating expenses with cleaner visibility.',
      heroSummary: 'Received invoices, vouchers, rent, and expenses.',
      heroSlot: 'north-east',
      token: 'PE',
      tone: 'mint',
      heroChips: ['Received Invoices', 'Vouchers', 'Expense Ledger'],
      capabilities: ['Received Invoices', 'Payment Vouchers', 'Rent Tracking', 'Expense Ledger', 'Supplier-side Tracking', 'Operating Expense Visibility'],
      lane: ['Receive', 'Review', 'Pay', 'Track'],
      emphasis: 'Purchases and expense movement stay traceable back to source documents.'
    },
    {
      kicker: 'Payroll & HR',
      title: 'Keep payroll, staff records, and conduct handling together.',
      description: 'Run payroll periods, review salary visibility, manage staff master data, and issue discipline or warning forms from one workspace.',
      heroSummary: 'Staff records, payroll, salary visibility, and warning forms.',
      heroSlot: 'south-west',
      token: 'PH',
      tone: 'lilac',
      heroChips: ['Staff Records', 'Payroll', 'Warning Forms'],
      capabilities: ['Staff Records', 'Payroll Cycles', 'Salary Visibility', 'Salary Slips', 'Discipline / Warning Forms'],
      lane: ['Maintain', 'Run', 'Review', 'Export'],
      emphasis: 'Payroll reporting and staff processes stay aligned instead of split across tools.'
    },
    {
      kicker: 'Tax & Compliance',
      title: 'Translate live finance records into compliance-ready output.',
      description: 'Prepare MIRA input tax, output tax, and BPT statements with period filters, exports, and reporting visibility tied to stored records.',
      heroSummary: 'MIRA input tax, output tax, BPT, and exports.',
      heroSlot: 'south-east',
      token: 'TC',
      tone: 'amber',
      heroChips: ['MIRA Input Tax', 'MIRA Output Tax', 'BPT'],
      capabilities: ['MIRA Input Tax', 'MIRA Output Tax', 'BPT Reporting', 'Period-based Summaries', 'PDF / Excel Exports'],
      lane: ['Classify', 'Review', 'Generate', 'Export'],
      emphasis: 'Compliance reporting sits on top of the live system rather than after-the-fact workarounds.'
    }
  ];

  readonly heroOrbitNodes: readonly HeroOrbitNode[] = [
    { label: 'Statements', slot: 'top', tone: 'indigo' },
    { label: 'Received Invoices', slot: 'upper-right', tone: 'mint' },
    { label: 'Payment Vouchers', slot: 'mid-right', tone: 'amber' },
    { label: 'BPT', slot: 'bottom', tone: 'amber' },
    { label: 'Discipline Forms', slot: 'lower-left', tone: 'lilac' },
    { label: 'Rent & Expenses', slot: 'upper-left', tone: 'mint' }
  ];

  readonly workflowStages: readonly WorkflowStage[] = [
    {
      lane: 'Operations',
      title: 'Create delivery note',
      description: 'Capture the operational record that starts billing and traceability.'
    },
    {
      lane: 'Billing',
      title: 'Convert to invoice',
      description: 'Turn completed work into invoices without running a second manual process.'
    },
    {
      lane: 'Receivables',
      title: 'Track collections and statements',
      description: 'Keep customer balance review and statement visibility live by period.'
    },
    {
      lane: 'Purchases',
      title: 'Record received invoices',
      description: 'Bring supplier bills into the system with source-linked document detail.'
    },
    {
      lane: 'Payments',
      title: 'Create payment vouchers',
      description: 'Document outgoing payments against the records that generated them.'
    },
    {
      lane: 'Expenses',
      title: 'Track rent and operating expenses',
      description: 'Classify recurring and day-to-day costs for reporting and compliance.'
    },
    {
      lane: 'Payroll',
      title: 'Run payroll and staff processes',
      description: 'Keep salary periods, slips, staff records, and warning forms in sync.'
    },
    {
      lane: 'Compliance',
      title: 'Generate MIRA outputs and BPT review',
      description: 'Prepare period-based tax workflows and BPT statements from live records.'
    }
  ];

  readonly visibilityNodes: readonly VisibilityNode[] = [
    { label: 'Customers', detail: 'Receivables', slot: 'north-west', tone: 'indigo' },
    { label: 'Delivery Notes', detail: 'Source record', slot: 'north', tone: 'indigo' },
    { label: 'Invoices', detail: 'Billing', slot: 'north-east', tone: 'indigo' },
    { label: 'Statements', detail: 'Collections', slot: 'east-top', tone: 'indigo' },
    { label: 'Received Invoices', detail: 'Supplier bills', slot: 'east', tone: 'mint' },
    { label: 'Payment Vouchers', detail: 'Outgoing payments', slot: 'south-east', tone: 'amber' },
    { label: 'Expenses', detail: 'Ledger', slot: 'south', tone: 'mint' },
    { label: 'Payroll', detail: 'Salary cycles', slot: 'south-west', tone: 'lilac' },
    { label: 'Suppliers', detail: 'Purchases', slot: 'west', tone: 'mint' },
    { label: 'MIRA', detail: 'Tax workflows', slot: 'inner-left', tone: 'amber' },
    { label: 'BPT', detail: 'Income statement', slot: 'inner-right', tone: 'amber' }
  ];

  readonly mapInsights: readonly MapInsight[] = [
    {
      title: 'Trace every total',
      detail: 'Move from summary visibility back to the document lane that produced the figure.'
    },
    {
      title: 'Filter by period',
      detail: 'Review operations, expenses, payroll, and compliance views by year, quarter, or custom dates.'
    },
    {
      title: 'Keep businesses structured',
      detail: 'Tenant-scoped records, role-based access, and export-ready output stay aligned as operations expand.'
    }
  ];

  readonly reportingHighlights: readonly ReportingHighlight[] = [
    {
      tag: 'Statements',
      title: 'Customer statement visibility',
      description: 'Review balances, payments, and outstanding activity by the selected period.',
      tone: 'indigo'
    },
    {
      tag: 'Expenses',
      title: 'Expense breakdown control',
      description: 'Group operating costs across rent, supplier bills, vouchers, and ledger entries.',
      tone: 'mint'
    },
    {
      tag: 'Payroll',
      title: 'Salary totals and staff exports',
      description: 'Keep payroll periods, salary visibility, and staff records ready for review and handoff.',
      tone: 'lilac'
    },
    {
      tag: 'Compliance',
      title: 'MIRA and BPT reporting visibility',
      description: 'Generate MIRA input tax, output tax, and BPT statement views from live data.',
      tone: 'amber'
    }
  ];

  readonly reportingWidgets: readonly ReportingWidget[] = [
    {
      title: 'Customer statements',
      detail: 'Balance visibility, statement exports, and receivable follow-up.',
      tone: 'indigo'
    },
    {
      title: 'Expense categories',
      detail: 'Rent, vouchers, received invoices, and ledger-based operating costs.',
      tone: 'mint'
    },
    {
      title: 'Payroll totals',
      detail: 'Salary-focused summaries with exports and period handling.',
      tone: 'lilac'
    },
    {
      title: 'MIRA + BPT',
      detail: 'Compliance-ready previews, statements, and export paths.',
      tone: 'amber'
    }
  ];

  readonly reportingChart: readonly ChartLane[] = [
    { label: 'Sales', height: 86, tone: 'indigo' },
    { label: 'Receivables', height: 72, tone: 'indigo' },
    { label: 'Expenses', height: 63, tone: 'mint' },
    { label: 'Payroll', height: 58, tone: 'lilac' },
    { label: 'Compliance', height: 76, tone: 'amber' }
  ];

  readonly exportTags: readonly string[] = ['PDF exports', 'Excel exports', 'Period filters', 'Live-source totals'];

  readonly workflowBenefits: readonly WorkflowBenefit[] = [
    {
      title: 'Fewer disconnected spreadsheets',
      description: 'Operational records, finance workflows, payroll cycles, and tax views live in one working system.'
    },
    {
      title: 'Better document traceability',
      description: 'Delivery notes, invoices, statements, received invoices, and vouchers remain connected to their source flow.'
    },
    {
      title: 'Cleaner finance handoffs',
      description: 'Collections, expenses, rent, and outgoing payments are easier to review when they share the same timeline.'
    },
    {
      title: 'Organized expense visibility',
      description: 'Operating costs can be reviewed by category instead of disappearing into manual notes and side files.'
    },
    {
      title: 'One place for operations, finance, and compliance',
      description: 'Reporting stays stronger when the same system handles the inputs, the review layer, and the exports.'
    }
  ];

  readonly structureLanes: readonly StructureLane[] = [
    {
      title: 'Operations lane',
      detail: 'Capture work, issue commercial documents, and keep customer-side movement visible.',
      items: ['Delivery Notes', 'Quotations', 'Invoices Issued', 'Customer Statements'],
      tone: 'indigo'
    },
    {
      title: 'Finance lane',
      detail: 'Bring supplier-side and operating expense workflows into one controlled view.',
      items: ['Received Invoices', 'Payment Vouchers', 'Rent', 'Expense Ledger'],
      tone: 'mint'
    },
    {
      title: 'People lane',
      detail: 'Run staff processes without separating payroll records from conduct handling.',
      items: ['Staff Records', 'Payroll', 'Salary Slips', 'Discipline / Warning Forms'],
      tone: 'lilac'
    },
    {
      title: 'Compliance lane',
      detail: 'Review period-based tax outputs and export-ready statements from stored records.',
      items: ['MIRA Input Tax', 'MIRA Output Tax', 'BPT', 'Operational Reports'],
      tone: 'amber'
    }
  ];

  readonly subscriptionLayers: readonly SubscriptionLayer[] = [
    {
      title: 'Operations workspace',
      detail: 'Delivery notes, quotations, invoices, customer records, and statement visibility.',
      tone: 'indigo'
    },
    {
      title: 'Finance workflow stack',
      detail: 'Received invoices, payment vouchers, rent tracking, and expense ledger control.',
      tone: 'mint'
    },
    {
      title: 'Payroll and staff processes',
      detail: 'Staff records, salary cycles, salary exports, and warning or disciplinary forms.',
      tone: 'lilac'
    },
    {
      title: 'Reporting and compliance layer',
      detail: 'Operational reporting, MIRA-ready data, and BPT statement generation with exports.',
      tone: 'amber'
    }
  ];

  readonly subscriptionTags: readonly string[] = [
    'One subscription',
    'Guided onboarding',
    'Role and business setup',
    'Document templates',
    'Multi-business ready'
  ];

  readonly currentYear = new Date().getFullYear();
  readonly requestDemoUrl = 'mailto:mydhathuru@gmail.com?subject=Request%20Demo%20-%20myDhathuru';
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

    const host = this.hostElement.nativeElement;
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
          threshold: 0.16,
          rootMargin: '0px 0px -10% 0px'
        }
      );

      revealTargets.forEach((target) => this.revealObserver?.observe(target));
    });
  }
}
