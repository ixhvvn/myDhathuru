using Microsoft.EntityFrameworkCore;
using Npgsql;
using ClosedXML.Excel;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Customers.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPdfExportService _pdfExportService;
    private readonly ICurrentTenantService _currentTenantService;

    public CustomerService(
        ApplicationDbContext dbContext,
        IPdfExportService pdfExportService,
        ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _pdfExportService = pdfExportService;
        _currentTenantService = currentTenantService;
    }

    public async Task<PagedResult<CustomerDto>> GetPagedAsync(CustomerListQuery query, CancellationToken cancellationToken = default)
    {
        var mapped = BuildCustomerQuery(query).Select(x => new CustomerDto
        {
            Id = x.Id,
            Name = x.Name,
            TinNumber = x.TinNumber,
            Phone = x.Phone,
            Email = x.Email,
            References = x.Contacts.OrderBy(c => c.CreatedAt).Select(c => c.Value).ToList()
        });

        return await mapped.ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<byte[]> GeneratePdfAsync(CustomerListQuery query, CancellationToken cancellationToken = default)
    {
        var customers = await BuildCustomerQuery(query)
            .Select(x => new CustomerDto
            {
                Id = x.Id,
                Name = x.Name,
                TinNumber = x.TinNumber,
                Phone = x.Phone,
                Email = x.Email,
                References = x.Contacts.OrderBy(c => c.CreatedAt).Select(c => c.Value).ToList()
            })
            .ToListAsync(cancellationToken);

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = $"TIN: {settings.TinNumber}, Phone: {settings.CompanyPhone}, Email: {settings.CompanyEmail}";
        return _pdfExportService.BuildCustomersPdf(customers, settings.CompanyName, companyInfo);
    }

    public async Task<byte[]> GenerateExcelAsync(CustomerListQuery query, CancellationToken cancellationToken = default)
    {
        var customers = await BuildCustomerQuery(query)
            .Select(x => new CustomerDto
            {
                Id = x.Id,
                Name = x.Name,
                TinNumber = x.TinNumber,
                Phone = x.Phone,
                Email = x.Email,
                References = x.Contacts.OrderBy(c => c.CreatedAt).Select(c => c.Value).ToList()
            })
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Customers");

        sheet.Cell(1, 1).Value = "Customer Name";
        sheet.Cell(1, 2).Value = "TIN No.";
        sheet.Cell(1, 3).Value = "Phone";
        sheet.Cell(1, 4).Value = "Email";
        sheet.Cell(1, 5).Value = "References";

        var row = 2;
        foreach (var customer in customers)
        {
            sheet.Cell(row, 1).Value = customer.Name;
            sheet.Cell(row, 2).Value = customer.TinNumber ?? string.Empty;
            sheet.Cell(row, 3).Value = customer.Phone ?? string.Empty;
            sheet.Cell(row, 4).Value = customer.Email ?? string.Empty;
            sheet.Cell(row, 5).Value = customer.References.Any() ? string.Join(", ", customer.References) : string.Empty;
            row++;
        }

        var header = sheet.Range(1, 1, 1, 5);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightBlue;

        if (row > 2)
        {
            sheet.Range(1, 1, row - 1, 5).SetAutoFilter();
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, 5).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Customers
            .AsNoTracking()
            .Include(x => x.Contacts)
            .Where(x => x.Id == id)
            .Select(x => new CustomerDto
            {
                Id = x.Id,
                Name = x.Name,
                TinNumber = x.TinNumber,
                Phone = x.Phone,
                Email = x.Email,
                References = x.Contacts.OrderBy(c => c.CreatedAt).Select(c => c.Value).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new AppException("Tenant context is missing.");
        var name = request.Name.Trim();
        var references = request.References.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();

        var existing = await _dbContext.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == name, cancellationToken);

        if (existing is not null)
        {
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.Name = name;
                existing.TinNumber = request.TinNumber?.Trim();
                existing.Phone = request.Phone?.Trim();
                existing.Email = request.Email?.Trim();

                await _dbContext.SaveChangesAsync(cancellationToken);
                return (await GetByIdAsync(existing.Id, cancellationToken))!;
            }

            throw new AppException("Customer name already exists.");
        }

        var customer = new Customer
        {
            Name = name,
            TinNumber = request.TinNumber?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            TenantId = tenantId
        };

        foreach (var reference in references)
        {
            customer.Contacts.Add(new CustomerContact
            {
                Value = reference,
                Label = "Reference"
            });
        }

        _dbContext.Customers.Add(customer);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
               {
                   SqlState: PostgresErrorCodes.UniqueViolation,
                   ConstraintName: "IX_Customers_TenantId_Name"
               })
        {
            throw new AppException("Customer name already exists.");
        }

        return (await GetByIdAsync(customer.Id, cancellationToken))!;
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers
            .Include(x => x.Contacts)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        var exists = await _dbContext.Customers
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Id != id && x.TenantId == customer.TenantId && x.Name == request.Name.Trim(), cancellationToken);
        if (exists)
        {
            throw new AppException("Customer name already exists.");
        }

        customer.Name = request.Name.Trim();
        customer.TinNumber = request.TinNumber?.Trim();
        customer.Phone = request.Phone?.Trim();
        customer.Email = request.Email?.Trim();

        foreach (var contact in customer.Contacts.ToList())
        {
            _dbContext.CustomerContacts.Remove(contact);
        }

        foreach (var reference in request.References.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()))
        {
            customer.Contacts.Add(new CustomerContact
            {
                Value = reference,
                Label = "Reference"
            });
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
               {
                   SqlState: PostgresErrorCodes.UniqueViolation,
                   ConstraintName: "IX_Customers_TenantId_Name"
               })
        {
            throw new AppException("Customer name already exists.");
        }

        return (await GetByIdAsync(customer.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers
            .Include(x => x.Contacts)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        var hasInvoice = await _dbContext.Invoices.AnyAsync(x => x.CustomerId == id, cancellationToken);
        var hasDeliveryNotes = await _dbContext.DeliveryNotes.AnyAsync(x => x.CustomerId == id, cancellationToken);
        if (hasInvoice || hasDeliveryNotes)
        {
            throw new AppException("Cannot delete customer with related transactions.");
        }

        foreach (var contact in customer.Contacts.ToList())
        {
            _dbContext.CustomerContacts.Remove(contact);
        }

        _dbContext.Customers.Remove(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Customer> BuildCustomerQuery(CustomerListQuery query)
    {
        var search = query.Search?.Trim();
        var searchLower = search?.ToLower();

        var customersQuery = _dbContext.Customers
            .AsNoTracking()
            .Include(x => x.Contacts)
            .Where(x => string.IsNullOrWhiteSpace(searchLower)
                || x.Name.ToLower().Contains(searchLower)
                || (x.TinNumber != null && x.TinNumber.ToLower().Contains(searchLower))
                || (x.Phone != null && x.Phone.ToLower().Contains(searchLower))
                || (x.Email != null && x.Email.ToLower().Contains(searchLower)));

        return query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? customersQuery.OrderBy(x => x.Name)
            : customersQuery.OrderByDescending(x => x.CreatedAt);
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }
}
