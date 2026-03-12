using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Reports.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/reports")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class ReportsController : BaseApiController
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("sales-summary")]
    public async Task<ActionResult<ApiResponse<SalesSummaryReportDto>>> GetSalesSummary(
        [FromQuery] ReportFilterQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetSalesSummaryAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("sales-transactions")]
    public async Task<ActionResult<ApiResponse<SalesTransactionsReportDto>>> GetSalesTransactions(
        [FromQuery] ReportFilterQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetSalesTransactionsAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("sales-by-vessel")]
    public async Task<ActionResult<ApiResponse<SalesByVesselReportDto>>> GetSalesByVessel(
        [FromQuery] ReportFilterQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetSalesByVesselAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("export/excel")]
    public async Task<IActionResult> ExportExcel(
        [FromBody] ReportExportRequest request,
        CancellationToken cancellationToken)
    {
        var export = await _reportService.ExportExcelAsync(request, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpPost("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromBody] ReportExportRequest request,
        CancellationToken cancellationToken)
    {
        var export = await _reportService.ExportPdfAsync(request, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }
}
