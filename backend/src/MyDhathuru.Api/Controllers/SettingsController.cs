using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Settings.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/settings")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class SettingsController : BaseApiController
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private readonly ISettingsService _settingsService;
    private readonly IWebHostEnvironment _environment;
    private readonly ICurrentTenantService _currentTenantService;

    public SettingsController(
        ISettingsService settingsService,
        IWebHostEnvironment environment,
        ICurrentTenantService currentTenantService)
    {
        _settingsService = settingsService;
        _environment = environment;
        _currentTenantService = currentTenantService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<TenantSettingsDto>>> Get(CancellationToken cancellationToken)
    {
        var result = await _settingsService.GetAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpPut]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<TenantSettingsDto>>> Update([FromBody] UpdateTenantSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _settingsService.UpdateAsync(request, cancellationToken);
        return OkResponse(result, "Settings updated.");
    }

    [HttpPost("logo-upload")]
    [Authorize(Policy = "AdminOnly")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<ApiResponse<TenantLogoUploadDto>>> UploadLogo([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        return await UploadTenantImageAsync(file, "logo", "company-logos", "logo", cancellationToken);
    }

    [HttpPost("stamp-upload")]
    [Authorize(Policy = "AdminOnly")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<ApiResponse<TenantLogoUploadDto>>> UploadStamp([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        return await UploadTenantImageAsync(file, "company stamp", "company-stamps", "stamp", cancellationToken);
    }

    [HttpPost("signature-upload")]
    [Authorize(Policy = "AdminOnly")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<ApiResponse<TenantLogoUploadDto>>> UploadSignature([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        return await UploadTenantImageAsync(file, "company signature", "company-signatures", "signature", cancellationToken);
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _settingsService.ChangePasswordAsync(request, cancellationToken);
        return SuccessMessage("Password changed successfully.");
    }

    private async Task<ActionResult<ApiResponse<TenantLogoUploadDto>>> UploadTenantImageAsync(
        IFormFile? file,
        string assetLabel,
        string folderName,
        string filePrefix,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new AppException($"Please select a {assetLabel} image.");
        }

        if (file.Length > MaxImageSizeBytes)
        {
            throw new AppException($"{ToDisplayLabel(assetLabel)} image must be 5 MB or smaller.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension))
        {
            throw new AppException($"Supported {assetLabel} formats are PNG, JPG, and WEBP.");
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException("Only image files are allowed.");
        }

        var tenantId = _currentTenantService.TenantId ?? throw new AppException("Tenant context missing.");
        cancellationToken.ThrowIfCancellationRequested();

        var webRootPath = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;

        var assetDirectory = Path.Combine(webRootPath, "uploads", folderName, tenantId.ToString("N"));
        Directory.CreateDirectory(assetDirectory);

        var fileName = $"{filePrefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(assetDirectory, fileName);

        await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var relativePath = $"/uploads/{folderName}/{tenantId:N}/{fileName}";
        var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativePath}";

        return OkResponse(
            new TenantLogoUploadDto
            {
                Url = absoluteUrl,
                RelativePath = relativePath,
                FileName = fileName
            },
            $"{ToDisplayLabel(assetLabel)} uploaded.");
    }

    private static string ToDisplayLabel(string assetLabel)
    {
        return string.IsNullOrWhiteSpace(assetLabel)
            ? "Image"
            : char.ToUpperInvariant(assetLabel[0]) + assetLabel[1..];
    }
}
