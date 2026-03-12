using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Settings.Dtos;
using MyDhathuru.Infrastructure.Persistence;
using MyDhathuru.Infrastructure.Security;

namespace MyDhathuru.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private const string DefaultInvoiceLogoUrl = "/logo-name.svg";
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;

    public SettingsService(
        ApplicationDbContext dbContext,
        ICurrentTenantService currentTenantService,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
    }

    public async Task<TenantSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");
        var settings = await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Settings not found.");

        return Map(settings);
    }

    public async Task<TenantSettingsDto> UpdateAsync(UpdateTenantSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");
        var settings = await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Settings not found.");

        settings.Username = request.Username?.Trim() ?? string.Empty;
        settings.CompanyName = request.CompanyName.Trim();
        settings.CompanyEmail = request.CompanyEmail.Trim();
        settings.CompanyPhone = request.CompanyPhone.Trim();
        settings.TinNumber = request.TinNumber.Trim();
        settings.BusinessRegistrationNumber = request.BusinessRegistrationNumber.Trim();
        settings.InvoicePrefix = request.InvoicePrefix.Trim();
        settings.DeliveryNotePrefix = request.DeliveryNotePrefix.Trim();
        settings.DefaultTaxRate = request.DefaultTaxRate;
        settings.DefaultDueDays = request.DefaultDueDays;
        settings.DefaultCurrency = request.DefaultCurrency.Trim().ToUpperInvariant();
        settings.BmlMvrAccountName = request.BmlMvrAccountName?.Trim() ?? string.Empty;
        settings.BmlMvrAccountNumber = request.BmlMvrAccountNumber?.Trim() ?? string.Empty;
        settings.BmlUsdAccountName = request.BmlUsdAccountName?.Trim() ?? string.Empty;
        settings.BmlUsdAccountNumber = request.BmlUsdAccountNumber?.Trim() ?? string.Empty;
        settings.MibMvrAccountName = request.MibMvrAccountName?.Trim() ?? string.Empty;
        settings.MibMvrAccountNumber = request.MibMvrAccountNumber?.Trim() ?? string.Empty;
        settings.MibUsdAccountName = request.MibUsdAccountName?.Trim() ?? string.Empty;
        settings.MibUsdAccountNumber = request.MibUsdAccountNumber?.Trim() ?? string.Empty;
        settings.InvoiceOwnerName = request.InvoiceOwnerName?.Trim() ?? string.Empty;
        settings.InvoiceOwnerIdCard = request.InvoiceOwnerIdCard?.Trim() ?? string.Empty;
        settings.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? DefaultInvoiceLogoUrl : request.LogoUrl.Trim();

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        tenant.CompanyName = settings.CompanyName;
        tenant.CompanyEmail = settings.CompanyEmail;
        tenant.CompanyPhone = settings.CompanyPhone;
        tenant.TinNumber = settings.TinNumber;
        tenant.BusinessRegistrationNumber = settings.BusinessRegistrationNumber;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(settings);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.GetContext().UserId ?? throw new UnauthorizedException("Unauthorized.");

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        var validCurrent = _passwordHasher.Verify(request.CurrentPassword, user.PasswordHash, user.PasswordSalt);
        if (!validCurrent)
        {
            throw new AppException("Current password is incorrect.");
        }

        var (hash, salt) = _passwordHasher.HashPassword(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TenantSettingsDto Map(Domain.Entities.TenantSettings settings)
    {
        return new TenantSettingsDto
        {
            Username = settings.Username,
            CompanyName = settings.CompanyName,
            CompanyEmail = settings.CompanyEmail,
            CompanyPhone = settings.CompanyPhone,
            TinNumber = settings.TinNumber,
            BusinessRegistrationNumber = settings.BusinessRegistrationNumber,
            InvoicePrefix = settings.InvoicePrefix,
            DeliveryNotePrefix = settings.DeliveryNotePrefix,
            DefaultTaxRate = settings.DefaultTaxRate,
            DefaultDueDays = settings.DefaultDueDays,
            DefaultCurrency = settings.DefaultCurrency,
            BmlMvrAccountName = settings.BmlMvrAccountName,
            BmlMvrAccountNumber = settings.BmlMvrAccountNumber,
            BmlUsdAccountName = settings.BmlUsdAccountName,
            BmlUsdAccountNumber = settings.BmlUsdAccountNumber,
            MibMvrAccountName = settings.MibMvrAccountName,
            MibMvrAccountNumber = settings.MibMvrAccountNumber,
            MibUsdAccountName = settings.MibUsdAccountName,
            MibUsdAccountNumber = settings.MibUsdAccountNumber,
            InvoiceOwnerName = settings.InvoiceOwnerName,
            InvoiceOwnerIdCard = settings.InvoiceOwnerIdCard,
            LogoUrl = string.IsNullOrWhiteSpace(settings.LogoUrl) ? DefaultInvoiceLogoUrl : settings.LogoUrl
        };
    }
}
