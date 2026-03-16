using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Invoices.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IInvoiceService
{
    Task<PagedResult<InvoiceListItemDto>> GetPagedAsync(InvoiceListQuery query, CancellationToken cancellationToken = default);
    Task<InvoiceDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InvoiceDetailDto> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<InvoiceDetailDto> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InvoicePaymentDto> ReceivePaymentAsync(Guid invoiceId, ReceiveInvoicePaymentRequest request, CancellationToken cancellationToken = default);
    Task ClearAllAsync(string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoicePaymentDto>> GetPaymentHistoryAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task SendEmailAsync(Guid id, SendInvoiceEmailRequest request, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default);
}
