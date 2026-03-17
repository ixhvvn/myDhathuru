using System.Security.Cryptography;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.StaffConduct.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class StaffConductService : IStaffConductService
{
    private const string PdfContentType = "application/pdf";

    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IPdfExportService _pdfExportService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IBusinessAuditLogService _auditLogService;

    public StaffConductService(
        ApplicationDbContext dbContext,
        IDocumentNumberService documentNumberService,
        IPdfExportService pdfExportService,
        ICurrentTenantService currentTenantService,
        IBusinessAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _documentNumberService = documentNumberService;
        _pdfExportService = pdfExportService;
        _currentTenantService = currentTenantService;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<StaffConductListItemDto>> GetPagedAsync(StaffConductListQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyListFilters(_dbContext.StaffConductForms.AsNoTracking(), query);
        filtered = ApplyOrdering(filtered, query);

        return await filtered
            .Select(x => MapListItem(x))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<StaffConductSummaryDto> GetSummaryAsync(StaffConductListQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await ApplyListFilters(_dbContext.StaffConductForms.AsNoTracking(), query).ToListAsync(cancellationToken);
        return BuildSummary(rows);
    }

    public async Task<IReadOnlyList<StaffConductStaffOptionDto>> GetStaffOptionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Staff
            .AsNoTracking()
            .OrderBy(x => x.StaffName)
            .Select(x => new StaffConductStaffOptionDto
            {
                Id = x.Id,
                StaffId = x.StaffId,
                StaffName = x.StaffName,
                Designation = x.Designation,
                WorkSite = x.WorkSite
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<StaffConductDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetStaffConductFormAsync(id, asTracking: false, cancellationToken);
        var savedPdf = await GetSavedDhivehiPdfAsync(id, asTracking: false, cancellationToken);
        return MapDetail(entity, savedPdf);
    }

    public async Task<StaffConductDetailDto> CreateAsync(CreateStaffConductFormRequest request, CancellationToken cancellationToken = default)
    {
        var staff = await _dbContext.Staff.FirstOrDefaultAsync(x => x.Id == request.StaffId, cancellationToken)
            ?? throw new NotFoundException("Staff not found.");

        var formNumber = await _documentNumberService.GenerateAsync(
            request.FormType == StaffConductFormType.Warning ? DocumentType.WarningForm : DocumentType.DisciplinaryForm,
            request.IssueDate,
            cancellationToken);

        var entity = new StaffConductForm
        {
            StaffId = staff.Id,
            FormNumber = formNumber,
            FormType = request.FormType,
            IssueDate = request.IssueDate,
            IncidentDate = request.IncidentDate,
            Subject = request.Subject.Trim(),
            IncidentDetails = request.IncidentDetails.Trim(),
            ActionTaken = request.ActionTaken.Trim(),
            RequiredImprovement = NormalizeOptional(request.RequiredImprovement),
            Severity = request.Severity,
            Status = request.Status,
            IssuedBy = request.IssuedBy.Trim(),
            WitnessedBy = NormalizeOptional(request.WitnessedBy),
            FollowUpDate = request.FollowUpDate,
            IsAcknowledgedByStaff = request.IsAcknowledgedByStaff,
            AcknowledgedDate = request.AcknowledgedDate,
            EmployeeRemarks = NormalizeOptional(request.EmployeeRemarks),
            ResolutionNotes = NormalizeOptional(request.ResolutionNotes),
            ResolvedDate = request.ResolvedDate
        };

        ApplyStaffSnapshot(entity, staff);

        _dbContext.StaffConductForms.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.StaffConductFormCreated,
            nameof(StaffConductForm),
            entity.Id.ToString(),
            entity.FormNumber,
            new
            {
                entity.FormType,
                entity.StaffCodeSnapshot,
                entity.StaffNameSnapshot,
                entity.Status
            },
            cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<StaffConductDetailDto> UpdateAsync(Guid id, UpdateStaffConductFormRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetStaffConductFormAsync(id, asTracking: true, cancellationToken);
        var staff = await _dbContext.Staff.FirstOrDefaultAsync(x => x.Id == request.StaffId, cancellationToken)
            ?? throw new NotFoundException("Staff not found.");

        entity.StaffId = staff.Id;
        entity.FormType = request.FormType;
        entity.IssueDate = request.IssueDate;
        entity.IncidentDate = request.IncidentDate;
        entity.Subject = request.Subject.Trim();
        entity.IncidentDetails = request.IncidentDetails.Trim();
        entity.ActionTaken = request.ActionTaken.Trim();
        entity.RequiredImprovement = NormalizeOptional(request.RequiredImprovement);
        entity.Severity = request.Severity;
        entity.Status = request.Status;
        entity.IssuedBy = request.IssuedBy.Trim();
        entity.WitnessedBy = NormalizeOptional(request.WitnessedBy);
        entity.FollowUpDate = request.FollowUpDate;
        entity.IsAcknowledgedByStaff = request.IsAcknowledgedByStaff;
        entity.AcknowledgedDate = request.AcknowledgedDate;
        entity.EmployeeRemarks = NormalizeOptional(request.EmployeeRemarks);
        entity.ResolutionNotes = NormalizeOptional(request.ResolutionNotes);
        entity.ResolvedDate = request.ResolvedDate;

        ApplyStaffSnapshot(entity, staff);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.StaffConductFormUpdated,
            nameof(StaffConductForm),
            entity.Id.ToString(),
            entity.FormNumber,
            new
            {
                entity.FormType,
                entity.StaffCodeSnapshot,
                entity.StaffNameSnapshot,
                entity.Status
            },
            cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<byte[]> ExportPdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await GetByIdAsync(id, cancellationToken);
        var settings = await GetTenantSettingsAsync(cancellationToken);
        return _pdfExportService.BuildStaffConductFormPdf(detail, settings.CompanyName, settings.BuildCompanyInfo(), settings.LogoUrl);
    }

    public async Task<byte[]> ExportExcelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await GetByIdAsync(id, cancellationToken);
        var settings = await GetTenantSettingsAsync(cancellationToken);
        return BuildDetailExcel(detail, settings.CompanyName, settings.BuildCompanyInfo());
    }

    public async Task<StaffConductDhivehiExportDto> GetDhivehiExportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetStaffConductFormAsync(id, asTracking: false, cancellationToken);
        var savedPdf = await GetSavedDhivehiPdfAsync(id, asTracking: false, cancellationToken);
        return BuildDhivehiExportDto(entity, savedPdf);
    }

    public async Task<StaffConductDhivehiExportDto> SaveDhivehiExportAsync(Guid id, UpsertStaffConductDhivehiExportRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetStaffConductFormAsync(id, asTracking: true, cancellationToken);

        entity.SubjectDv = NormalizeOptional(request.SubjectDv);
        entity.IncidentDetailsDv = NormalizeOptional(request.IncidentDetailsDv);
        entity.ActionTakenDv = NormalizeOptional(request.ActionTakenDv);
        entity.RequiredImprovementDv = NormalizeOptional(request.RequiredImprovementDv);
        entity.EmployeeRemarksDv = NormalizeOptional(request.EmployeeRemarksDv);
        entity.AcknowledgementDv = NormalizeOptional(request.AcknowledgementDv);
        entity.ResolutionNotesDv = NormalizeOptional(request.ResolutionNotesDv);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.StaffConductDhivehiSaved,
            nameof(StaffConductForm),
            entity.Id.ToString(),
            entity.FormNumber,
            new
            {
                entity.FormType,
                HasDhivehiContent = HasDhivehiContent(entity),
                MissingRequiredFields = GetMissingRequiredDhivehiFields(entity)
            },
            cancellationToken);

        var savedPdf = await GetSavedDhivehiPdfAsync(id, asTracking: false, cancellationToken);
        return BuildDhivehiExportDto(entity, savedPdf);
    }

    public async Task<StaffConductExportFileDto> GenerateDhivehiPdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetStaffConductFormAsync(id, asTracking: true, cancellationToken);
        var missingRequiredFields = GetMissingRequiredDhivehiFields(entity);
        if (missingRequiredFields.Count > 0)
        {
            throw new AppException($"Dhivehi export content is incomplete. Please complete: {string.Join(", ", missingRequiredFields)}.");
        }

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var existingDocument = await GetSavedDhivehiPdfAsync(id, asTracking: true, cancellationToken);
        var detail = MapDetail(entity, existingDocument);
        var dhivehiExport = BuildDhivehiExportDto(entity, existingDocument);
        var pdfBytes = _pdfExportService.BuildStaffConductFormDhivehiPdf(detail, dhivehiExport, settings.CompanyName, settings.BuildCompanyInfo(), settings.LogoUrl);
        var fileName = BuildDhivehiPdfFileName(entity);

        var document = existingDocument ?? new StaffConductExportDocument
        {
            StaffConductFormId = entity.Id,
            FormType = entity.FormType,
            Language = StaffConductExportLanguage.Dhivehi
        };

        document.FormType = entity.FormType;
        document.FileName = fileName;
        document.ContentType = PdfContentType;
        document.FileSizeBytes = pdfBytes.LongLength;
        document.Content = pdfBytes;
        document.ContentHash = ComputeContentHash(pdfBytes);

        if (existingDocument is null)
        {
            _dbContext.StaffConductExportDocuments.Add(document);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.StaffConductDhivehiPdfGenerated,
            nameof(StaffConductForm),
            entity.Id.ToString(),
            entity.FormNumber,
            new
            {
                entity.FormType,
                document.FileName,
                document.FileSizeBytes
            },
            cancellationToken);

        return new StaffConductExportFileDto
        {
            FileName = fileName,
            ContentType = PdfContentType,
            Content = pdfBytes
        };
    }

    public async Task<StaffConductExportFileDto> DownloadSavedDhivehiPdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await GetSavedDhivehiPdfAsync(id, asTracking: false, cancellationToken)
            ?? throw new NotFoundException("Saved Dhivehi PDF not found.");

        return new StaffConductExportFileDto
        {
            FileName = document.FileName,
            ContentType = string.IsNullOrWhiteSpace(document.ContentType) ? PdfContentType : document.ContentType,
            Content = document.Content
        };
    }

    public async Task<byte[]> ExportSummaryPdfAsync(StaffConductListQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await GetFilteredListItemsAsync(query, cancellationToken);
        var summary = BuildSummary(rows.Select(MapEntityFromListItem).ToList());
        var settings = await GetTenantSettingsAsync(cancellationToken);
        return _pdfExportService.BuildStaffConductSummaryPdf(rows, summary, settings.CompanyName, settings.BuildCompanyInfo(), settings.LogoUrl, query);
    }

    public async Task<byte[]> ExportSummaryExcelAsync(StaffConductListQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await GetFilteredListItemsAsync(query, cancellationToken);
        var summary = BuildSummary(rows.Select(MapEntityFromListItem).ToList());
        var settings = await GetTenantSettingsAsync(cancellationToken);
        return BuildSummaryExcel(rows, summary, query, settings.CompanyName, settings.BuildCompanyInfo());
    }

    private async Task<StaffConductForm> GetStaffConductFormAsync(Guid id, bool asTracking, CancellationToken cancellationToken)
    {
        var query = asTracking
            ? _dbContext.StaffConductForms
            : _dbContext.StaffConductForms.AsNoTracking();

        return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Disciplinary / warning form not found.");
    }

    private async Task<StaffConductExportDocument?> GetSavedDhivehiPdfAsync(Guid formId, bool asTracking, CancellationToken cancellationToken)
    {
        var query = asTracking
            ? _dbContext.StaffConductExportDocuments
            : _dbContext.StaffConductExportDocuments.AsNoTracking();

        return await query.FirstOrDefaultAsync(
            x => x.StaffConductFormId == formId && x.Language == StaffConductExportLanguage.Dhivehi,
            cancellationToken);
    }

    private async Task<List<StaffConductListItemDto>> GetFilteredListItemsAsync(StaffConductListQuery query, CancellationToken cancellationToken)
    {
        var filtered = ApplyListFilters(_dbContext.StaffConductForms.AsNoTracking(), query);
        filtered = ApplyOrdering(filtered, query);
        return await filtered.Select(x => MapListItem(x)).ToListAsync(cancellationToken);
    }

    private static IQueryable<StaffConductForm> ApplyListFilters(IQueryable<StaffConductForm> queryable, StaffConductListQuery query)
    {
        if (query.StaffId.HasValue)
        {
            queryable = queryable.Where(x => x.StaffId == query.StaffId.Value);
        }

        if (query.FormType.HasValue)
        {
            queryable = queryable.Where(x => x.FormType == query.FormType.Value);
        }

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(x => x.Status == query.Status.Value);
        }

        if (query.DateFrom.HasValue)
        {
            queryable = queryable.Where(x => x.IssueDate >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            queryable = queryable.Where(x => x.IssueDate <= query.DateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            queryable = queryable.Where(x =>
                x.FormNumber.ToLower().Contains(search)
                || x.StaffCodeSnapshot.ToLower().Contains(search)
                || x.StaffNameSnapshot.ToLower().Contains(search)
                || x.Subject.ToLower().Contains(search)
                || x.IssuedBy.ToLower().Contains(search)
                || (x.DesignationSnapshot != null && x.DesignationSnapshot.ToLower().Contains(search))
                || (x.WorkSiteSnapshot != null && x.WorkSiteSnapshot.ToLower().Contains(search)));
        }

        return queryable;
    }

    private static IQueryable<StaffConductForm> ApplyOrdering(IQueryable<StaffConductForm> queryable, StaffConductListQuery query)
    {
        var sortBy = query.SortBy?.Trim().ToLowerInvariant();
        var ascending = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);

        return sortBy switch
        {
            "staff" => ascending
                ? queryable.OrderBy(x => x.StaffNameSnapshot).ThenBy(x => x.IssueDate)
                : queryable.OrderByDescending(x => x.StaffNameSnapshot).ThenByDescending(x => x.IssueDate),
            "type" => ascending
                ? queryable.OrderBy(x => x.FormType).ThenBy(x => x.IssueDate)
                : queryable.OrderByDescending(x => x.FormType).ThenByDescending(x => x.IssueDate),
            "status" => ascending
                ? queryable.OrderBy(x => x.Status).ThenBy(x => x.IssueDate)
                : queryable.OrderByDescending(x => x.Status).ThenByDescending(x => x.IssueDate),
            _ => ascending
                ? queryable.OrderBy(x => x.IssueDate).ThenBy(x => x.FormNumber)
                : queryable.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.FormNumber)
        };
    }

    private static StaffConductSummaryDto BuildSummary(IReadOnlyCollection<StaffConductForm> rows)
    {
        return new StaffConductSummaryDto
        {
            TotalForms = rows.Count,
            WarningCount = rows.Count(x => x.FormType == StaffConductFormType.Warning),
            DisciplinaryCount = rows.Count(x => x.FormType == StaffConductFormType.Disciplinary),
            OpenCount = rows.Count(x => x.Status == StaffConductStatus.Open),
            AcknowledgedCount = rows.Count(x => x.Status == StaffConductStatus.Acknowledged),
            ResolvedCount = rows.Count(x => x.Status == StaffConductStatus.Resolved)
        };
    }

    private static StaffConductListItemDto MapListItem(StaffConductForm entity)
    {
        return new StaffConductListItemDto
        {
            Id = entity.Id,
            FormNumber = entity.FormNumber,
            FormType = entity.FormType,
            IssueDate = entity.IssueDate,
            IncidentDate = entity.IncidentDate,
            StaffId = entity.StaffId,
            StaffCode = entity.StaffCodeSnapshot,
            StaffName = entity.StaffNameSnapshot,
            Designation = entity.DesignationSnapshot,
            WorkSite = entity.WorkSiteSnapshot,
            Subject = entity.Subject,
            Severity = entity.Severity,
            Status = entity.Status,
            IssuedBy = entity.IssuedBy,
            IsAcknowledgedByStaff = entity.IsAcknowledgedByStaff,
            FollowUpDate = entity.FollowUpDate,
            ResolvedDate = entity.ResolvedDate
        };
    }

    private static StaffConductDetailDto MapDetail(StaffConductForm entity, StaffConductExportDocument? savedDhivehiPdf)
    {
        return new StaffConductDetailDto
        {
            Id = entity.Id,
            FormNumber = entity.FormNumber,
            FormType = entity.FormType,
            IssueDate = entity.IssueDate,
            IncidentDate = entity.IncidentDate,
            StaffId = entity.StaffId,
            StaffCode = entity.StaffCodeSnapshot,
            StaffName = entity.StaffNameSnapshot,
            Designation = entity.DesignationSnapshot,
            WorkSite = entity.WorkSiteSnapshot,
            IdNumber = entity.IdNumberSnapshot,
            Subject = entity.Subject,
            IncidentDetails = entity.IncidentDetails,
            ActionTaken = entity.ActionTaken,
            RequiredImprovement = entity.RequiredImprovement,
            Severity = entity.Severity,
            Status = entity.Status,
            IssuedBy = entity.IssuedBy,
            WitnessedBy = entity.WitnessedBy,
            FollowUpDate = entity.FollowUpDate,
            IsAcknowledgedByStaff = entity.IsAcknowledgedByStaff,
            AcknowledgedDate = entity.AcknowledgedDate,
            EmployeeRemarks = entity.EmployeeRemarks,
            ResolutionNotes = entity.ResolutionNotes,
            ResolvedDate = entity.ResolvedDate,
            HasDhivehiContent = HasDhivehiContent(entity),
            HasSavedDhivehiPdf = savedDhivehiPdf is not null,
            IsSavedDhivehiPdfStale = IsSavedPdfStale(entity, savedDhivehiPdf),
            DhivehiPdfFileName = savedDhivehiPdf?.FileName,
            DhivehiPdfUpdatedAt = savedDhivehiPdf?.UpdatedAt ?? savedDhivehiPdf?.CreatedAt
        };
    }

    private static StaffConductDhivehiExportDto BuildDhivehiExportDto(StaffConductForm entity, StaffConductExportDocument? savedDhivehiPdf)
    {
        return new StaffConductDhivehiExportDto
        {
            FormId = entity.Id,
            FormNumber = entity.FormNumber,
            FormType = entity.FormType,
            StaffId = entity.StaffId,
            StaffCode = entity.StaffCodeSnapshot,
            StaffName = entity.StaffNameSnapshot,
            Designation = entity.DesignationSnapshot,
            WorkSite = entity.WorkSiteSnapshot,
            IssueDate = entity.IssueDate,
            IncidentDate = entity.IncidentDate,
            Subject = entity.Subject,
            IncidentDetails = entity.IncidentDetails,
            ActionTaken = entity.ActionTaken,
            RequiredImprovement = entity.RequiredImprovement,
            EmployeeRemarks = entity.EmployeeRemarks,
            ResolutionNotes = entity.ResolutionNotes,
            AcknowledgementSource = BuildAcknowledgementSource(entity),
            SubjectDv = entity.SubjectDv,
            IncidentDetailsDv = entity.IncidentDetailsDv,
            ActionTakenDv = entity.ActionTakenDv,
            RequiredImprovementDv = entity.RequiredImprovementDv,
            EmployeeRemarksDv = entity.EmployeeRemarksDv,
            AcknowledgementDv = entity.AcknowledgementDv,
            ResolutionNotesDv = entity.ResolutionNotesDv,
            HasDhivehiContent = HasDhivehiContent(entity),
            HasSavedPdf = savedDhivehiPdf is not null,
            IsSavedPdfStale = IsSavedPdfStale(entity, savedDhivehiPdf),
            SavedPdfFileName = savedDhivehiPdf?.FileName,
            SavedPdfUpdatedAt = savedDhivehiPdf?.UpdatedAt ?? savedDhivehiPdf?.CreatedAt,
            MissingRequiredFields = GetMissingRequiredDhivehiFields(entity)
        };
    }

    private static IReadOnlyList<string> GetMissingRequiredDhivehiFields(StaffConductForm entity)
    {
        var missing = new List<string>();

        if (!string.IsNullOrWhiteSpace(entity.Subject) && string.IsNullOrWhiteSpace(entity.SubjectDv))
        {
            missing.Add("Subject");
        }

        if (!string.IsNullOrWhiteSpace(entity.IncidentDetails) && string.IsNullOrWhiteSpace(entity.IncidentDetailsDv))
        {
            missing.Add("Incident Details");
        }

        if (!string.IsNullOrWhiteSpace(entity.ActionTaken) && string.IsNullOrWhiteSpace(entity.ActionTakenDv))
        {
            missing.Add("Action Taken");
        }

        return missing;
    }

    private static bool HasDhivehiContent(StaffConductForm entity)
    {
        return !string.IsNullOrWhiteSpace(entity.SubjectDv)
            || !string.IsNullOrWhiteSpace(entity.IncidentDetailsDv)
            || !string.IsNullOrWhiteSpace(entity.ActionTakenDv)
            || !string.IsNullOrWhiteSpace(entity.RequiredImprovementDv)
            || !string.IsNullOrWhiteSpace(entity.EmployeeRemarksDv)
            || !string.IsNullOrWhiteSpace(entity.AcknowledgementDv)
            || !string.IsNullOrWhiteSpace(entity.ResolutionNotesDv);
    }

    private static bool IsSavedPdfStale(StaffConductForm entity, StaffConductExportDocument? savedDhivehiPdf)
    {
        if (savedDhivehiPdf is null)
        {
            return false;
        }

        var formTimestamp = entity.UpdatedAt ?? entity.CreatedAt;
        var documentTimestamp = savedDhivehiPdf.UpdatedAt ?? savedDhivehiPdf.CreatedAt;
        return formTimestamp > documentTimestamp;
    }

    private static string BuildAcknowledgementSource(StaffConductForm entity)
    {
        if (!entity.IsAcknowledgedByStaff)
        {
            return "This form is pending staff acknowledgement.";
        }

        return entity.AcknowledgedDate.HasValue
            ? $"This form has been acknowledged by the staff member on {entity.AcknowledgedDate.Value:yyyy-MM-dd}."
            : "This form has been acknowledged by the staff member.";
    }

    private static string BuildDhivehiPdfFileName(StaffConductForm entity)
    {
        return $"{entity.FormNumber}-dhivehi.pdf";
    }

    private static string ComputeContentHash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private static void ApplyStaffSnapshot(StaffConductForm entity, Staff staff)
    {
        entity.StaffCodeSnapshot = staff.StaffId;
        entity.StaffNameSnapshot = staff.StaffName;
        entity.DesignationSnapshot = NormalizeOptional(staff.Designation);
        entity.WorkSiteSnapshot = NormalizeOptional(staff.WorkSite);
        entity.IdNumberSnapshot = NormalizeOptional(staff.IdNumber);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private static string BuildFilterLabel(StaffConductListQuery query)
    {
        var parts = new List<string>();
        if (query.FormType.HasValue)
        {
            parts.Add($"Type: {query.FormType.Value}");
        }

        if (query.Status.HasValue)
        {
            parts.Add($"Status: {query.Status.Value}");
        }

        if (query.DateFrom.HasValue || query.DateTo.HasValue)
        {
            parts.Add($"Issue Date: {(query.DateFrom?.ToString("yyyy-MM-dd") ?? "Start")} to {(query.DateTo?.ToString("yyyy-MM-dd") ?? "Today")}");
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parts.Add($"Search: {query.Search.Trim()}");
        }

        return parts.Count == 0 ? "All disciplinary and warning forms" : string.Join(" | ", parts);
    }

    private static byte[] BuildDetailExcel(StaffConductDetailDto detail, string companyName, string companyInfo)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("HR Form");

        sheet.Cell(1, 1).Value = $"{companyName} - {detail.FormType} Form";
        sheet.Range(1, 1, 1, 6).Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell(2, 1).Value = companyInfo;
        sheet.Range(2, 1, 2, 6).Merge().Style.Font.SetFontSize(10).Font.SetFontColor(XLColor.FromHtml("#51648F"));
        sheet.Cell(3, 1).Value = $"Form No: {detail.FormNumber} | Generated: {DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5)):yyyy-MM-dd HH:mm} MVT";
        sheet.Range(3, 1, 3, 6).Merge().Style.Font.SetFontSize(10).Font.SetFontColor(XLColor.FromHtml("#51648F"));

        var row = 5;
        WriteField(sheet, row++, "Form Type", detail.FormType.ToString());
        WriteField(sheet, row++, "Issue Date", detail.IssueDate.ToString("yyyy-MM-dd"));
        WriteField(sheet, row++, "Incident Date", detail.IncidentDate.ToString("yyyy-MM-dd"));
        WriteField(sheet, row++, "Staff", $"{detail.StaffCode} - {detail.StaffName}");
        WriteField(sheet, row++, "Designation / Work Site", $"{detail.Designation ?? "-"} / {detail.WorkSite ?? "-"}");
        WriteField(sheet, row++, "ID Number", detail.IdNumber ?? "-");
        WriteField(sheet, row++, "Severity", detail.Severity.ToString());
        WriteField(sheet, row++, "Status", detail.Status.ToString());
        WriteField(sheet, row++, "Issued By", detail.IssuedBy);
        WriteField(sheet, row++, "Witnessed By", detail.WitnessedBy ?? "-");
        WriteField(sheet, row++, "Follow Up Date", detail.FollowUpDate?.ToString("yyyy-MM-dd") ?? "-");
        WriteField(sheet, row++, "Acknowledged", detail.IsAcknowledgedByStaff ? $"Yes ({detail.AcknowledgedDate:yyyy-MM-dd})" : "No");
        WriteField(sheet, row++, "Subject", detail.Subject);
        WriteField(sheet, row++, "Incident Details", detail.IncidentDetails, 2);
        WriteField(sheet, row++, "Action Taken", detail.ActionTaken, 2);
        WriteField(sheet, row++, "Required Improvement", detail.RequiredImprovement ?? "-", 2);
        WriteField(sheet, row++, "Employee Remarks", detail.EmployeeRemarks ?? "-", 2);
        WriteField(sheet, row++, "Resolution Notes", detail.ResolutionNotes ?? "-", 2);
        WriteField(sheet, row++, "Resolved Date", detail.ResolvedDate?.ToString("yyyy-MM-dd") ?? "-");

        sheet.Columns(1, 1).Width = 24;
        sheet.Columns(2, 6).Width = 18;
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildSummaryExcel(
        IReadOnlyList<StaffConductListItemDto> rows,
        StaffConductSummaryDto summary,
        StaffConductListQuery query,
        string companyName,
        string companyInfo)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("HR Forms");
        const int totalColumns = 10;

        sheet.Cell(1, 1).Value = $"{companyName} - Disciplinary & Warning Summary";
        sheet.Range(1, 1, 1, totalColumns).Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell(2, 1).Value = companyInfo;
        sheet.Range(2, 1, 2, totalColumns).Merge().Style.Font.SetFontSize(10).Font.SetFontColor(XLColor.FromHtml("#51648F"));
        sheet.Cell(3, 1).Value = BuildFilterLabel(query);
        sheet.Range(3, 1, 3, totalColumns).Merge().Style.Font.SetFontSize(10).Font.SetFontColor(XLColor.FromHtml("#51648F"));

        WriteSummaryTile(sheet, 5, 1, 2, "Total Forms", summary.TotalForms);
        WriteSummaryTile(sheet, 5, 3, 4, "Warnings", summary.WarningCount);
        WriteSummaryTile(sheet, 5, 5, 6, "Disciplinary", summary.DisciplinaryCount);
        WriteSummaryTile(sheet, 5, 7, 8, "Open", summary.OpenCount);
        WriteSummaryTile(sheet, 5, 9, 10, "Resolved", summary.ResolvedCount);

        var headerRow = 8;
        var headers = new[]
        {
            "Form No", "Type", "Issue Date", "Staff ID", "Staff Name", "Designation", "Subject", "Severity", "Status", "Issued By"
        };

        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(headerRow, index + 1).Value = headers[index];
        }

        var headerRange = sheet.Range(headerRow, 1, headerRow, totalColumns);
        headerRange.Style.Font.SetBold();
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEFF");
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var row = headerRow + 1;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.FormNumber;
            sheet.Cell(row, 2).Value = item.FormType.ToString();
            sheet.Cell(row, 3).Value = item.IssueDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 4).Value = item.StaffCode;
            sheet.Cell(row, 5).Value = item.StaffName;
            sheet.Cell(row, 6).Value = item.Designation ?? "-";
            sheet.Cell(row, 7).Value = item.Subject;
            sheet.Cell(row, 8).Value = item.Severity.ToString();
            sheet.Cell(row, 9).Value = item.Status.ToString();
            sheet.Cell(row, 10).Value = item.IssuedBy;
            row++;
        }

        if (rows.Count > 0)
        {
            var dataRange = sheet.Range(headerRow + 1, 1, row - 1, totalColumns);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            sheet.Range(headerRow, 1, row - 1, totalColumns).SetAutoFilter();
        }

        sheet.Column(3).Style.DateFormat.Format = "yyyy-MM-dd";
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteField(IXLWorksheet sheet, int row, string label, string value, int contentRowSpan = 1)
    {
        var labelCell = sheet.Cell(row, 1);
        labelCell.Value = label;
        labelCell.Style.Font.SetBold();
        labelCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF4FF");
        labelCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        labelCell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CFDCF6");

        var contentRange = sheet.Range(row, 2, row + contentRowSpan - 1, 6);
        contentRange.Merge();
        contentRange.Value = value;
        contentRange.Style.Alignment.WrapText = true;
        contentRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        contentRange.Style.Fill.BackgroundColor = XLColor.White;
        contentRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        contentRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E0E8F8");
    }

    private static void WriteSummaryTile(IXLWorksheet sheet, int row, int startColumn, int endColumn, string label, int value)
    {
        var range = sheet.Range(row, startColumn, row + 1, endColumn);
        range.Merge();
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF4FF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CFDCF6");
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var text = sheet.Cell(row, startColumn).GetRichText();
        text.ClearText();
        text.AddText(label.ToUpperInvariant()).SetBold().SetFontSize(9).SetFontColor(XLColor.FromHtml("#5D71A5"));
        text.AddText(Environment.NewLine);
        text.AddText(value.ToString("N0")).SetBold().SetFontSize(15).SetFontColor(XLColor.FromHtml("#243B6B"));
    }

    private static StaffConductForm MapEntityFromListItem(StaffConductListItemDto item)
    {
        return new StaffConductForm
        {
            Id = item.Id,
            FormNumber = item.FormNumber,
            FormType = item.FormType,
            IssueDate = item.IssueDate,
            IncidentDate = item.IncidentDate,
            StaffId = item.StaffId,
            StaffCodeSnapshot = item.StaffCode,
            StaffNameSnapshot = item.StaffName,
            DesignationSnapshot = item.Designation,
            WorkSiteSnapshot = item.WorkSite,
            Subject = item.Subject,
            Severity = item.Severity,
            Status = item.Status,
            IssuedBy = item.IssuedBy,
            IsAcknowledgedByStaff = item.IsAcknowledgedByStaff,
            FollowUpDate = item.FollowUpDate,
            ResolvedDate = item.ResolvedDate
        };
    }
}
