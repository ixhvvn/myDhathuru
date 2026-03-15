using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Expenses.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IExpenseService
{
    Task<PagedResult<ExpenseLedgerRowDto>> GetLedgerAsync(ExpenseLedgerQuery query, CancellationToken cancellationToken = default);
    Task<ExpenseSummaryDto> GetSummaryAsync(ExpenseSummaryQuery query, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePdfAsync(ExpenseLedgerQuery query, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateExcelAsync(ExpenseLedgerQuery query, CancellationToken cancellationToken = default);
    Task<ExpenseEntryDetailDto?> GetManualEntryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ExpenseEntryDetailDto> CreateManualEntryAsync(CreateManualExpenseEntryRequest request, CancellationToken cancellationToken = default);
    Task<ExpenseEntryDetailDto> UpdateManualEntryAsync(Guid id, UpdateManualExpenseEntryRequest request, CancellationToken cancellationToken = default);
    Task DeleteManualEntryAsync(Guid id, CancellationToken cancellationToken = default);
}
