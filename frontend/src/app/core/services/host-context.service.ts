import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';

export type HostArea = 'public' | 'business' | 'admin';

@Injectable({ providedIn: 'root' })
export class HostContextService {
  private static readonly HOSTS = {
    apex: 'mydhathuru.com',
    public: 'www.mydhathuru.com',
    business: 'app.mydhathuru.com',
    admin: 'admin.mydhathuru.com'
  } as const;

  private readonly document = inject(DOCUMENT);

  readonly hostname = signal(this.resolveHostname());
  readonly area = computed(() => this.resolveArea(this.hostname()));
  readonly isProductionHost = computed(() => {
    const host = this.hostname();
    return host === HostContextService.HOSTS.apex
      || host === HostContextService.HOSTS.public
      || host === HostContextService.HOSTS.business
      || host === HostContextService.HOSTS.admin;
  });
  readonly defaultLoginRoute = computed(() => this.area() === 'admin' ? '/portal-admin/login' : '/login');

  refresh(): void {
    this.hostname.set(this.resolveHostname());
  }

  toBusinessUrl(path: string): string {
    return this.toAreaUrl('business', path);
  }

  toAdminUrl(path: string): string {
    return this.toAreaUrl('admin', path);
  }

  toPublicUrl(path: string): string {
    return this.toAreaUrl('public', path);
  }

  private toAreaUrl(area: HostArea, path: string): string {
    const normalizedPath = this.normalizePath(path);

    if (!this.isProductionHost()) {
      return normalizedPath;
    }

    if (area === 'public') {
      return `https://${HostContextService.HOSTS.public}${normalizedPath}`;
    }

    if (area === 'business') {
      return `https://${HostContextService.HOSTS.business}${normalizedPath}`;
    }

    return `https://${HostContextService.HOSTS.admin}${normalizedPath}`;
  }

  private normalizePath(path: string): string {
    const trimmed = path.trim();
    if (!trimmed) {
      return '/';
    }

    return trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
  }

  private resolveHostname(): string {
    return this.document?.location?.hostname?.trim().toLowerCase() ?? '';
  }

  private resolveArea(hostname: string): HostArea {
    if (!hostname) {
      return 'public';
    }

    if (hostname.startsWith('admin.')) {
      return 'admin';
    }

    if (hostname.startsWith('app.')) {
      return 'business';
    }

    return 'public';
  }
}
