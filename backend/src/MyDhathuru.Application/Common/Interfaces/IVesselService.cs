using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Customers.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IVesselService
{
    Task<PagedResult<VesselDto>> GetPagedAsync(VesselListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VesselDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<VesselDto> CreateAsync(CreateVesselRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
