using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Statements.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/account-statements")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class AccountStatementsController : BaseApiController
{
    private readonly IStatementService _statementService;

    public AccountStatementsController(IStatementService statementService)
    {
        _statementService = statementService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<AccountStatementDto>>> GetStatement([FromQuery] Guid customerId, [FromQuery] int year, CancellationToken cancellationToken)
    {
        var result = await _statementService.GetStatementAsync(customerId, year, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("opening-balance")]
    public async Task<ActionResult<ApiResponse<object>>> SaveOpeningBalance([FromBody] SaveOpeningBalanceRequest request, CancellationToken cancellationToken)
    {
        await _statementService.SaveOpeningBalanceAsync(request, cancellationToken);
        return SuccessMessage("Opening balance saved.");
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] Guid customerId, [FromQuery] int year, CancellationToken cancellationToken)
    {
        var bytes = await _statementService.GeneratePdfAsync(customerId, year, cancellationToken);
        return File(bytes, "application/pdf", $"statement-{customerId}-{year}.pdf");
    }
}
