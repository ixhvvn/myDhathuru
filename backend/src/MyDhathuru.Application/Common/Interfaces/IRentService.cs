using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Rent.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IRentService
{
    Task<PagedResult<RentEntryListItemDto>> GetPagedAsync(RentEntryListQuery query, CancellationToken cancellationToken = default);
    Task<RentEntryDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RentEntryDetailDto> CreateAsync(CreateRentEntryRequest request, CancellationToken cancellationToken = default);
    Task<RentEntryDetailDto> UpdateAsync(Guid id, UpdateRentEntryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
