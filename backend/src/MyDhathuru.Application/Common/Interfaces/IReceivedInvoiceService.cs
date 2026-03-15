using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.ReceivedInvoices.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IReceivedInvoiceService
{
    Task<PagedResult<ReceivedInvoiceListItemDto>> GetPagedAsync(ReceivedInvoiceListQuery query, CancellationToken cancellationToken = default);
    Task<ReceivedInvoiceDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReceivedInvoiceDetailDto> CreateAsync(CreateReceivedInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<ReceivedInvoiceDetailDto> UpdateAsync(Guid id, UpdateReceivedInvoiceRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReceivedInvoicePaymentDto> RecordPaymentAsync(Guid id, RecordReceivedInvoicePaymentRequest request, CancellationToken cancellationToken = default);
    Task<ReceivedInvoiceAttachmentDto> UploadAttachmentAsync(Guid id, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default);
    Task<ReceivedInvoiceAttachmentFileDto> GetAttachmentAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken = default);
}
