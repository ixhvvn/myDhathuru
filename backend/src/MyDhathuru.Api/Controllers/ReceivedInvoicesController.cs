using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.ReceivedInvoices.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/received-invoices")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class ReceivedInvoicesController : BaseApiController
{
    private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;
    private const long MaxRequestBodySizeBytes = 11 * 1024 * 1024;
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/gif"
    };
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif"
    };

    private readonly IReceivedInvoiceService _receivedInvoiceService;

    public ReceivedInvoicesController(IReceivedInvoiceService receivedInvoiceService)
    {
        _receivedInvoiceService = receivedInvoiceService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ReceivedInvoiceListItemDto>>>> GetPaged([FromQuery] ReceivedInvoiceListQuery query, CancellationToken cancellationToken)
    {
        var result = await _receivedInvoiceService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReceivedInvoiceDetailDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _receivedInvoiceService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ReceivedInvoiceDetailDto>.Fail("Received invoice not found."));
        }

        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReceivedInvoiceDetailDto>>> Create([FromBody] CreateReceivedInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await _receivedInvoiceService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Received invoice created.");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReceivedInvoiceDetailDto>>> Update(Guid id, [FromBody] UpdateReceivedInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await _receivedInvoiceService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Received invoice updated.");
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<ApiResponse<ReceivedInvoicePaymentDto>>> RecordPayment(Guid id, [FromBody] RecordReceivedInvoicePaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _receivedInvoiceService.RecordPaymentAsync(id, request, cancellationToken);
        return OkResponse(result, "Supplier payment recorded.");
    }

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(MaxRequestBodySizeBytes)]
    public async Task<ActionResult<ApiResponse<ReceivedInvoiceAttachmentDto>>> UploadAttachment(Guid id, [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        var attachment = await BuildAttachmentAsync(file, cancellationToken);
        var result = await _receivedInvoiceService.UploadAttachmentAsync(id, attachment.FileName, attachment.ContentType, attachment.Content, cancellationToken);
        return OkResponse(result, "Attachment uploaded.");
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> ViewAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _receivedInvoiceService.GetAttachmentAsync(id, attachmentId, cancellationToken);
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _receivedInvoiceService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Received invoice deleted.");
    }

    private static async Task<ReceivedInvoiceAttachmentFileDto> BuildAttachmentAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw new AppException("Please select an attachment file.");
        }

        if (file.Length == 0)
        {
            throw new AppException("Attached file is empty.");
        }

        if (file.Length > MaxAttachmentSizeBytes)
        {
            throw new AppException("Attached file must be 10 MB or smaller.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new AppException("Supported attachment formats are PDF, PNG, JPG, WEBP, and GIF.");
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedMimeTypes.Contains(file.ContentType))
        {
            throw new AppException("Only PDF or image files are allowed.");
        }

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var bytes = stream.ToArray();

        return new ReceivedInvoiceAttachmentFileDto
        {
            FileName = Path.GetFileName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType.Trim(),
            SizeBytes = bytes.LongLength,
            Content = bytes
        };
    }
}
