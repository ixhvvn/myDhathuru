using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Suppliers.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class SupplierService : ISupplierService
{
    private readonly IBusinessAuditLogService _auditLogService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;

    public SupplierService(
        ApplicationDbContext dbContext,
        ICurrentTenantService currentTenantService,
        IBusinessAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<SupplierDto>> GetPagedAsync(SupplierListQuery query, CancellationToken cancellationToken = default)
    {
        var suppliers = BuildQuery(query).Select(x => new SupplierDto
        {
            Id = x.Id,
            Name = x.Name,
            TinNumber = x.TinNumber,
            ContactNumber = x.ContactNumber,
            Email = x.Email,
            Address = x.Address,
            Notes = x.Notes,
            IsActive = x.IsActive,
            ReceivedInvoiceCount = x.ReceivedInvoices.Count,
            OutstandingAmount = x.ReceivedInvoices.Sum(invoice => invoice.BalanceDue)
        });

        return await suppliers.ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierLookupDto>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Suppliers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SupplierLookupDto
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Suppliers
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new SupplierDto
            {
                Id = x.Id,
                Name = x.Name,
                TinNumber = x.TinNumber,
                ContactNumber = x.ContactNumber,
                Email = x.Email,
                Address = x.Address,
                Notes = x.Notes,
                IsActive = x.IsActive,
                ReceivedInvoiceCount = x.ReceivedInvoices.Count,
                OutstandingAmount = x.ReceivedInvoices.Sum(invoice => invoice.BalanceDue)
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        var name = request.Name.Trim();

        var existing = await _dbContext.Suppliers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == name, cancellationToken);

        Supplier supplier;
        if (existing is not null)
        {
            if (!existing.IsDeleted)
            {
                throw new AppException("Supplier name already exists.");
            }

            existing.IsDeleted = false;
            supplier = Apply(existing, request, name);
        }
        else
        {
            supplier = Apply(new Supplier { TenantId = tenantId, Name = name }, request, name);
            _dbContext.Suppliers.Add(supplier);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync(
            BusinessAuditActionType.SupplierCreated,
            nameof(Supplier),
            supplier.Id.ToString(),
            supplier.Name,
            new { supplier.TinNumber, supplier.ContactNumber, supplier.Email, supplier.IsActive },
            cancellationToken);

        return (await GetByIdAsync(supplier.Id, cancellationToken))!;
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Supplier not found.");

        var name = request.Name.Trim();
        var exists = await _dbContext.Suppliers
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Id != id && x.TenantId == supplier.TenantId && x.Name == name && !x.IsDeleted, cancellationToken);

        if (exists)
        {
            throw new AppException("Supplier name already exists.");
        }

        Apply(supplier, request, name);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync(
            BusinessAuditActionType.SupplierUpdated,
            nameof(Supplier),
            supplier.Id.ToString(),
            supplier.Name,
            new { supplier.TinNumber, supplier.ContactNumber, supplier.Email, supplier.IsActive },
            cancellationToken);

        return (await GetByIdAsync(supplier.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Supplier not found.");

        var hasInvoices = await _dbContext.ReceivedInvoices.AnyAsync(x => x.SupplierId == id, cancellationToken);
        if (hasInvoices)
        {
            throw new AppException("Cannot delete a supplier with received invoices.");
        }

        _dbContext.Suppliers.Remove(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync(
            BusinessAuditActionType.SupplierDeleted,
            nameof(Supplier),
            supplier.Id.ToString(),
            supplier.Name,
            null,
            cancellationToken);
    }

    private IQueryable<Supplier> BuildQuery(SupplierListQuery query)
    {
        var suppliers = _dbContext.Suppliers.AsNoTracking();

        if (query.IsActive.HasValue)
        {
            suppliers = suppliers.Where(x => x.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            suppliers = suppliers.Where(x =>
                x.Name.ToLower().Contains(search)
                || (x.TinNumber != null && x.TinNumber.ToLower().Contains(search))
                || (x.ContactNumber != null && x.ContactNumber.ToLower().Contains(search))
                || (x.Email != null && x.Email.ToLower().Contains(search)));
        }

        return query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? suppliers.OrderBy(x => x.Name)
            : suppliers.OrderByDescending(x => x.CreatedAt);
    }

    private static Supplier Apply(Supplier supplier, CreateSupplierRequest request, string name)
    {
        supplier.Name = name;
        supplier.TinNumber = request.TinNumber?.Trim();
        supplier.ContactNumber = request.ContactNumber?.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.Address = request.Address?.Trim();
        supplier.Notes = request.Notes?.Trim();
        supplier.IsActive = request.IsActive;
        return supplier;
    }
}
