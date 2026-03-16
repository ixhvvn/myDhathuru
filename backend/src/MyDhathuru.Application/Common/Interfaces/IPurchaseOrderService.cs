using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PurchaseOrders.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderListItemDto>> GetPagedAsync(PurchaseOrderListQuery query, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailDto> UpdateAsync(Guid id, UpdatePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SendEmailAsync(Guid id, SendPurchaseOrderEmailRequest request, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default);
}
