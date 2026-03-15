using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Mira.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/mira")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class MiraController : BaseApiController
{
    private readonly IMiraService _miraService;

    public MiraController(IMiraService miraService)
    {
        _miraService = miraService;
    }

    [HttpGet("preview")]
    public async Task<ActionResult<ApiResponse<MiraReportPreviewDto>>> GetPreview([FromQuery] MiraReportQuery query, CancellationToken cancellationToken)
    {
        var result = await _miraService.GetPreviewAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("export/excel")]
    public async Task<IActionResult> ExportExcel([FromBody] MiraReportExportRequest request, CancellationToken cancellationToken)
    {
        var export = await _miraService.ExportExcelAsync(request, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpPost("export/pdf")]
    public async Task<IActionResult> ExportPdf([FromBody] MiraReportExportRequest request, CancellationToken cancellationToken)
    {
        var export = await _miraService.ExportPdfAsync(request, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }
}
