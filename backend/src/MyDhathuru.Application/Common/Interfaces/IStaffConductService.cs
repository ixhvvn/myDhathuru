using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.StaffConduct.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IStaffConductService
{
    Task<PagedResult<StaffConductListItemDto>> GetPagedAsync(StaffConductListQuery query, CancellationToken cancellationToken = default);
    Task<StaffConductSummaryDto> GetSummaryAsync(StaffConductListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffConductStaffOptionDto>> GetStaffOptionsAsync(CancellationToken cancellationToken = default);
    Task<StaffConductDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffConductDetailDto> CreateAsync(CreateStaffConductFormRequest request, CancellationToken cancellationToken = default);
    Task<StaffConductDetailDto> UpdateAsync(Guid id, UpdateStaffConductFormRequest request, CancellationToken cancellationToken = default);
    Task<byte[]> ExportPdfAsync(Guid id, CancellationToken cancellationToken = default);
    Task<byte[]> ExportExcelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<byte[]> ExportSummaryPdfAsync(StaffConductListQuery query, CancellationToken cancellationToken = default);
    Task<byte[]> ExportSummaryExcelAsync(StaffConductListQuery query, CancellationToken cancellationToken = default);
}
