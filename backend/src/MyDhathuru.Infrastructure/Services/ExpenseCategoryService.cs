using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Expenses.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly IBusinessAuditLogService _auditLogService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;

    public ExpenseCategoryService(
        ApplicationDbContext dbContext,
        ICurrentTenantService currentTenantService,
        IBusinessAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<ExpenseCategoryDto>> GetPagedAsync(ExpenseCategoryListQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);

        var categories = BuildQuery(query).Select(x => new ExpenseCategoryDto
        {
            Id = x.Id,
            Name = x.Name,
            Code = x.Code,
            Description = x.Description,
            BptCategoryCode = x.BptCategoryCode,
            IsActive = x.IsActive,
            IsSystem = x.IsSystem,
            SortOrder = x.SortOrder,
            UsageCount = x.ReceivedInvoices.Count + x.ExpenseEntries.Count + x.RentEntries.Count
        });

        return await categories.ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseCategoryLookupDto>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);

        return await _dbContext.ExpenseCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new ExpenseCategoryLookupDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                BptCategoryCode = x.BptCategoryCode
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ExpenseCategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);

        return await _dbContext.ExpenseCategories
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ExpenseCategoryDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Description = x.Description,
                BptCategoryCode = x.BptCategoryCode,
                IsActive = x.IsActive,
                IsSystem = x.IsSystem,
                SortOrder = x.SortOrder,
                UsageCount = x.ReceivedInvoices.Count + x.ExpenseEntries.Count + x.RentEntries.Count
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ExpenseCategoryDto> CreateAsync(CreateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);

        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureUniqueAsync(tenantId, null, name, code, cancellationToken);

        var category = new ExpenseCategory
        {
            TenantId = tenantId,
            Name = name,
            Code = code,
            Description = request.Description?.Trim(),
            BptCategoryCode = request.BptCategoryCode,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };

        _dbContext.ExpenseCategories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync(
            BusinessAuditActionType.ExpenseCategoryCreated,
            nameof(ExpenseCategory),
            category.Id.ToString(),
            category.Name,
            new { category.Code, category.BptCategoryCode, category.IsActive },
            cancellationToken);

        return (await GetByIdAsync(category.Id, cancellationToken))!;
    }

    public async Task<ExpenseCategoryDto> UpdateAsync(Guid id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);

        var category = await _dbContext.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");

        await EnsureUniqueAsync(category.TenantId, id, request.Name.Trim(), request.Code.Trim().ToUpperInvariant(), cancellationToken);

        category.Name = request.Name.Trim();
        category.Code = request.Code.Trim().ToUpperInvariant();
        category.Description = request.Description?.Trim();
        category.BptCategoryCode = request.BptCategoryCode;
        category.IsActive = request.IsActive;
        category.SortOrder = request.SortOrder;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync(
            BusinessAuditActionType.ExpenseCategoryUpdated,
            nameof(ExpenseCategory),
            category.Id.ToString(),
            category.Name,
            new { category.Code, category.BptCategoryCode, category.IsActive },
            cancellationToken);

        return (await GetByIdAsync(category.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);

        var category = await _dbContext.ExpenseCategories
            .Include(x => x.ReceivedInvoices)
            .Include(x => x.ExpenseEntries)
            .Include(x => x.RentEntries)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");

        if (category.IsSystem)
        {
            throw new AppException("System expense categories cannot be deleted.");
        }

        if (category.ReceivedInvoices.Any() || category.ExpenseEntries.Any() || category.RentEntries.Any())
        {
            throw new AppException("Cannot delete an expense category that is already in use.");
        }

        _dbContext.ExpenseCategories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync(
            BusinessAuditActionType.ExpenseCategoryDeleted,
            nameof(ExpenseCategory),
            category.Id.ToString(),
            category.Name,
            null,
            cancellationToken);
    }

    private IQueryable<ExpenseCategory> BuildQuery(ExpenseCategoryListQuery query)
    {
        var categories = _dbContext.ExpenseCategories.AsNoTracking();

        if (query.IsActive.HasValue)
        {
            categories = categories.Where(x => x.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            categories = categories.Where(x =>
                x.Name.ToLower().Contains(search)
                || x.Code.ToLower().Contains(search)
                || (x.Description != null && x.Description.ToLower().Contains(search)));
        }

        return query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? categories.OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            : categories.OrderByDescending(x => x.IsSystem).ThenBy(x => x.SortOrder).ThenBy(x => x.Name);
    }

    private async Task EnsureUniqueAsync(Guid tenantId, Guid? currentId, string name, string code, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.ExpenseCategories
            .IgnoreQueryFilters()
            .AnyAsync(x =>
                x.TenantId == tenantId
                && !x.IsDeleted
                && x.Id != currentId
                && (x.Name == name || x.Code == code), cancellationToken);

        if (exists)
        {
            throw new AppException("Expense category name or code already exists.");
        }
    }

    private async Task EnsureDefaultCategoriesAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        var hasAny = await _dbContext.ExpenseCategories.AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (hasAny)
        {
            return;
        }

        _dbContext.ExpenseCategories.AddRange(BuildDefaultCategories(tenantId));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public static IReadOnlyList<ExpenseCategory> BuildDefaultCategories(Guid tenantId)
    {
        return new List<ExpenseCategory>
        {
            CreateDefault(tenantId, "Salary", "SAL", BptCategoryCode.Salary, 10),
            CreateDefault(tenantId, "Rent", "RNT", BptCategoryCode.Rent, 20),
            CreateDefault(tenantId, "License and Registration Fees", "LIC", BptCategoryCode.LicenseAndRegistrationFees, 30),
            CreateDefault(tenantId, "Office Expense", "OFF", BptCategoryCode.OfficeExpense, 40),
            CreateDefault(tenantId, "Repair and Maintenance", "RPM", BptCategoryCode.RepairAndMaintenance, 50),
            CreateDefault(tenantId, "Diesel / Fuel", "DSL", BptCategoryCode.DieselCharges, 60),
            CreateDefault(tenantId, "Hired Ferry Charges", "FRY", BptCategoryCode.HiredFerryCharges, 70),
            CreateDefault(tenantId, "Utilities / Telephone / Internet", "UTL", BptCategoryCode.UtilitiesTelephoneInternet, 80),
            CreateDefault(tenantId, "Office Supplies", "SUP", BptCategoryCode.OfficeSupplies, 90),
            CreateDefault(tenantId, "Insurance", "INS", BptCategoryCode.Insurance, 100),
            CreateDefault(tenantId, "Legal and Professional Fees", "LEG", BptCategoryCode.LegalAndProfessionalFees, 110),
            CreateDefault(tenantId, "Travel", "TRV", BptCategoryCode.Travel, 120),
            CreateDefault(tenantId, "Other", "OTH", BptCategoryCode.Other, 999)
        };
    }

    private static ExpenseCategory CreateDefault(Guid tenantId, string name, string code, BptCategoryCode bptCategoryCode, int sortOrder)
    {
        return new ExpenseCategory
        {
            TenantId = tenantId,
            Name = name,
            Code = code,
            BptCategoryCode = bptCategoryCode,
            IsActive = true,
            IsSystem = true,
            SortOrder = sortOrder
        };
    }
}
