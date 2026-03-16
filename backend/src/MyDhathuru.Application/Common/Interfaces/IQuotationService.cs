using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Quotations.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IQuotationService
{
    Task<PagedResult<QuotationListItemDto>> GetPagedAsync(QuotationListQuery query, CancellationToken cancellationToken = default);
    Task<QuotationDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuotationDetailDto> CreateAsync(CreateQuotationRequest request, CancellationToken cancellationToken = default);
    Task<QuotationDetailDto> UpdateAsync(Guid id, UpdateQuotationRequest request, CancellationToken cancellationToken = default);
    Task<QuotationConversionResultDto> ConvertToSaleAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SendEmailAsync(Guid id, SendQuotationEmailRequest request, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default);
}
