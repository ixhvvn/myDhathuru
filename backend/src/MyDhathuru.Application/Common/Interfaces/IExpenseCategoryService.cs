using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Expenses.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IExpenseCategoryService
{
    Task<PagedResult<ExpenseCategoryDto>> GetPagedAsync(ExpenseCategoryListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseCategoryLookupDto>> GetLookupAsync(CancellationToken cancellationToken = default);
    Task<ExpenseCategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ExpenseCategoryDto> CreateAsync(CreateExpenseCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ExpenseCategoryDto> UpdateAsync(Guid id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
