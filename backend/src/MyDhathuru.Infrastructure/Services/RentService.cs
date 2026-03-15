using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Rent.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class RentService : IRentService
{
    private readonly IBusinessAuditLogService _auditLogService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly ICurrentTenantService _currentTenantService;

    public RentService(
        ApplicationDbContext dbContext,
        IDocumentNumberService documentNumberService,
        ICurrentTenantService currentTenantService,
        IBusinessAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _documentNumberService = documentNumberService;
        _currentTenantService = currentTenantService;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<RentEntryListItemDto>> GetPagedAsync(RentEntryListQuery query, CancellationToken cancellationToken = default)
    {
        var rents = _dbContext.RentEntries
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Where(x =>
                (query.DateFrom == null || x.Date >= query.DateFrom)
                && (query.DateTo == null || x.Date <= query.DateTo)
                && (query.ExpenseCategoryId == null || x.ExpenseCategoryId == query.ExpenseCategoryId));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            rents = rents.Where(x =>
                x.RentNumber.ToLower().Contains(search)
                || x.PropertyName.ToLower().Contains(search)
                || x.PayTo.ToLower().Contains(search));
        }

        rents = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? rents.OrderBy(x => x.Date).ThenBy(x => x.RentNumber)
            : rents.OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAt);

        return await rents.Select(x => new RentEntryListItemDto
            {
                Id = x.Id,
                RentNumber = x.RentNumber,
                Date = x.Date,
                PropertyName = x.PropertyName,
                PayTo = x.PayTo,
                Currency = x.Currency,
                Amount = x.Amount,
                ExpenseCategoryId = x.ExpenseCategoryId,
                ExpenseCategoryName = x.ExpenseCategory.Name,
                ApprovalStatus = x.ApprovalStatus
            })
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<RentEntryDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RentEntries
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Where(x => x.Id == id)
            .Select(x => new RentEntryDetailDto
            {
                Id = x.Id,
                RentNumber = x.RentNumber,
                Date = x.Date,
                PropertyName = x.PropertyName,
                PayTo = x.PayTo,
                Currency = x.Currency,
                Amount = x.Amount,
                ExpenseCategoryId = x.ExpenseCategoryId,
                ExpenseCategoryName = x.ExpenseCategory.Name,
                ApprovalStatus = x.ApprovalStatus,
                Notes = x.Notes
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RentEntryDetailDto> CreateAsync(CreateRentEntryRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var category = await _dbContext.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == request.ExpenseCategoryId, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");

        var entry = new RentEntry
        {
            RentNumber = await _documentNumberService.GenerateAsync(DocumentType.RentEntry, request.Date, cancellationToken),
            Date = request.Date,
            PropertyName = request.PropertyName.Trim(),
            PayTo = request.PayTo.Trim(),
            Currency = NormalizeCurrency(request.Currency, settings.DefaultCurrency),
            Amount = Round2(request.Amount),
            ExpenseCategoryId = category.Id,
            ApprovalStatus = request.ApprovalStatus,
            Notes = request.Notes?.Trim()
        };

        _dbContext.RentEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.RentEntryCreated,
            nameof(RentEntry),
            entry.Id.ToString(),
            entry.RentNumber,
            new { entry.PayTo, entry.Amount, entry.Currency },
            cancellationToken);

        return (await GetByIdAsync(entry.Id, cancellationToken))!;
    }

    public async Task<RentEntryDetailDto> UpdateAsync(Guid id, UpdateRentEntryRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var entry = await _dbContext.RentEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Rent entry not found.");
        var category = await _dbContext.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == request.ExpenseCategoryId, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");

        entry.Date = request.Date;
        entry.PropertyName = request.PropertyName.Trim();
        entry.PayTo = request.PayTo.Trim();
        entry.Currency = NormalizeCurrency(request.Currency, settings.DefaultCurrency);
        entry.Amount = Round2(request.Amount);
        entry.ExpenseCategoryId = category.Id;
        entry.ApprovalStatus = request.ApprovalStatus;
        entry.Notes = request.Notes?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.RentEntryUpdated,
            nameof(RentEntry),
            entry.Id.ToString(),
            entry.RentNumber,
            new { entry.PayTo, entry.Amount, entry.Currency },
            cancellationToken);

        return (await GetByIdAsync(entry.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.RentEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Rent entry not found.");

        _dbContext.RentEntries.Remove(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.RentEntryDeleted,
            nameof(RentEntry),
            entry.Id.ToString(),
            entry.RentNumber,
            null,
            cancellationToken);
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private static string NormalizeCurrency(string? value, string fallback)
    {
        var currency = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
        return currency == "USD" ? "USD" : "MVR";
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
