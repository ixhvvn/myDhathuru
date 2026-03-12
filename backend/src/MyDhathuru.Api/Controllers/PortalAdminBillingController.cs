using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/portal-admin/billing")]
[Authorize(Policy = "SuperAdminOnly")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class PortalAdminBillingController : BaseApiController
{
    private const long MaxLogoSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedLogoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private readonly IPortalAdminBillingService _billingService;
    private readonly IWebHostEnvironment _environment;

    public PortalAdminBillingController(IPortalAdminBillingService billingService, IWebHostEnvironment environment)
    {
        _billingService = billingService;
        _environment = environment;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<PortalAdminBillingDashboardDto>>> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await _billingService.GetDashboardAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("settings")]
    public async Task<ActionResult<ApiResponse<PortalAdminBillingSettingsDto>>> GetSettings(CancellationToken cancellationToken)
    {
        var result = await _billingService.GetSettingsAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpPut("settings")]
    public async Task<ActionResult<ApiResponse<PortalAdminBillingSettingsDto>>> UpdateSettings([FromBody] UpdatePortalAdminBillingSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _billingService.UpdateSettingsAsync(request, cancellationToken);
        return OkResponse(result, "Billing settings updated.");
    }

    [HttpPost("logo-upload")]
    [RequestSizeLimit(MaxLogoSizeBytes)]
    public async Task<ActionResult<ApiResponse<PortalAdminBillingLogoUploadDto>>> UploadLogo([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new AppException("Please select a logo file.");
        }

        if (file.Length > MaxLogoSizeBytes)
        {
            throw new AppException("Logo file must be 5 MB or smaller.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedLogoExtensions.Contains(extension))
        {
            throw new AppException("Supported logo formats are PNG, JPG, and WEBP.");
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException("Only image files are allowed.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var webRootPath = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;

        var logoDirectory = Path.Combine(webRootPath, "uploads", "logos");
        Directory.CreateDirectory(logoDirectory);

        var fileName = $"logo-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(logoDirectory, fileName);

        await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var relativePath = $"/uploads/logos/{fileName}";
        var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativePath}";

        return OkResponse(
            new PortalAdminBillingLogoUploadDto
            {
                Url = absoluteUrl,
                RelativePath = relativePath,
                FileName = fileName
            },
            "Logo uploaded.");
    }

    [HttpGet("business-options")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PortalAdminBillingBusinessOptionDto>>>> GetBusinessOptions(CancellationToken cancellationToken)
    {
        var result = await _billingService.GetBusinessOptionsAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("statements/yearly")]
    public async Task<ActionResult<ApiResponse<PortalAdminBillingYearlyStatementDto>>> GetYearlyStatement([FromQuery] Guid tenantId, [FromQuery] int year, CancellationToken cancellationToken)
    {
        var result = await _billingService.GetYearlyStatementAsync(tenantId, year, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<ApiResponse<PagedResult<PortalAdminBillingInvoiceListItemDto>>>> GetInvoices([FromQuery] PortalAdminBillingInvoiceListQuery query, CancellationToken cancellationToken)
    {
        var result = await _billingService.GetInvoicesAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpDelete("invoices")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAllInvoices(CancellationToken cancellationToken)
    {
        var deletedCount = await _billingService.DeleteAllInvoicesAsync(cancellationToken);
        return OkResponse<object>(new { deletedCount }, $"Deleted {deletedCount} invoice(s).");
    }

    [HttpGet("invoices/{invoiceId:guid}")]
    public async Task<ActionResult<ApiResponse<PortalAdminBillingInvoiceDetailDto>>> GetInvoiceById(Guid invoiceId, CancellationToken cancellationToken)
    {
        var result = await _billingService.GetInvoiceByIdAsync(invoiceId, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("invoices/generate")]
    public async Task<ActionResult<ApiResponse<PortalAdminBillingGenerationResultDto>>> GenerateInvoice([FromBody] PortalAdminBillingGenerateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await _billingService.GenerateInvoiceAsync(request, cancellationToken);
        return OkResponse(result, result.PreviewOnly ? "Invoice preview generated." : "Invoice generated.");
    }

    [HttpPost("invoices/generate-bulk")]
    public async Task<ActionResult<ApiResponse<PortalAdminBillingGenerationResultDto>>> GenerateBulkInvoices([FromBody] PortalAdminBillingGenerateBulkInvoicesRequest request, CancellationToken cancellationToken)
    {
        var result = await _billingService.GenerateBulkInvoicesAsync(request, cancellationToken);
        return OkResponse(result, result.PreviewOnly ? "Bulk invoice preview generated." : "Bulk invoices generated.");
    }

    [HttpPost("invoices/custom")]
    public async Task<ActionResult<ApiResponse<PortalAdminBillingGenerationResultDto>>> CreateCustomInvoices([FromBody] PortalAdminBillingCustomInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await _billingService.CreateCustomInvoicesAsync(request, cancellationToken);
        return OkResponse(result, result.PreviewOnly ? "Custom invoice preview generated." : "Custom invoices generated.");
    }

    [HttpPost("invoices/{invoiceId:guid}/send-email")]
    public async Task<ActionResult<ApiResponse<object>>> SendInvoiceEmail(Guid invoiceId, [FromBody] PortalAdminBillingSendInvoiceEmailRequest request, CancellationToken cancellationToken)
    {
        await _billingService.SendInvoiceEmailAsync(invoiceId, request, cancellationToken);
        return SuccessMessage("Invoice email sent.");
    }

    [HttpGet("invoices/{invoiceId:guid}/pdf")]
    public async Task<IActionResult> GetInvoicePdf(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await _billingService.GetInvoiceByIdAsync(invoiceId, cancellationToken);
        var bytes = await _billingService.GetInvoicePdfAsync(invoiceId, cancellationToken);
        return File(bytes, "application/pdf", $"admin-invoice-{invoice.InvoiceNumber}.pdf");
    }

    [HttpGet("custom-rates")]
    public async Task<ActionResult<ApiResponse<PagedResult<PortalAdminBillingBusinessCustomRateDto>>>> GetCustomRates([FromQuery] PortalAdminBillingCustomRateQuery query, CancellationToken cancellationToken)
    {
        var result = await _billingService.GetCustomRatesAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("custom-rates")]
    public async Task<ActionResult<ApiResponse<PortalAdminBillingBusinessCustomRateDto>>> CreateCustomRate([FromBody] PortalAdminBillingUpsertCustomRateRequest request, CancellationToken cancellationToken)
    {
        var result = await _billingService.CreateCustomRateAsync(request, cancellationToken);
        return OkResponse(result, "Custom rate created.");
    }

    [HttpPut("custom-rates/{rateId:guid}")]
    public async Task<ActionResult<ApiResponse<PortalAdminBillingBusinessCustomRateDto>>> UpdateCustomRate(Guid rateId, [FromBody] PortalAdminBillingUpsertCustomRateRequest request, CancellationToken cancellationToken)
    {
        var result = await _billingService.UpdateCustomRateAsync(rateId, request, cancellationToken);
        return OkResponse(result, "Custom rate updated.");
    }

    [HttpDelete("custom-rates/{rateId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCustomRate(Guid rateId, CancellationToken cancellationToken)
    {
        await _billingService.DeleteCustomRateAsync(rateId, cancellationToken);
        return SuccessMessage("Custom rate removed.");
    }
}
