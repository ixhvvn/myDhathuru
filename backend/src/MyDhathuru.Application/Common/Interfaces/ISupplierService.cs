using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Suppliers.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetPagedAsync(SupplierListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierLookupDto>> GetLookupAsync(CancellationToken cancellationToken = default);
    Task<SupplierDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
