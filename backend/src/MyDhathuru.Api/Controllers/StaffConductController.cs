using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.StaffConduct.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/staff-conduct")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class StaffConductController : BaseApiController
{
    private readonly IStaffConductService _staffConductService;

    public StaffConductController(IStaffConductService staffConductService)
    {
        _staffConductService = staffConductService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<StaffConductListItemDto>>>> GetPaged([FromQuery] StaffConductListQuery query, CancellationToken cancellationToken)
    {
        var result = await _staffConductService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<StaffConductSummaryDto>>> GetSummary([FromQuery] StaffConductListQuery query, CancellationToken cancellationToken)
    {
        var result = await _staffConductService.GetSummaryAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("staff-options")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StaffConductStaffOptionDto>>>> GetStaffOptions(CancellationToken cancellationToken)
    {
        var result = await _staffConductService.GetStaffOptionsAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<StaffConductDetailDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _staffConductService.GetByIdAsync(id, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<StaffConductDetailDto>>> Create([FromBody] CreateStaffConductFormRequest request, CancellationToken cancellationToken)
    {
        var result = await _staffConductService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Form created.");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<StaffConductDetailDto>>> Update(Guid id, [FromBody] UpdateStaffConductFormRequest request, CancellationToken cancellationToken)
    {
        var result = await _staffConductService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Form updated.");
    }

    [HttpGet("{id:guid}/export/pdf")]
    public async Task<IActionResult> ExportPdf(Guid id, CancellationToken cancellationToken)
    {
        var bytes = await _staffConductService.ExportPdfAsync(id, cancellationToken);
        return File(bytes, "application/pdf", $"staff-conduct-{id}.pdf");
    }

    [HttpGet("{id:guid}/export/excel")]
    public async Task<IActionResult> ExportExcel(Guid id, CancellationToken cancellationToken)
    {
        var bytes = await _staffConductService.ExportExcelAsync(id, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"staff-conduct-{id}.xlsx");
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportSummaryPdf([FromQuery] StaffConductListQuery query, CancellationToken cancellationToken)
    {
        var bytes = await _staffConductService.ExportSummaryPdfAsync(query, cancellationToken);
        return File(bytes, "application/pdf", "staff-conduct-summary.pdf");
    }

    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportSummaryExcel([FromQuery] StaffConductListQuery query, CancellationToken cancellationToken)
    {
        var bytes = await _staffConductService.ExportSummaryExcelAsync(query, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "staff-conduct-summary.xlsx");
    }
}
