using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Bpt.Dtos;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;

namespace MyDhathuru.Api.Controllers;

[Route("api/bpt")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class BptController : BaseApiController
{
    private readonly IBptService _bptService;

    public BptController(IBptService bptService)
    {
        _bptService = bptService;
    }

    [HttpGet("report")]
    public async Task<ActionResult<ApiResponse<BptReportDto>>> GetReport([FromQuery] BptReportQuery query, CancellationToken cancellationToken)
    {
        var result = await _bptService.GetReportAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("export/excel")]
    public async Task<IActionResult> ExportExcel([FromBody] BptReportExportRequest request, CancellationToken cancellationToken)
    {
        var export = await _bptService.ExportExcelAsync(request, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpPost("export/pdf")]
    public async Task<IActionResult> ExportPdf([FromBody] BptReportExportRequest request, CancellationToken cancellationToken)
    {
        var export = await _bptService.ExportPdfAsync(request, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BptCategoryLookupDto>>>> GetCategories(CancellationToken cancellationToken)
    {
        var result = await _bptService.GetCategoryLookupAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("mappings")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BptExpenseMappingDto>>>> GetMappings(CancellationToken cancellationToken)
    {
        var result = await _bptService.GetExpenseMappingsAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpPut("mappings/{expenseCategoryId:guid}")]
    public async Task<ActionResult<ApiResponse<BptExpenseMappingDto>>> UpsertMapping(Guid expenseCategoryId, [FromBody] UpsertBptExpenseMappingRequest request, CancellationToken cancellationToken)
    {
        var result = await _bptService.UpsertExpenseMappingAsync(expenseCategoryId, request, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("exchange-rates")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BptExchangeRateDto>>>> GetExchangeRates([FromQuery] BptExchangeRateListQuery query, CancellationToken cancellationToken)
    {
        var result = await _bptService.GetExchangeRatesAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("exchange-rates")]
    public async Task<ActionResult<ApiResponse<BptExchangeRateDto>>> CreateExchangeRate([FromBody] UpsertBptExchangeRateRequest request, CancellationToken cancellationToken)
    {
        var result = await _bptService.CreateExchangeRateAsync(request, cancellationToken);
        return OkResponse(result);
    }

    [HttpPut("exchange-rates/{id:guid}")]
    public async Task<ActionResult<ApiResponse<BptExchangeRateDto>>> UpdateExchangeRate(Guid id, [FromBody] UpsertBptExchangeRateRequest request, CancellationToken cancellationToken)
    {
        var result = await _bptService.UpdateExchangeRateAsync(id, request, cancellationToken);
        return OkResponse(result);
    }

    [HttpDelete("exchange-rates/{id:guid}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, string>>>> DeleteExchangeRate(Guid id, CancellationToken cancellationToken)
    {
        await _bptService.DeleteExchangeRateAsync(id, cancellationToken);
        return OkResponse(new Dictionary<string, string>());
    }

    [HttpGet("sales-adjustments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SalesAdjustmentDto>>>> GetSalesAdjustments([FromQuery] SalesAdjustmentListQuery query, CancellationToken cancellationToken)
    {
        var result = await _bptService.GetSalesAdjustmentsAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("sales-adjustments")]
    public async Task<ActionResult<ApiResponse<SalesAdjustmentDto>>> CreateSalesAdjustment([FromBody] CreateSalesAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _bptService.CreateSalesAdjustmentAsync(request, cancellationToken);
        return OkResponse(result);
    }

    [HttpPut("sales-adjustments/{id:guid}")]
    public async Task<ActionResult<ApiResponse<SalesAdjustmentDto>>> UpdateSalesAdjustment(Guid id, [FromBody] UpdateSalesAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _bptService.UpdateSalesAdjustmentAsync(id, request, cancellationToken);
        return OkResponse(result);
    }

    [HttpDelete("sales-adjustments/{id:guid}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, string>>>> DeleteSalesAdjustment(Guid id, CancellationToken cancellationToken)
    {
        await _bptService.DeleteSalesAdjustmentAsync(id, cancellationToken);
        return OkResponse(new Dictionary<string, string>());
    }

    [HttpGet("other-income")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OtherIncomeEntryDto>>>> GetOtherIncome([FromQuery] OtherIncomeEntryListQuery query, CancellationToken cancellationToken)
    {
        var result = await _bptService.GetOtherIncomeEntriesAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("other-income")]
    public async Task<ActionResult<ApiResponse<OtherIncomeEntryDto>>> CreateOtherIncome([FromBody] CreateOtherIncomeEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await _bptService.CreateOtherIncomeEntryAsync(request, cancellationToken);
        return OkResponse(result);
    }

    [HttpPut("other-income/{id:guid}")]
    public async Task<ActionResult<ApiResponse<OtherIncomeEntryDto>>> UpdateOtherIncome(Guid id, [FromBody] UpdateOtherIncomeEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await _bptService.UpdateOtherIncomeEntryAsync(id, request, cancellationToken);
        return OkResponse(result);
    }

    [HttpDelete("other-income/{id:guid}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, string>>>> DeleteOtherIncome(Guid id, CancellationToken cancellationToken)
    {
        await _bptService.DeleteOtherIncomeEntryAsync(id, cancellationToken);
        return OkResponse(new Dictionary<string, string>());
    }

    [HttpGet("adjustments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BptAdjustmentDto>>>> GetAdjustments([FromQuery] BptAdjustmentListQuery query, CancellationToken cancellationToken)
    {
        var result = await _bptService.GetAdjustmentsAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("adjustments")]
    public async Task<ActionResult<ApiResponse<BptAdjustmentDto>>> CreateAdjustment([FromBody] CreateBptAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _bptService.CreateAdjustmentAsync(request, cancellationToken);
        return OkResponse(result);
    }

    [HttpPut("adjustments/{id:guid}")]
    public async Task<ActionResult<ApiResponse<BptAdjustmentDto>>> UpdateAdjustment(Guid id, [FromBody] UpdateBptAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _bptService.UpdateAdjustmentAsync(id, request, cancellationToken);
        return OkResponse(result);
    }

    [HttpDelete("adjustments/{id:guid}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, string>>>> DeleteAdjustment(Guid id, CancellationToken cancellationToken)
    {
        await _bptService.DeleteAdjustmentAsync(id, cancellationToken);
        return OkResponse(new Dictionary<string, string>());
    }
}
