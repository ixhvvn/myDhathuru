using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PaymentVouchers.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IPaymentVoucherService
{
    Task<PagedResult<PaymentVoucherListItemDto>> GetPagedAsync(PaymentVoucherListQuery query, CancellationToken cancellationToken = default);
    Task<PaymentVoucherDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaymentVoucherDetailDto> CreateAsync(CreatePaymentVoucherRequest request, CancellationToken cancellationToken = default);
    Task<PaymentVoucherDetailDto> UpdateAsync(Guid id, UpdatePaymentVoucherRequest request, CancellationToken cancellationToken = default);
    Task<PaymentVoucherDetailDto> UpdateStatusAsync(Guid id, PaymentVoucherStatus status, string? notes, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default);
}
