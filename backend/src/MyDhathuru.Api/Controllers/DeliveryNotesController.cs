using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Dtos;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.DeliveryNotes.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/delivery-notes")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class DeliveryNotesController : BaseApiController
{
    private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;
    private const long MaxRequestBodySizeBytes = 11 * 1024 * 1024;
    private static readonly HashSet<string> AllowedAttachmentMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/gif"
    };
    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif"
    };

    private readonly IDeliveryNoteService _deliveryNoteService;

    public DeliveryNotesController(IDeliveryNoteService deliveryNoteService)
    {
        _deliveryNoteService = deliveryNoteService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DeliveryNoteListItemDto>>>> GetPaged([FromQuery] DeliveryNoteListQuery query, CancellationToken cancellationToken)
    {
        var result = await _deliveryNoteService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDetailDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deliveryNoteService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DeliveryNoteDetailDto>.Fail("Delivery note not found."));
        }

        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDetailDto>>> Create([FromBody] CreateDeliveryNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _deliveryNoteService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Delivery note created.");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDetailDto>>> Update(Guid id, [FromBody] UpdateDeliveryNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _deliveryNoteService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Delivery note updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _deliveryNoteService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Delivery note deleted.");
    }

    [HttpPost("clear-all")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> ClearAll([FromBody] ConfirmPasswordRequest request, CancellationToken cancellationToken)
    {
        await _deliveryNoteService.ClearAllAsync(request.Password, cancellationToken);
        return SuccessMessage("All delivery notes deleted.");
    }

    [HttpPost("{id:guid}/create-invoice")]
    public async Task<ActionResult<ApiResponse<CreateInvoiceFromDeliveryNoteResultDto>>> CreateInvoice(Guid id, [FromBody] CreateInvoiceFromDeliveryNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _deliveryNoteService.CreateInvoiceFromDeliveryNoteAsync(id, request, cancellationToken);
        return OkResponse(result, "Invoice created from delivery note.");
    }

    [HttpPost("{id:guid}/vessel-payment-attachment")]
    [RequestSizeLimit(MaxRequestBodySizeBytes)]
    public async Task<ActionResult<ApiResponse<DeliveryNoteAttachmentDto>>> UploadVesselPaymentInvoiceAttachment(
        Guid id,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var attachment = await BuildAttachmentAsync(file, cancellationToken);
        var result = await _deliveryNoteService.UploadVesselPaymentInvoiceAttachmentAsync(
            id,
            attachment.FileName,
            attachment.ContentType,
            attachment.Content,
            cancellationToken);
        return OkResponse(result, "Vessel payment invoice uploaded.");
    }

    [HttpPost("{id:guid}/po-attachment")]
    [RequestSizeLimit(MaxRequestBodySizeBytes)]
    public async Task<ActionResult<ApiResponse<DeliveryNoteAttachmentDto>>> UploadPoAttachment(
        Guid id,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var attachment = await BuildAttachmentAsync(file, cancellationToken, "po-attachment");
        var result = await _deliveryNoteService.UploadPoAttachmentAsync(
            id,
            attachment.FileName,
            attachment.ContentType,
            attachment.Content,
            cancellationToken);
        return OkResponse(result, "PO attachment uploaded.");
    }

    [HttpGet("{id:guid}/po-attachment")]
    public async Task<IActionResult> ViewPoAttachment(Guid id, CancellationToken cancellationToken)
    {
        var attachment = await _deliveryNoteService.GetPoAttachmentAsync(id, cancellationToken);
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    [HttpGet("{id:guid}/vessel-payment-attachment")]
    public async Task<IActionResult> ViewVesselPaymentInvoiceAttachment(Guid id, CancellationToken cancellationToken)
    {
        var attachment = await _deliveryNoteService.GetVesselPaymentInvoiceAttachmentAsync(id, cancellationToken);
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var bytes = await _deliveryNoteService.GeneratePdfAsync(id, cancellationToken);
        return File(bytes, "application/pdf", $"delivery-note-{id}.pdf");
    }

    private static async Task<DeliveryNoteAttachmentFileDto> BuildAttachmentAsync(
        IFormFile? file,
        CancellationToken cancellationToken,
        string defaultFileNamePrefix = "vessel-payment-invoice")
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
        if (string.IsNullOrWhiteSpace(extension) || !AllowedAttachmentExtensions.Contains(extension))
        {
            throw new AppException("Supported attachment formats are PDF, PNG, JPG, WEBP, and GIF.");
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedAttachmentMimeTypes.Contains(file.ContentType))
        {
            throw new AppException("Only PDF or image files are allowed.");
        }

        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        if (bytes.Length == 0)
        {
            throw new AppException("Attached file is empty.");
        }

        var safeFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = $"{defaultFileNamePrefix}{extension.ToLowerInvariant()}";
        }

        return new DeliveryNoteAttachmentFileDto
        {
            FileName = safeFileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType.Trim(),
            SizeBytes = bytes.LongLength,
            Content = bytes
        };
    }
}
