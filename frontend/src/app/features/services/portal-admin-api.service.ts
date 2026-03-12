import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import {
  PagedResult,
  PortalAdminAuditLog,
  PortalAdminBillingBusinessOption,
  PortalAdminBillingCustomInvoiceRequest,
  PortalAdminBillingCustomRate,
  PortalAdminBillingDashboard,
  PortalAdminBillingGenerateBulkInvoicesRequest,
  PortalAdminBillingGenerateInvoiceRequest,
  PortalAdminBillingGenerationResult,
  PortalAdminBillingInvoiceDetail,
  PortalAdminBillingInvoiceListItem,
  PortalAdminBillingLogoUpload,
  PortalAdminBillingResetInvoicesResult,
  PortalAdminBillingSettings,
  PortalAdminBillingUpsertCustomRateRequest,
  PortalAdminBillingYearlyStatement,
  PortalAdminBusinessDetail,
  PortalAdminBusinessListItem,
  PortalAdminBusinessUser,
  PortalAdminDashboard,
  SignupRequestCounts,
  SignupRequestDetail,
  SignupRequestListItem
} from '../../core/models/app.models';

@Injectable({ providedIn: 'root' })
export class PortalAdminApiService {
  private readonly api = inject(ApiService);

  getDashboard(): Observable<PortalAdminDashboard> {
    return this.api.get<PortalAdminDashboard>('portal-admin/dashboard');
  }

  getSignupRequests(params: Record<string, unknown>): Observable<PagedResult<SignupRequestListItem>> {
    return this.api.get<PagedResult<SignupRequestListItem>>('portal-admin/signup-requests', params);
  }

  getSignupRequestCounts(): Observable<SignupRequestCounts> {
    return this.api.get<SignupRequestCounts>('portal-admin/signup-requests/counts');
  }

  getSignupRequestById(id: string): Observable<SignupRequestDetail> {
    return this.api.get<SignupRequestDetail>(`portal-admin/signup-requests/${id}`);
  }

  approveSignupRequest(id: string, notes?: string): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>(`portal-admin/signup-requests/${id}/approve`, { notes: notes?.trim() || null });
  }

  rejectSignupRequest(id: string, rejectionReason: string): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>(`portal-admin/signup-requests/${id}/reject`, { rejectionReason });
  }

  getBusinesses(params: Record<string, unknown>): Observable<PagedResult<PortalAdminBusinessListItem>> {
    return this.api.get<PagedResult<PortalAdminBusinessListItem>>('portal-admin/businesses', params);
  }

  getBusinessById(tenantId: string): Observable<PortalAdminBusinessDetail> {
    return this.api.get<PortalAdminBusinessDetail>(`portal-admin/businesses/${tenantId}`);
  }

  disableBusiness(tenantId: string, reason?: string): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>(`portal-admin/businesses/${tenantId}/disable`, { reason: reason?.trim() || null });
  }

  enableBusiness(tenantId: string): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>(`portal-admin/businesses/${tenantId}/enable`, {});
  }

  updateBusinessLoginDetails(
    tenantId: string,
    payload: { adminFullName: string; adminLoginEmail: string; companyEmail: string; companyPhone: string }
  ): Observable<Record<string, never>> {
    return this.api.put<Record<string, never>>(`portal-admin/businesses/${tenantId}/login-details`, payload);
  }

  sendBusinessResetLink(tenantId: string, adminEmail?: string): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>(`portal-admin/businesses/${tenantId}/send-reset-link`, { adminEmail: adminEmail?.trim() || null });
  }

  getBusinessUsers(params: Record<string, unknown>): Observable<PagedResult<PortalAdminBusinessUser>> {
    return this.api.get<PagedResult<PortalAdminBusinessUser>>('portal-admin/businesses/users', params);
  }

  getUsers(params: Record<string, unknown>): Observable<PagedResult<PortalAdminBusinessUser>> {
    return this.api.get<PagedResult<PortalAdminBusinessUser>>('portal-admin/users', params);
  }

  getAuditLogs(params: Record<string, unknown>): Observable<PagedResult<PortalAdminAuditLog>> {
    return this.api.get<PagedResult<PortalAdminAuditLog>>('portal-admin/audit-logs', params);
  }

  getBillingDashboard(): Observable<PortalAdminBillingDashboard> {
    return this.api.get<PortalAdminBillingDashboard>('portal-admin/billing/dashboard');
  }

  getBillingSettings(): Observable<PortalAdminBillingSettings> {
    return this.api.get<PortalAdminBillingSettings>('portal-admin/billing/settings');
  }

  updateBillingSettings(payload: PortalAdminBillingSettings): Observable<PortalAdminBillingSettings> {
    return this.api.put<PortalAdminBillingSettings>('portal-admin/billing/settings', payload);
  }

  uploadBillingLogo(file: File): Observable<PortalAdminBillingLogoUpload> {
    const formData = new FormData();
    formData.append('file', file);
    return this.api.post<PortalAdminBillingLogoUpload>('portal-admin/billing/logo-upload', formData);
  }

  getBillingBusinessOptions(): Observable<PortalAdminBillingBusinessOption[]> {
    return this.api.get<PortalAdminBillingBusinessOption[]>('portal-admin/billing/business-options');
  }

  getBillingYearlyStatement(tenantId: string, year: number): Observable<PortalAdminBillingYearlyStatement> {
    return this.api.get<PortalAdminBillingYearlyStatement>('portal-admin/billing/statements/yearly', { tenantId, year });
  }

  getBillingInvoices(params: Record<string, unknown>): Observable<PagedResult<PortalAdminBillingInvoiceListItem>> {
    return this.api.get<PagedResult<PortalAdminBillingInvoiceListItem>>('portal-admin/billing/invoices', params);
  }

  getBillingInvoiceById(invoiceId: string): Observable<PortalAdminBillingInvoiceDetail> {
    return this.api.get<PortalAdminBillingInvoiceDetail>(`portal-admin/billing/invoices/${invoiceId}`);
  }

  generateBillingInvoice(payload: PortalAdminBillingGenerateInvoiceRequest): Observable<PortalAdminBillingGenerationResult> {
    return this.api.post<PortalAdminBillingGenerationResult>('portal-admin/billing/invoices/generate', payload);
  }

  generateBillingInvoicesBulk(payload: PortalAdminBillingGenerateBulkInvoicesRequest): Observable<PortalAdminBillingGenerationResult> {
    return this.api.post<PortalAdminBillingGenerationResult>('portal-admin/billing/invoices/generate-bulk', payload);
  }

  createCustomBillingInvoices(payload: PortalAdminBillingCustomInvoiceRequest): Observable<PortalAdminBillingGenerationResult> {
    return this.api.post<PortalAdminBillingGenerationResult>('portal-admin/billing/invoices/custom', payload);
  }

  resetAllBillingInvoices(): Observable<PortalAdminBillingResetInvoicesResult> {
    return this.api.delete<PortalAdminBillingResetInvoicesResult>('portal-admin/billing/invoices');
  }

  sendBillingInvoiceEmail(invoiceId: string, payload: { toEmail?: string; ccEmail?: string }): Observable<Record<string, never>> {
    return this.api.post<Record<string, never>>(`portal-admin/billing/invoices/${invoiceId}/send-email`, payload);
  }

  downloadBillingInvoicePdf(invoiceId: string): Observable<Blob> {
    return this.api.getFile(`portal-admin/billing/invoices/${invoiceId}/pdf`);
  }

  getBillingCustomRates(params: Record<string, unknown>): Observable<PagedResult<PortalAdminBillingCustomRate>> {
    return this.api.get<PagedResult<PortalAdminBillingCustomRate>>('portal-admin/billing/custom-rates', params);
  }

  createBillingCustomRate(payload: PortalAdminBillingUpsertCustomRateRequest): Observable<PortalAdminBillingCustomRate> {
    return this.api.post<PortalAdminBillingCustomRate>('portal-admin/billing/custom-rates', payload);
  }

  updateBillingCustomRate(rateId: string, payload: PortalAdminBillingUpsertCustomRateRequest): Observable<PortalAdminBillingCustomRate> {
    return this.api.put<PortalAdminBillingCustomRate>(`portal-admin/billing/custom-rates/${rateId}`, payload);
  }

  deleteBillingCustomRate(rateId: string): Observable<Record<string, never>> {
    return this.api.delete<Record<string, never>>(`portal-admin/billing/custom-rates/${rateId}`);
  }
}
