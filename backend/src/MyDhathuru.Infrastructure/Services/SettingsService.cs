using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Settings.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
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
        var taxDisabledNow = settings.IsTaxApplicable && !request.IsTaxApplicable;

        settings.Username = request.Username?.Trim() ?? string.Empty;
        settings.CompanyName = request.CompanyName.Trim();
        settings.CompanyEmail = request.CompanyEmail.Trim();
        settings.CompanyPhone = request.CompanyPhone.Trim();
        settings.TinNumber = request.TinNumber.Trim();
        settings.BusinessRegistrationNumber = request.BusinessRegistrationNumber.Trim();
        settings.InvoicePrefix = request.InvoicePrefix.Trim();
        settings.DeliveryNotePrefix = request.DeliveryNotePrefix.Trim();
        settings.QuotePrefix = request.QuotePrefix.Trim();
        settings.PurchaseOrderPrefix = request.PurchaseOrderPrefix.Trim();
        settings.ReceivedInvoicePrefix = request.ReceivedInvoicePrefix.Trim();
        settings.PaymentVoucherPrefix = request.PaymentVoucherPrefix.Trim();
        settings.RentEntryPrefix = request.RentEntryPrefix.Trim();
        settings.WarningFormPrefix = request.WarningFormPrefix.Trim();
        settings.StatementPrefix = request.StatementPrefix.Trim();
        settings.SalarySlipPrefix = request.SalarySlipPrefix.Trim();
        settings.IsTaxApplicable = request.IsTaxApplicable;
        settings.DefaultTaxRate = request.IsTaxApplicable ? request.DefaultTaxRate : 0m;
        settings.DefaultDueDays = request.DefaultDueDays;
        settings.DefaultCurrency = request.DefaultCurrency.Trim().ToUpperInvariant();
        settings.TaxableActivityNumber = request.TaxableActivityNumber?.Trim() ?? string.Empty;
        settings.IsInputTaxClaimEnabled = request.IsInputTaxClaimEnabled;
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

        if (taxDisabledNow)
        {
            await RemoveTenantDocumentTaxAsync(tenantId, cancellationToken);
        }

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
            QuotePrefix = settings.QuotePrefix,
            PurchaseOrderPrefix = settings.PurchaseOrderPrefix,
            ReceivedInvoicePrefix = settings.ReceivedInvoicePrefix,
            PaymentVoucherPrefix = settings.PaymentVoucherPrefix,
            RentEntryPrefix = settings.RentEntryPrefix,
            WarningFormPrefix = settings.WarningFormPrefix,
            StatementPrefix = settings.StatementPrefix,
            SalarySlipPrefix = settings.SalarySlipPrefix,
            IsTaxApplicable = settings.IsTaxApplicable,
            DefaultTaxRate = settings.DefaultTaxRate,
            DefaultDueDays = settings.DefaultDueDays,
            DefaultCurrency = settings.DefaultCurrency,
            TaxableActivityNumber = settings.TaxableActivityNumber,
            IsInputTaxClaimEnabled = settings.IsInputTaxClaimEnabled,
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

    private async Task RemoveTenantDocumentTaxAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var invoices = await _dbContext.Invoices
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Payments)
            .ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            invoice.TaxRate = 0m;
            invoice.TaxAmount = 0m;
            invoice.GrandTotal = Round2(invoice.Subtotal);
            invoice.AmountPaid = Round2(invoice.Payments.Sum(x => x.Amount));
            invoice.Balance = Round2(invoice.GrandTotal - invoice.AmountPaid);

            if (invoice.Balance <= 0m)
            {
                invoice.Balance = 0m;
                invoice.PaymentStatus = PaymentStatus.Paid;
                continue;
            }

            invoice.PaymentStatus = invoice.AmountPaid > 0m
                ? PaymentStatus.Partial
                : PaymentStatus.Unpaid;
        }

        var quotations = await _dbContext.Quotations
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        foreach (var quotation in quotations)
        {
            quotation.TaxRate = 0m;
            quotation.TaxAmount = 0m;
            quotation.GrandTotal = Round2(quotation.Subtotal);
        }

        var purchaseOrders = await _dbContext.PurchaseOrders
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        foreach (var purchaseOrder in purchaseOrders)
        {
            purchaseOrder.TaxRate = 0m;
            purchaseOrder.TaxAmount = 0m;
            purchaseOrder.GrandTotal = Round2(purchaseOrder.Subtotal);
        }
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
