using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IPortalAdminBillingService
{
    Task<PortalAdminBillingDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<PortalAdminBillingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<PortalAdminBillingSettingsDto> UpdateSettingsAsync(UpdatePortalAdminBillingSettingsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortalAdminBillingBusinessOptionDto>> GetBusinessOptionsAsync(CancellationToken cancellationToken = default);
    Task<PortalAdminBillingYearlyStatementDto> GetYearlyStatementAsync(Guid tenantId, int year, CancellationToken cancellationToken = default);
    Task<PagedResult<PortalAdminBillingInvoiceListItemDto>> GetInvoicesAsync(PortalAdminBillingInvoiceListQuery query, CancellationToken cancellationToken = default);
    Task<PortalAdminBillingInvoiceDetailDto> GetInvoiceByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task<PortalAdminBillingGenerationResultDto> GenerateInvoiceAsync(PortalAdminBillingGenerateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<PortalAdminBillingGenerationResultDto> GenerateBulkInvoicesAsync(PortalAdminBillingGenerateBulkInvoicesRequest request, CancellationToken cancellationToken = default);
    Task<PortalAdminBillingGenerationResultDto> CreateCustomInvoicesAsync(PortalAdminBillingCustomInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<int> DeleteAllInvoicesAsync(CancellationToken cancellationToken = default);
    Task SendInvoiceEmailAsync(Guid invoiceId, PortalAdminBillingSendInvoiceEmailRequest request, CancellationToken cancellationToken = default);
    Task<byte[]> GetInvoicePdfAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task<PagedResult<PortalAdminBillingBusinessCustomRateDto>> GetCustomRatesAsync(PortalAdminBillingCustomRateQuery query, CancellationToken cancellationToken = default);
    Task<PortalAdminBillingBusinessCustomRateDto> CreateCustomRateAsync(PortalAdminBillingUpsertCustomRateRequest request, CancellationToken cancellationToken = default);
    Task<PortalAdminBillingBusinessCustomRateDto> UpdateCustomRateAsync(Guid rateId, PortalAdminBillingUpsertCustomRateRequest request, CancellationToken cancellationToken = default);
    Task DeleteCustomRateAsync(Guid rateId, CancellationToken cancellationToken = default);
}
