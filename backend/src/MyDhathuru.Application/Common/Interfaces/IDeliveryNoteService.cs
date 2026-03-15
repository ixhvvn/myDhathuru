using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.DeliveryNotes.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IDeliveryNoteService
{
    Task<PagedResult<DeliveryNoteListItemDto>> GetPagedAsync(DeliveryNoteListQuery query, CancellationToken cancellationToken = default);
    Task<DeliveryNoteDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DeliveryNoteDetailDto> CreateAsync(CreateDeliveryNoteRequest request, CancellationToken cancellationToken = default);
    Task<DeliveryNoteDetailDto> UpdateAsync(Guid id, UpdateDeliveryNoteRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task ClearAllAsync(string password, CancellationToken cancellationToken = default);
    Task<CreateInvoiceFromDeliveryNoteResultDto> CreateInvoiceFromDeliveryNoteAsync(Guid id, CreateInvoiceFromDeliveryNoteRequest request, CancellationToken cancellationToken = default);
    Task<DeliveryNoteAttachmentDto> UploadPoAttachmentAsync(Guid id, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default);
    Task<DeliveryNoteAttachmentFileDto> GetPoAttachmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DeliveryNoteAttachmentDto> UploadVesselPaymentInvoiceAttachmentAsync(Guid id, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default);
    Task<DeliveryNoteAttachmentFileDto> GetVesselPaymentInvoiceAttachmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default);
}
