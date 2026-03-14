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
        return _pdfExportService.BuildCustomersPdf(customers, settings.CompanyName, companyInfo, settings.LogoUrl, query);
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

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var generatedAt = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5));
        var searchLabel = string.IsNullOrWhiteSpace(query.Search) ? "All customers" : query.Search.Trim();
        var sortLabel = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? "Name (A-Z)"
            : "Newest first";
        var customersWithEmail = customers.Count(x => !string.IsNullOrWhiteSpace(x.Email));
        var customersWithPhone = customers.Count(x => !string.IsNullOrWhiteSpace(x.Phone));
        var customersWithReferences = customers.Count(x => x.References.Any());
        var totalReferences = customers.Sum(x => x.References.Count);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Customers");
        const int totalColumns = 5;

        var heroRange = sheet.Range(1, 1, 3, totalColumns);
        heroRange.Merge();
        heroRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#18315D");
        heroRange.Style.Font.FontColor = XLColor.White;
        heroRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        heroRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        heroRange.Style.Alignment.WrapText = true;

        var hero = sheet.Cell(1, 1).GetRichText();
        hero.ClearText();
        hero.AddText("Customer Directory").SetBold().SetFontSize(19).SetFontColor(XLColor.White);
        hero.AddText(Environment.NewLine);
        hero.AddText(settings.CompanyName).SetBold().SetFontSize(11).SetFontColor(XLColor.FromHtml("#DDE9FF"));
        hero.AddText(Environment.NewLine);
        hero.AddText($"Generated {generatedAt:yyyy-MM-dd HH:mm} MVT | Search: {searchLabel}")
            .SetFontSize(9)
            .SetFontColor(XLColor.FromHtml("#BFD4FF"));

        sheet.Row(1).Height = 26;
        sheet.Row(2).Height = 22;
        sheet.Row(3).Height = 22;

        var metaCards = new[]
        {
            new CustomerExcelMetaCard("Company", settings.CompanyName),
            new CustomerExcelMetaCard("Sort", sortLabel),
            new CustomerExcelMetaCard("Contact", $"{settings.CompanyPhone} | {settings.CompanyEmail}")
        };
        var metaSegments = BuildColumnSegments(totalColumns, metaCards.Length);
        for (var index = 0; index < metaCards.Length; index++)
        {
            var (startCol, endCol) = metaSegments[index];
            WriteMetaCard(sheet, 5, 6, startCol, endCol, metaCards[index]);
        }

        var summaryTiles = new[]
        {
            new CustomerExcelSummaryTile("Total Customers", customers.Count.ToString("N0"), "Customer records in this export.", "#EEF3FF"),
            new CustomerExcelSummaryTile("With Email", customersWithEmail.ToString("N0"), "Customer records with email contact.", "#ECFAF6"),
            new CustomerExcelSummaryTile("With Phone", customersWithPhone.ToString("N0"), "Customer records with phone contact.", "#EFF8FF"),
            new CustomerExcelSummaryTile("Reference Links", totalReferences.ToString("N0"), $"{customersWithReferences:N0} customer(s) with linked references.", "#F4F1FF")
        };

        var summarySegments = BuildColumnSegments(totalColumns, summaryTiles.Length);
        for (var index = 0; index < summaryTiles.Length; index++)
        {
            var (startCol, endCol) = summarySegments[index];
            WriteSummaryTile(sheet, 8, 10, startCol, endCol, summaryTiles[index]);
        }

        WriteSectionHeading(sheet, 12, 1, totalColumns, "Customer List");

        sheet.Cell(13, 1).Value = "Customer Name";
        sheet.Cell(13, 2).Value = "TIN No.";
        sheet.Cell(13, 3).Value = "Phone";
        sheet.Cell(13, 4).Value = "Email";
        sheet.Cell(13, 5).Value = "References";
        StyleExcelTableHeader(sheet.Range(13, 1, 13, totalColumns));

        var row = 14;
        if (customers.Count == 0)
        {
            var emptyRange = sheet.Range(row, 1, row + 1, totalColumns);
            emptyRange.Merge();
            emptyRange.Value = "No customers matched the current export filter.";
            emptyRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            emptyRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            emptyRange.Style.Font.FontColor = XLColor.FromHtml("#6078A7");
            emptyRange.Style.Font.Bold = true;
            emptyRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F7FAFF");
            emptyRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            emptyRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
            sheet.Row(row).Height = 24;
            sheet.Row(row + 1).Height = 24;
            row += 2;
        }
        else
        {
            foreach (var customer in customers)
            {
                sheet.Cell(row, 1).Value = customer.Name;
                sheet.Cell(row, 2).Value = customer.TinNumber ?? "-";
                sheet.Cell(row, 3).Value = customer.Phone ?? "-";
                sheet.Cell(row, 4).Value = customer.Email ?? "-";
                sheet.Cell(row, 5).Value = customer.References.Any() ? string.Join(", ", customer.References) : "-";
                row++;
            }

            StyleExcelBodyRows(sheet.Range(14, 1, row - 1, totalColumns));

            sheet.Cell(row, 1).Value = "TOTAL";
            sheet.Cell(row, 2).Value = customers.Count;
            sheet.Cell(row, 3).Value = customersWithPhone;
            sheet.Cell(row, 4).Value = customersWithEmail;
            sheet.Cell(row, 5).Value = totalReferences;
            StyleExcelTotalRow(sheet.Range(row, 1, row, totalColumns));

            sheet.Range(13, 1, row - 1, totalColumns).SetAutoFilter();
            row++;
        }

        sheet.SheetView.FreezeRows(13);
        sheet.Column(1).Width = 24;
        sheet.Column(2).Width = 16;
        sheet.Column(3).Width = 18;
        sheet.Column(4).Width = 32;
        sheet.Column(5).Width = 38;
        sheet.Rows(13, Math.Max(13, row)).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        sheet.Rows(13, Math.Max(13, row)).Style.Alignment.WrapText = true;

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

    private static IReadOnlyList<(int StartColumn, int EndColumn)> BuildColumnSegments(int totalColumns, int segmentCount)
    {
        var segments = new List<(int StartColumn, int EndColumn)>(segmentCount);
        var baseWidth = totalColumns / segmentCount;
        var remainder = totalColumns % segmentCount;
        var cursor = 1;

        for (var index = 0; index < segmentCount; index++)
        {
            var width = baseWidth + (index < remainder ? 1 : 0);
            var startColumn = cursor;
            var endColumn = cursor + width - 1;
            segments.Add((startColumn, endColumn));
            cursor = endColumn + 1;
        }

        return segments;
    }

    private static void WriteMetaCard(
        IXLWorksheet sheet,
        int startRow,
        int endRow,
        int startColumn,
        int endColumn,
        CustomerExcelMetaCard card)
    {
        var range = sheet.Range(startRow, startColumn, endRow, endColumn);
        range.Merge();
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F7FAFF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        range.Style.Alignment.SetIndent(1);

        var rich = sheet.Cell(startRow, startColumn).GetRichText();
        rich.ClearText();
        rich.AddText(card.Label.ToUpperInvariant()).SetBold().SetFontSize(8).SetFontColor(XLColor.FromHtml("#6A7FA8"));
        rich.AddText(Environment.NewLine);
        rich.AddText(card.Value).SetBold().SetFontSize(10.5).SetFontColor(XLColor.FromHtml("#243B63"));
    }

    private static void WriteSummaryTile(
        IXLWorksheet sheet,
        int startRow,
        int endRow,
        int startColumn,
        int endColumn,
        CustomerExcelSummaryTile tile)
    {
        var range = sheet.Range(startRow, startColumn, endRow, endColumn);
        range.Merge();
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(tile.FillColor);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        range.Style.Alignment.SetIndent(1);

        var rich = sheet.Cell(startRow, startColumn).GetRichText();
        rich.ClearText();
        rich.AddText(tile.Label.ToUpperInvariant()).SetBold().SetFontSize(8).SetFontColor(XLColor.FromHtml("#6A7FA8"));
        rich.AddText(Environment.NewLine);
        rich.AddText(tile.Value).SetBold().SetFontSize(15).SetFontColor(XLColor.FromHtml("#243B63"));
        rich.AddText(Environment.NewLine);
        rich.AddText(tile.Detail).SetFontSize(8.5).SetFontColor(XLColor.FromHtml("#6178A3"));
    }

    private static void WriteSectionHeading(IXLWorksheet sheet, int row, int startColumn, int endColumn, string title)
    {
        var range = sheet.Range(row, startColumn, row, endColumn);
        range.Merge();
        range.Value = title;
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#30466F");
        range.Style.Font.FontSize = 11;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDF3FF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.Indent = 1;
        sheet.Row(row).Height = 22;
    }

    private static void StyleExcelTableHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#30466F");
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEFF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void StyleExcelBodyRows(IXLRange range)
    {
        if (range.RowCount() <= 0)
        {
            return;
        }

        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Style.Alignment.WrapText = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#30466F");

        for (var index = 1; index <= range.RowCount(); index++)
        {
            var rowRange = range.Row(index);
            rowRange.Style.Fill.BackgroundColor = index % 2 == 0
                ? XLColor.FromHtml("#F9FBFF")
                : XLColor.White;
        }
    }

    private static void StyleExcelTotalRow(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#243B63");
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDF3FF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#D8E2F4");
    }

    private sealed record CustomerExcelMetaCard(string Label, string Value);
    private sealed record CustomerExcelSummaryTile(string Label, string Value, string Detail, string FillColor);
}
