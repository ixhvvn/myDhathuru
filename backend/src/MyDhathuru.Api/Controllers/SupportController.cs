using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Support.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/support")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class SupportController : BaseApiController
{
    private const long MaxAttachmentSizeBytes = 4 * 1024 * 1024;
    private const long MaxRequestBodySizeBytes = 6 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/gif"
    };
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif"
    };

    private readonly ISupportService _supportService;

    public SupportController(ISupportService supportService)
    {
        _supportService = supportService;
    }

    [HttpPost("report-bug")]
    [RequestSizeLimit(MaxRequestBodySizeBytes)]
    public async Task<ActionResult<ApiResponse<object>>> ReportBug(
        [FromForm] ReportBugRequest request,
        [FromForm] IFormFile? attachment,
        CancellationToken cancellationToken)
    {
        var bugAttachment = await BuildAttachmentAsync(attachment, cancellationToken);
        await _supportService.ReportBugAsync(request, bugAttachment, cancellationToken);
        return SuccessMessage("Bug report sent successfully.");
    }

    private static async Task<BugReportAttachment?> BuildAttachmentAsync(IFormFile? attachment, CancellationToken cancellationToken)
    {
        if (attachment is null)
        {
            return null;
        }

        if (attachment.Length == 0)
        {
            throw new AppException("Attached image is empty.");
        }

        if (attachment.Length > MaxAttachmentSizeBytes)
        {
            throw new AppException("Attached image must be 4 MB or smaller.");
        }

        var extension = Path.GetExtension(attachment.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension))
        {
            throw new AppException("Supported image formats are PNG, JPG, WEBP, and GIF.");
        }

        if (!string.IsNullOrWhiteSpace(attachment.ContentType) && !AllowedImageMimeTypes.Contains(attachment.ContentType))
        {
            throw new AppException("Only image files are allowed.");
        }

        await using var memoryStream = new MemoryStream();
        await attachment.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        if (bytes.Length == 0)
        {
            throw new AppException("Attached image is empty.");
        }

        var safeFileName = Path.GetFileName(attachment.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = $"bug-image{extension.ToLowerInvariant()}";
        }

        return new BugReportAttachment
        {
            FileName = safeFileName,
            ContentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType.Trim(),
            Content = bytes
        };
    }
}
