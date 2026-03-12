using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Customers.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetPagedAsync(CustomerListQuery query, CancellationToken cancellationToken = default);
    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePdfAsync(CustomerListQuery query, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateExcelAsync(CustomerListQuery query, CancellationToken cancellationToken = default);
}
