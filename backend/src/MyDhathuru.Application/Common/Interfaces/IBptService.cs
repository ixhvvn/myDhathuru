using MyDhathuru.Application.Bpt.Dtos;
using MyDhathuru.Application.Reports.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IBptService
{
    Task<BptReportDto> GetReportAsync(BptReportQuery query, CancellationToken cancellationToken = default);
    Task<ReportExportResultDto> ExportExcelAsync(BptReportExportRequest request, CancellationToken cancellationToken = default);
    Task<ReportExportResultDto> ExportPdfAsync(BptReportExportRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BptCategoryLookupDto>> GetCategoryLookupAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BptExpenseMappingDto>> GetExpenseMappingsAsync(CancellationToken cancellationToken = default);
    Task<BptExpenseMappingDto> UpsertExpenseMappingAsync(Guid expenseCategoryId, UpsertBptExpenseMappingRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BptExchangeRateDto>> GetExchangeRatesAsync(BptExchangeRateListQuery query, CancellationToken cancellationToken = default);
    Task<BptExchangeRateDto> CreateExchangeRateAsync(UpsertBptExchangeRateRequest request, CancellationToken cancellationToken = default);
    Task<BptExchangeRateDto> UpdateExchangeRateAsync(Guid id, UpsertBptExchangeRateRequest request, CancellationToken cancellationToken = default);
    Task DeleteExchangeRateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesAdjustmentDto>> GetSalesAdjustmentsAsync(SalesAdjustmentListQuery query, CancellationToken cancellationToken = default);
    Task<SalesAdjustmentDto> CreateSalesAdjustmentAsync(CreateSalesAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<SalesAdjustmentDto> UpdateSalesAdjustmentAsync(Guid id, UpdateSalesAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteSalesAdjustmentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OtherIncomeEntryDto>> GetOtherIncomeEntriesAsync(OtherIncomeEntryListQuery query, CancellationToken cancellationToken = default);
    Task<OtherIncomeEntryDto> CreateOtherIncomeEntryAsync(CreateOtherIncomeEntryRequest request, CancellationToken cancellationToken = default);
    Task<OtherIncomeEntryDto> UpdateOtherIncomeEntryAsync(Guid id, UpdateOtherIncomeEntryRequest request, CancellationToken cancellationToken = default);
    Task DeleteOtherIncomeEntryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BptAdjustmentDto>> GetAdjustmentsAsync(BptAdjustmentListQuery query, CancellationToken cancellationToken = default);
    Task<BptAdjustmentDto> CreateAdjustmentAsync(CreateBptAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<BptAdjustmentDto> UpdateAdjustmentAsync(Guid id, UpdateBptAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAdjustmentAsync(Guid id, CancellationToken cancellationToken = default);
}
