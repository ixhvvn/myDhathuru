using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Payroll.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class PayrollService : IPayrollService
{
    private const int FixedPayrollPeriodDays = 28;
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IPdfExportService _pdfExportService;
    private readonly ICurrentTenantService _currentTenantService;

    public PayrollService(
        ApplicationDbContext dbContext,
        IDocumentNumberService documentNumberService,
        IPdfExportService pdfExportService,
        ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _documentNumberService = documentNumberService;
        _pdfExportService = pdfExportService;
        _currentTenantService = currentTenantService;
    }

    public async Task<PagedResult<StaffDto>> GetStaffAsync(StaffListQuery query, CancellationToken cancellationToken = default)
    {
        var staffQuery = _dbContext.Staff
            .AsNoTracking()
            .Where(x => string.IsNullOrWhiteSpace(query.Search)
                || x.StaffId.ToLower().Contains(query.Search.ToLower())
                || x.StaffName.ToLower().Contains(query.Search.ToLower())
                || (x.Designation != null && x.Designation.ToLower().Contains(query.Search.ToLower()))
                || (x.WorkSite != null && x.WorkSite.ToLower().Contains(query.Search.ToLower())));

        staffQuery = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? staffQuery.OrderBy(x => x.StaffName)
            : staffQuery.OrderByDescending(x => x.CreatedAt);

        return await staffQuery.Select(x => MapStaff(x)).ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken = default)
    {
        var staffId = request.StaffId.Trim();
        var exists = await _dbContext.Staff.AnyAsync(x => x.StaffId == staffId, cancellationToken);
        if (exists)
        {
            throw new AppException("Staff ID already exists.");
        }

        var staff = new Staff
        {
            StaffId = staffId,
            StaffName = request.StaffName.Trim(),
            Designation = request.Designation?.Trim(),
            WorkSite = request.WorkSite?.Trim(),
            BankName = NormalizeBankName(request.BankName),
            AccountName = NormalizeOptional(request.AccountName),
            AccountNumber = NormalizeOptional(request.AccountNumber),
            Basic = request.Basic,
            ServiceAllowance = request.ServiceAllowance,
            OtherAllowance = request.OtherAllowance,
            PhoneAllowance = request.PhoneAllowance,
            FoodRate = request.FoodRate
        };

        _dbContext.Staff.Add(staff);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapStaff(staff);
    }

    public async Task<StaffDto> UpdateStaffAsync(Guid id, UpdateStaffRequest request, CancellationToken cancellationToken = default)
    {
        var staff = await _dbContext.Staff.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Staff not found.");

        var newStaffId = request.StaffId.Trim();
        var exists = await _dbContext.Staff.AnyAsync(x => x.Id != id && x.StaffId == newStaffId, cancellationToken);
        if (exists)
        {
            throw new AppException("Staff ID already exists.");
        }

        staff.StaffId = newStaffId;
        staff.StaffName = request.StaffName.Trim();
        staff.Designation = request.Designation?.Trim();
        staff.WorkSite = request.WorkSite?.Trim();
        staff.BankName = NormalizeBankName(request.BankName);
        staff.AccountName = NormalizeOptional(request.AccountName);
        staff.AccountNumber = NormalizeOptional(request.AccountNumber);
        staff.Basic = request.Basic;
        staff.ServiceAllowance = request.ServiceAllowance;
        staff.OtherAllowance = request.OtherAllowance;
        staff.PhoneAllowance = request.PhoneAllowance;
        staff.FoodRate = request.FoodRate;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapStaff(staff);
    }

    public async Task DeleteStaffAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var staff = await _dbContext.Staff.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Staff not found.");

        var hasPayroll = await _dbContext.PayrollEntries.AnyAsync(x => x.StaffId == id, cancellationToken);
        if (hasPayroll)
        {
            throw new AppException("Cannot delete staff with payroll history.");
        }

        _dbContext.Staff.Remove(staff);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollPeriodDto>> GetPeriodsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.PayrollPeriods
            .AsNoTracking()
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Select(x => new PayrollPeriodDto
            {
                Id = x.Id,
                Year = x.Year,
                Month = x.Month,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                PeriodDays = x.PeriodDays,
                Status = x.Status,
                TotalNetPayable = x.TotalNetPayable
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PayrollPeriodDetailDto> CreatePeriodAsync(CreatePayrollPeriodRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");

        var existingPeriod = await _dbContext.PayrollPeriods
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Year == request.Year && x.Month == request.Month,
                cancellationToken);

        if (existingPeriod is not null && !existingPeriod.IsDeleted)
        {
            throw new AppException("Payroll period already exists.");
        }

        if (existingPeriod is not null)
        {
            await PermanentlyDeletePeriodsAsync(tenantId, new[] { existingPeriod.Id }, cancellationToken);
        }

        var startDate = request.StartDate;
        var endDate = startDate.AddDays(FixedPayrollPeriodDays - 1);

        var period = new PayrollPeriod
        {
            Year = request.Year,
            Month = request.Month,
            StartDate = startDate,
            EndDate = endDate,
            PeriodDays = FixedPayrollPeriodDays,
            Status = PayrollPeriodStatus.Draft
        };

        var staffMembers = await _dbContext.Staff.AsNoTracking().OrderBy(x => x.StaffName).ToListAsync(cancellationToken);
        foreach (var staff in staffMembers)
        {
            var entry = BuildEntryFromStaff(period, staff);
            period.Entries.Add(entry);
        }

        period.TotalNetPayable = period.Entries.Sum(x => x.NetPayable);
        _dbContext.PayrollPeriods.Add(period);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetPeriodAsync(period.Id, cancellationToken);
    }

    public async Task<PayrollPeriodDetailDto> GetPeriodAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var period = await _dbContext.PayrollPeriods
            .Include(x => x.Entries)
            .ThenInclude(x => x.Staff)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Payroll period not found.");

        if (NormalizeToFixedPeriodDays(period))
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new PayrollPeriodDetailDto
        {
            Id = period.Id,
            Year = period.Year,
            Month = period.Month,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            PeriodDays = period.PeriodDays,
            Status = period.Status,
            TotalNetPayable = period.TotalNetPayable,
            Entries = period.Entries.OrderBy(x => x.Staff.StaffName).Select(MapEntry).ToList()
        };
    }

    public async Task DeletePeriodAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");

        var exists = await _dbContext.PayrollPeriods
            .AnyAsync(x => x.Id == id, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Payroll period not found.");
        }

        await PermanentlyDeletePeriodsAsync(tenantId, new[] { id }, cancellationToken);
    }

    public async Task RecalculatePeriodAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var period = await _dbContext.PayrollPeriods
            .Include(x => x.Entries)
            .ThenInclude(x => x.Staff)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Payroll period not found.");

        period.PeriodDays = FixedPayrollPeriodDays;
        period.EndDate = period.StartDate.AddDays(FixedPayrollPeriodDays - 1);

        foreach (var entry in period.Entries)
        {
            // Refresh base values from current staff master while preserving attendance and manual overrides.
            entry.Basic = entry.Staff.Basic;
            entry.ServiceAllowance = entry.Staff.ServiceAllowance;
            entry.OtherAllowance = entry.Staff.OtherAllowance;
            entry.PhoneAllowance = entry.Staff.PhoneAllowance;
            entry.FoodAllowanceRate = entry.Staff.FoodRate;
            entry.PeriodDays = FixedPayrollPeriodDays;

            RecalculateEntry(entry);
        }

        period.TotalNetPayable = period.Entries.Sum(x => x.NetPayable);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PayrollEntryDto> UpdatePayrollEntryAsync(Guid periodId, Guid entryId, UpdatePayrollEntryRequest request, CancellationToken cancellationToken = default)
    {
        var period = await _dbContext.PayrollPeriods
            .Include(x => x.Entries)
            .ThenInclude(x => x.Staff)
            .FirstOrDefaultAsync(x => x.Id == periodId, cancellationToken)
            ?? throw new NotFoundException("Payroll period not found.");

        var entry = period.Entries.FirstOrDefault(x => x.Id == entryId)
            ?? throw new NotFoundException("Payroll entry not found.");

        period.PeriodDays = FixedPayrollPeriodDays;
        period.EndDate = period.StartDate.AddDays(FixedPayrollPeriodDays - 1);
        entry.PeriodDays = FixedPayrollPeriodDays;

        if (request.AttendedDays.HasValue)
        {
            entry.AttendedDays = request.AttendedDays.Value;
        }

        if (request.FoodAllowanceDays.HasValue)
        {
            entry.FoodAllowanceDays = request.FoodAllowanceDays.Value;
        }

        if (request.FoodAllowanceRate.HasValue)
        {
            entry.FoodAllowanceRate = request.FoodAllowanceRate.Value;
        }

        if (request.OvertimePay.HasValue)
        {
            entry.OvertimePay = request.OvertimePay.Value;
        }

        if (request.SalaryAdvanceDeduction.HasValue)
        {
            entry.SalaryAdvanceDeduction = request.SalaryAdvanceDeduction.Value;
        }

        RecalculateEntry(entry);
        period.TotalNetPayable = period.Entries.Sum(x => x.NetPayable);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapEntry(entry);
    }

    public async Task<SalarySlipDto> GenerateSalarySlipAsync(Guid payrollEntryId, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.PayrollEntries
            .Include(x => x.PayrollPeriod)
            .Include(x => x.Staff)
            .FirstOrDefaultAsync(x => x.Id == payrollEntryId, cancellationToken)
            ?? throw new NotFoundException("Payroll entry not found.");

        var existingSlip = await _dbContext.SalarySlips
            .FirstOrDefaultAsync(x => x.PayrollEntryId == payrollEntryId, cancellationToken);

        if (existingSlip is null)
        {
            var slipNo = await _documentNumberService.GenerateAsync(DocumentType.SalarySlip, entry.PayrollPeriod.StartDate, cancellationToken);
            existingSlip = new SalarySlip
            {
                PayrollEntryId = entry.Id,
                SlipNo = slipNo
            };

            // Insert explicitly so EF tracks this as Added and emits INSERT.
            _dbContext.SalarySlips.Add(existingSlip);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        entry.SalarySlip = existingSlip;
        return MapSalarySlip(entry);
    }

    public async Task<SalarySlipDto?> GetSalarySlipAsync(Guid payrollEntryId, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.PayrollEntries
            .AsNoTracking()
            .Include(x => x.PayrollPeriod)
            .Include(x => x.Staff)
            .Include(x => x.SalarySlip)
            .FirstOrDefaultAsync(x => x.Id == payrollEntryId, cancellationToken);

        if (entry?.SalarySlip is null)
        {
            return null;
        }

        return MapSalarySlip(entry);
    }

    public async Task<byte[]> GenerateSalarySlipPdfAsync(Guid payrollEntryId, CancellationToken cancellationToken = default)
    {
        var salarySlip = await GetSalarySlipAsync(payrollEntryId, cancellationToken)
            ?? await GenerateSalarySlipAsync(payrollEntryId, cancellationToken);

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = $"TIN: {settings.TinNumber}, Phone: {settings.CompanyPhone}, Email: {settings.CompanyEmail}";
        return _pdfExportService.BuildSalarySlipPdf(salarySlip, settings.CompanyName, companyInfo, settings.LogoUrl);
    }

    public async Task<byte[]> GeneratePayrollPeriodPdfAsync(Guid payrollPeriodId, CancellationToken cancellationToken = default)
    {
        var detail = await GetPeriodAsync(payrollPeriodId, cancellationToken);
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = $"TIN: {settings.TinNumber}, Phone: {settings.CompanyPhone}, Email: {settings.CompanyEmail}";

        return _pdfExportService.BuildPayrollPeriodPdf(detail, settings.CompanyName, companyInfo);
    }

    public async Task<byte[]> GeneratePayrollPeriodExcelAsync(Guid payrollPeriodId, CancellationToken cancellationToken = default)
    {
        var detail = await GetPeriodAsync(payrollPeriodId, cancellationToken);
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = $"TIN: {settings.TinNumber}, Phone: {settings.CompanyPhone}, Email: {settings.CompanyEmail}";

        return BuildPayrollPeriodExcel(detail, settings.CompanyName, companyInfo);
    }

    private PayrollEntry BuildEntryFromStaff(PayrollPeriod period, Staff staff)
    {
        var entry = new PayrollEntry
        {
            PayrollPeriod = period,
            StaffId = staff.Id,
            Basic = staff.Basic,
            ServiceAllowance = staff.ServiceAllowance,
            OtherAllowance = staff.OtherAllowance,
            PhoneAllowance = staff.PhoneAllowance,
            PeriodDays = FixedPayrollPeriodDays,
            AttendedDays = FixedPayrollPeriodDays,
            FoodAllowanceDays = 0,
            FoodAllowanceRate = staff.FoodRate,
            OvertimePay = 0,
            SalaryAdvanceDeduction = 0
        };

        RecalculateEntry(entry);
        return entry;
    }

    private static void RecalculateEntry(PayrollEntry entry)
    {
        entry.PeriodDays = FixedPayrollPeriodDays;
        entry.AttendedDays = Math.Clamp(entry.AttendedDays, 0, entry.PeriodDays);
        entry.FoodAllowanceDays = Math.Max(0, entry.FoodAllowanceDays);

        entry.GrossBase = Round2(entry.Basic);
        entry.GrossAllowances = Round2(entry.ServiceAllowance + entry.OtherAllowance + entry.PhoneAllowance);
        entry.SubTotal = Round2(entry.Basic + entry.ServiceAllowance + entry.OtherAllowance);
        entry.RatePerDay = Math.Round(entry.SubTotal / entry.PeriodDays, 4, MidpointRounding.AwayFromZero);

        entry.AbsentDays = Math.Max(0, entry.PeriodDays - entry.AttendedDays);
        entry.AbsentDeduction = Round2(entry.RatePerDay * entry.AbsentDays);

        entry.FoodAllowance = Round2(entry.FoodAllowanceRate * entry.FoodAllowanceDays);
        entry.PensionDeduction = Round2(entry.Basic * 0.07m);

        entry.TotalPay = Round2((entry.RatePerDay * entry.AttendedDays) + entry.PhoneAllowance + entry.FoodAllowance + entry.OvertimePay);
        entry.NetPayable = Round2(entry.TotalPay - entry.PensionDeduction - entry.SalaryAdvanceDeduction);
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static byte[] BuildPayrollPeriodExcel(PayrollPeriodDetailDto detail, string companyName, string companyInfo)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Payroll Detail");
        const int TotalColumns = 22;

        sheet.Cell(1, 1).Value = $"{companyName} - Payroll Detail";
        sheet.Range(1, 1, 1, TotalColumns).Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell(2, 1).Value = companyInfo;
        sheet.Range(2, 1, 2, TotalColumns).Merge().Style.Font.SetFontSize(10).Font.SetFontColor(XLColor.FromHtml("#51648F"));
        sheet.Cell(3, 1).Value = $"Period: {detail.Year}-{detail.Month:00} | {detail.StartDate:yyyy-MM-dd} to {detail.EndDate:yyyy-MM-dd}";
        sheet.Range(3, 1, 3, TotalColumns).Merge().Style.Font.SetFontSize(10).Font.SetFontColor(XLColor.FromHtml("#51648F"));
        sheet.Cell(4, 1).Value = $"Entries: {detail.Entries.Count:N0} | Total Net Payable: MVR {detail.TotalNetPayable:N2}";
        sheet.Range(4, 1, 4, TotalColumns).Merge().Style.Font.SetFontSize(10).Font.SetFontColor(XLColor.FromHtml("#51648F"));

        var headerRow = 6;
        sheet.Cell(headerRow, 1).Value = "Staff ID";
        sheet.Cell(headerRow, 2).Value = "Staff Name";
        sheet.Cell(headerRow, 3).Value = "Designation";
        sheet.Cell(headerRow, 4).Value = "Work Site";
        sheet.Cell(headerRow, 5).Value = "Basic";
        sheet.Cell(headerRow, 6).Value = "Service Allowance";
        sheet.Cell(headerRow, 7).Value = "Other Allowance";
        sheet.Cell(headerRow, 8).Value = "Phone Allowance";
        sheet.Cell(headerRow, 9).Value = "Sub Total";
        sheet.Cell(headerRow, 10).Value = "Attended";
        sheet.Cell(headerRow, 11).Value = "Rate / Day";
        sheet.Cell(headerRow, 12).Value = "Food Days";
        sheet.Cell(headerRow, 13).Value = "Absent Deduction";
        sheet.Cell(headerRow, 14).Value = "Absent Days";
        sheet.Cell(headerRow, 15).Value = "Food A Rate";
        sheet.Cell(headerRow, 16).Value = "Food Allowance";
        sheet.Cell(headerRow, 17).Value = "OT Pay";
        sheet.Cell(headerRow, 18).Value = "Pension";
        sheet.Cell(headerRow, 19).Value = "Salary Advance";
        sheet.Cell(headerRow, 20).Value = "Total Pay";
        sheet.Cell(headerRow, 21).Value = "Account Number";
        sheet.Cell(headerRow, 22).Value = "Total Salary";

        var headerRange = sheet.Range(headerRow, 1, headerRow, TotalColumns);
        headerRange.Style.Font.SetBold();
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEFF");
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Alignment.WrapText = true;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var row = headerRow + 1;
        foreach (var entry in detail.Entries)
        {
            sheet.Cell(row, 1).Value = entry.StaffCode;
            sheet.Cell(row, 2).Value = entry.StaffName;
            sheet.Cell(row, 3).Value = entry.Designation ?? "-";
            sheet.Cell(row, 4).Value = entry.WorkSite ?? "-";
            sheet.Cell(row, 5).Value = entry.Basic;
            sheet.Cell(row, 6).Value = entry.ServiceAllowance;
            sheet.Cell(row, 7).Value = entry.OtherAllowance;
            sheet.Cell(row, 8).Value = entry.PhoneAllowance;
            sheet.Cell(row, 9).Value = entry.SubTotal;
            sheet.Cell(row, 10).Value = entry.AttendedDays;
            sheet.Cell(row, 11).Value = entry.RatePerDay;
            sheet.Cell(row, 12).Value = entry.FoodAllowanceDays;
            sheet.Cell(row, 13).Value = entry.AbsentDeduction;
            sheet.Cell(row, 14).Value = entry.AbsentDays;
            sheet.Cell(row, 15).Value = entry.FoodAllowanceRate;
            sheet.Cell(row, 16).Value = entry.FoodAllowance;
            sheet.Cell(row, 17).Value = entry.OvertimePay;
            sheet.Cell(row, 18).Value = entry.PensionDeduction;
            sheet.Cell(row, 19).Value = entry.SalaryAdvanceDeduction;
            sheet.Cell(row, 20).Value = entry.TotalPay;
            sheet.Cell(row, 21).Value = string.IsNullOrWhiteSpace(entry.AccountNumber) ? "-" : entry.AccountNumber;
            sheet.Cell(row, 22).Value = entry.NetPayable;
            row++;
        }

        var totalRow = row;
        sheet.Cell(totalRow, 1).Value = "TOTAL";
        sheet.Cell(totalRow, 5).Value = detail.Entries.Sum(x => x.Basic);
        sheet.Cell(totalRow, 6).Value = detail.Entries.Sum(x => x.ServiceAllowance);
        sheet.Cell(totalRow, 7).Value = detail.Entries.Sum(x => x.OtherAllowance);
        sheet.Cell(totalRow, 8).Value = detail.Entries.Sum(x => x.PhoneAllowance);
        sheet.Cell(totalRow, 9).Value = detail.Entries.Sum(x => x.SubTotal);
        sheet.Cell(totalRow, 10).Value = detail.Entries.Sum(x => x.AttendedDays);
        sheet.Cell(totalRow, 12).Value = detail.Entries.Sum(x => x.FoodAllowanceDays);
        sheet.Cell(totalRow, 13).Value = detail.Entries.Sum(x => x.AbsentDeduction);
        sheet.Cell(totalRow, 14).Value = detail.Entries.Sum(x => x.AbsentDays);
        sheet.Cell(totalRow, 16).Value = detail.Entries.Sum(x => x.FoodAllowance);
        sheet.Cell(totalRow, 17).Value = detail.Entries.Sum(x => x.OvertimePay);
        sheet.Cell(totalRow, 18).Value = detail.Entries.Sum(x => x.PensionDeduction);
        sheet.Cell(totalRow, 19).Value = detail.Entries.Sum(x => x.SalaryAdvanceDeduction);
        sheet.Cell(totalRow, 20).Value = detail.Entries.Sum(x => x.TotalPay);
        sheet.Cell(totalRow, 22).Value = detail.TotalNetPayable;

        var totalRange = sheet.Range(totalRow, 1, totalRow, TotalColumns);
        totalRange.Style.Font.SetBold();
        totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5FF");
        totalRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        totalRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        if (detail.Entries.Count > 0)
        {
            var dataRange = sheet.Range(headerRow + 1, 1, totalRow - 1, TotalColumns);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        sheet.Range(headerRow + 1, 5, totalRow, 9).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(headerRow + 1, 10, totalRow, 10).Style.NumberFormat.Format = "#,##0";
        sheet.Range(headerRow + 1, 11, totalRow, 11).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(headerRow + 1, 12, totalRow, 12).Style.NumberFormat.Format = "#,##0";
        sheet.Range(headerRow + 1, 13, totalRow, 13).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(headerRow + 1, 14, totalRow, 14).Style.NumberFormat.Format = "#,##0";
        sheet.Range(headerRow + 1, 15, totalRow, 20).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(headerRow + 1, 22, totalRow, 22).Style.NumberFormat.Format = "#,##0.00";
        sheet.SheetView.FreezeRows(headerRow);
        sheet.Range(headerRow, 1, Math.Max(totalRow - 1, headerRow), TotalColumns).SetAutoFilter();

        sheet.Column(1).Width = 14;
        sheet.Column(2).Width = 22;
        sheet.Column(3).Width = 16;
        sheet.Column(4).Width = 14;
        sheet.Column(5).Width = 12;
        sheet.Column(6).Width = 15;
        sheet.Column(7).Width = 15;
        sheet.Column(8).Width = 15;
        sheet.Column(9).Width = 12;
        sheet.Column(10).Width = 10;
        sheet.Column(11).Width = 12;
        sheet.Column(12).Width = 10;
        sheet.Column(13).Width = 14;
        sheet.Column(14).Width = 11;
        sheet.Column(15).Width = 13;
        sheet.Column(16).Width = 15;
        sheet.Column(17).Width = 11;
        sheet.Column(18).Width = 12;
        sheet.Column(19).Width = 14;
        sheet.Column(20).Width = 12;
        sheet.Column(21).Width = 18;
        sheet.Column(22).Width = 13;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static StaffDto MapStaff(Staff staff)
    {
        return new StaffDto
        {
            Id = staff.Id,
            StaffId = staff.StaffId,
            StaffName = staff.StaffName,
            Designation = staff.Designation,
            WorkSite = staff.WorkSite,
            BankName = staff.BankName,
            AccountName = staff.AccountName,
            AccountNumber = staff.AccountNumber,
            Basic = staff.Basic,
            ServiceAllowance = staff.ServiceAllowance,
            OtherAllowance = staff.OtherAllowance,
            PhoneAllowance = staff.PhoneAllowance,
            FoodRate = staff.FoodRate
        };
    }

    private static PayrollEntryDto MapEntry(PayrollEntry entry)
    {
        return new PayrollEntryDto
        {
            Id = entry.Id,
            StaffId = entry.StaffId,
            StaffCode = entry.Staff.StaffId,
            StaffName = entry.Staff.StaffName,
            Designation = entry.Staff.Designation,
            WorkSite = entry.Staff.WorkSite,
            BankName = entry.Staff.BankName,
            AccountName = entry.Staff.AccountName,
            AccountNumber = entry.Staff.AccountNumber,

            Basic = entry.Basic,
            ServiceAllowance = entry.ServiceAllowance,
            OtherAllowance = entry.OtherAllowance,
            PhoneAllowance = entry.PhoneAllowance,
            GrossBase = entry.GrossBase,
            GrossAllowances = entry.GrossAllowances,
            SubTotal = entry.SubTotal,
            PeriodDays = entry.PeriodDays,
            AttendedDays = entry.AttendedDays,
            AbsentDays = entry.AbsentDays,
            RatePerDay = entry.RatePerDay,
            AbsentDeduction = entry.AbsentDeduction,
            FoodAllowanceDays = entry.FoodAllowanceDays,
            FoodAllowanceRate = entry.FoodAllowanceRate,
            FoodAllowance = entry.FoodAllowance,
            OvertimePay = entry.OvertimePay,
            PensionDeduction = entry.PensionDeduction,
            SalaryAdvanceDeduction = entry.SalaryAdvanceDeduction,
            TotalPay = entry.TotalPay,
            NetPayable = entry.NetPayable
        };
    }

    private static SalarySlipDto MapSalarySlip(PayrollEntry entry)
    {
        var totalSalary = Round2(entry.SubTotal + entry.PhoneAllowance + entry.FoodAllowance + entry.OvertimePay);
        var totalDeduction = Round2(entry.AbsentDeduction + entry.PensionDeduction + entry.SalaryAdvanceDeduction);

        return new SalarySlipDto
        {
            PayrollEntryId = entry.Id,
            SlipNo = entry.SalarySlip?.SlipNo ?? string.Empty,
            PeriodStart = entry.PayrollPeriod.StartDate,
            PeriodEnd = entry.PayrollPeriod.EndDate,
            StaffName = entry.Staff.StaffName,
            StaffCode = entry.Staff.StaffId,
            Designation = entry.Staff.Designation,
            WorkSite = entry.Staff.WorkSite,
            BankName = entry.Staff.BankName,
            AccountName = entry.Staff.AccountName,
            AccountNumber = entry.Staff.AccountNumber,
            BasicSalary = entry.Basic,
            ServiceAllowance = entry.ServiceAllowance,
            OtherAllowance = entry.OtherAllowance,
            PhoneAllowance = entry.PhoneAllowance,
            FoodAllowance = entry.FoodAllowance,
            OvertimePay = entry.OvertimePay,
            FoodAllowanceDays = entry.FoodAllowanceDays,
            FoodAllowanceRate = entry.FoodAllowanceRate,
            AttendedDays = entry.AttendedDays,
            AbsentDays = entry.AbsentDays,
            RatePerDay = entry.RatePerDay,
            AbsentDeduction = entry.AbsentDeduction,
            PensionDeduction = entry.PensionDeduction,
            SalaryAdvanceDeduction = entry.SalaryAdvanceDeduction,
            TotalSalary = totalSalary,
            TotalDeduction = totalDeduction,
            TotalPayable = entry.NetPayable
        };
    }

    private static bool NormalizeToFixedPeriodDays(PayrollPeriod period)
    {
        var changed = false;

        if (period.PeriodDays != FixedPayrollPeriodDays)
        {
            period.PeriodDays = FixedPayrollPeriodDays;
            changed = true;
        }

        var normalizedEndDate = period.StartDate.AddDays(FixedPayrollPeriodDays - 1);
        if (period.EndDate != normalizedEndDate)
        {
            period.EndDate = normalizedEndDate;
            changed = true;
        }

        foreach (var entry in period.Entries)
        {
            var beforeState = (
                entry.PeriodDays,
                entry.AttendedDays,
                entry.AbsentDays,
                entry.RatePerDay,
                entry.AbsentDeduction,
                entry.TotalPay,
                entry.NetPayable);

            entry.PeriodDays = FixedPayrollPeriodDays;
            RecalculateEntry(entry);

            var afterState = (
                entry.PeriodDays,
                entry.AttendedDays,
                entry.AbsentDays,
                entry.RatePerDay,
                entry.AbsentDeduction,
                entry.TotalPay,
                entry.NetPayable);

            if (!beforeState.Equals(afterState))
            {
                changed = true;
            }
        }

        var totalNetPayable = period.Entries.Sum(x => x.NetPayable);
        if (period.TotalNetPayable != totalNetPayable)
        {
            period.TotalNetPayable = totalNetPayable;
            changed = true;
        }

        return changed;
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private async Task PermanentlyDeletePeriodsAsync(Guid tenantId, IReadOnlyCollection<Guid> periodIds, CancellationToken cancellationToken)
    {
        if (periodIds.Count == 0)
        {
            return;
        }

        var scopedPeriodIds = await _dbContext.PayrollPeriods
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && periodIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (scopedPeriodIds.Count == 0)
        {
            return;
        }

        var entryIds = await _dbContext.PayrollEntries
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && scopedPeriodIds.Contains(x.PayrollPeriodId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (entryIds.Count > 0)
        {
            await _dbContext.SalarySlips
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && entryIds.Contains(x.PayrollEntryId))
                .ExecuteDeleteAsync(cancellationToken);

            await _dbContext.PayrollEntries
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && scopedPeriodIds.Contains(x.PayrollPeriodId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await _dbContext.PayrollPeriods
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && scopedPeriodIds.Contains(x.Id))
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static string? NormalizeBankName(string? requestedBankName)
    {
        if (string.IsNullOrWhiteSpace(requestedBankName))
        {
            return null;
        }

        if (!Enum.TryParse<BankCode>(requestedBankName.Trim(), true, out var parsed))
        {
            throw new AppException("Bank must be BML or MIB.");
        }

        return parsed.ToString().ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
