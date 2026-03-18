using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;
using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Constants;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Persistence;
using MyDhathuru.Infrastructure.Security;

namespace MyDhathuru.Infrastructure.Services;

public class PortalAdminService : IPortalAdminService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPortalAdminDemoDataService _portalAdminDemoDataService;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PortalAdminService> _logger;

    public PortalAdminService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IPortalAdminDemoDataService portalAdminDemoDataService,
        IJwtTokenGenerator tokenGenerator,
        INotificationService notificationService,
        ILogger<PortalAdminService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _portalAdminDemoDataService = portalAdminDemoDataService;
        _tokenGenerator = tokenGenerator;
        _notificationService = notificationService;
        _logger = logger;
    }

    public Task<PortalAdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
        => GetDashboardInternalAsync(cancellationToken);

    public Task<PagedResult<SignupRequestListItemDto>> GetSignupRequestsAsync(SignupRequestListQuery query, CancellationToken cancellationToken = default)
        => GetSignupRequestsInternalAsync(query, cancellationToken);

    public Task<SignupRequestCountsDto> GetSignupRequestCountsAsync(CancellationToken cancellationToken = default)
        => GetSignupRequestCountsInternalAsync(cancellationToken);

    public Task<SignupRequestDetailDto> GetSignupRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default)
        => GetSignupRequestByIdInternalAsync(requestId, cancellationToken);

    public Task ApproveSignupRequestAsync(Guid requestId, ApproveSignupRequest request, CancellationToken cancellationToken = default)
        => ApproveSignupRequestInternalAsync(requestId, request, cancellationToken);

    public Task RejectSignupRequestAsync(Guid requestId, RejectSignupRequest request, CancellationToken cancellationToken = default)
        => RejectSignupRequestInternalAsync(requestId, request, cancellationToken);

    public Task<PagedResult<PortalAdminBusinessListItemDto>> GetBusinessesAsync(PortalAdminBusinessListQuery query, CancellationToken cancellationToken = default)
        => GetBusinessesInternalAsync(query, cancellationToken);

    public Task<PortalAdminBusinessDetailDto> GetBusinessByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => GetBusinessByIdInternalAsync(tenantId, cancellationToken);

    public Task DisableBusinessAsync(Guid tenantId, PortalAdminSetBusinessStatusRequest request, CancellationToken cancellationToken = default)
        => DisableBusinessInternalAsync(tenantId, request, cancellationToken);

    public Task EnableBusinessAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => EnableBusinessInternalAsync(tenantId, cancellationToken);

    public Task SetBusinessDataTestingAsync(Guid tenantId, PortalAdminSetBusinessDataTestingRequest request, CancellationToken cancellationToken = default)
        => SetBusinessDataTestingInternalAsync(tenantId, request, cancellationToken);

    public Task<PortalAdminDemoDataSeedResultDto> GenerateBusinessDemoDataAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => GenerateBusinessDemoDataInternalAsync(tenantId, cancellationToken);

    public Task DeleteBusinessPermanentlyAsync(Guid tenantId, PortalAdminDeleteBusinessRequest request, CancellationToken cancellationToken = default)
        => DeleteBusinessPermanentlyInternalAsync(tenantId, request, cancellationToken);

    public Task UpdateBusinessLoginDetailsAsync(Guid tenantId, PortalAdminUpdateBusinessLoginRequest request, CancellationToken cancellationToken = default)
        => UpdateBusinessLoginDetailsInternalAsync(tenantId, request, cancellationToken);

    public Task SendBusinessPasswordResetLinkAsync(Guid tenantId, PortalAdminSendResetLinkRequest request, CancellationToken cancellationToken = default)
        => SendBusinessPasswordResetLinkInternalAsync(tenantId, request, cancellationToken);

    public Task<PagedResult<PortalAdminBusinessUserDto>> GetBusinessUsersAsync(PortalAdminBusinessUsersQuery query, CancellationToken cancellationToken = default)
        => GetBusinessUsersInternalAsync(query, cancellationToken);

    public Task<PagedResult<PortalAdminAuditLogDto>> GetAuditLogsAsync(PortalAdminAuditLogQuery query, CancellationToken cancellationToken = default)
        => GetAuditLogsInternalAsync(query, cancellationToken);

    private async Task<PortalAdminDashboardDto> GetDashboardInternalAsync(CancellationToken cancellationToken)
    {
        await EnsureSuperAdminAsync(cancellationToken);

        var superAdminTenantIds = SuperAdminTenantIdsQuery();
        var allBusinessesQuery = _dbContext.Tenants.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && !superAdminTenantIds.Contains(x.Id));
        var businessesQuery = allBusinessesQuery.Where(x => !x.IsDataTesting);
        var totalBusinesses = await businessesQuery.CountAsync(cancellationToken);
        var excludedDataTestingBusinesses = await allBusinessesQuery.CountAsync(x => x.IsDataTesting, cancellationToken);
        var activeBusinesses = await businessesQuery.CountAsync(x => x.IsActive && x.AccountStatus == BusinessAccountStatus.Active, cancellationToken);
        var visibleTenantIds = businessesQuery.Select(x => x.Id);

        var pendingSignupRequests = await _dbContext.SignupRequests.IgnoreQueryFilters()
            .CountAsync(x => !x.IsDeleted && x.Status == SignupRequestStatus.Pending, cancellationToken);

        var recentRequests = await _dbContext.SignupRequests.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.SubmittedAt)
            .Take(6)
            .Select(x => new PortalAdminRecentSignupRequestDto
            {
                Id = x.Id,
                RequestDate = x.SubmittedAt,
                CompanyName = x.CompanyName,
                RequestedByName = x.RequestedByName,
                Status = x.Status
            })
            .ToListAsync(cancellationToken);

        var recentLogs = await BuildAuditLogDtosAsync(
            _dbContext.AdminAuditLogs.IgnoreQueryFilters()
                .Where(x => !x.IsDeleted && (!x.RelatedTenantId.HasValue || visibleTenantIds.Contains(x.RelatedTenantId.Value)))
                .OrderByDescending(x => x.PerformedAt)
                .Take(5),
            cancellationToken);

        return new PortalAdminDashboardDto
        {
            TotalBusinesses = totalBusinesses,
            ExcludedDataTestingBusinesses = excludedDataTestingBusinesses,
            PendingSignupRequests = pendingSignupRequests,
            ActiveBusinesses = activeBusinesses,
            DisabledBusinesses = totalBusinesses - activeBusinesses,
            TotalStaffAcrossBusinesses = await _dbContext.Staff.IgnoreQueryFilters().CountAsync(x => !x.IsDeleted && visibleTenantIds.Contains(x.TenantId), cancellationToken),
            TotalVesselsAcrossBusinesses = await _dbContext.Vessels.IgnoreQueryFilters().CountAsync(x => !x.IsDeleted && visibleTenantIds.Contains(x.TenantId), cancellationToken),
            RecentSignupRequests = recentRequests,
            RecentActions = recentLogs
        };
    }

    private async Task<PagedResult<SignupRequestListItemDto>> GetSignupRequestsInternalAsync(SignupRequestListQuery query, CancellationToken cancellationToken)
    {
        await EnsureSuperAdminAsync(cancellationToken);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestsQuery = _dbContext.SignupRequests.IgnoreQueryFilters().Where(x => !x.IsDeleted);

        if (query.Status.HasValue)
        {
            requestsQuery = requestsQuery.Where(x => x.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            requestsQuery = requestsQuery.Where(x => x.SubmittedAt >= query.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        if (query.ToDate.HasValue)
        {
            requestsQuery = requestsQuery.Where(x => x.SubmittedAt < query.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        var search = query.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            requestsQuery = requestsQuery.Where(x =>
                x.CompanyName.ToLower().Contains(search)
                || x.CompanyEmail.ToLower().Contains(search)
                || x.RequestedByName.ToLower().Contains(search)
                || x.RequestedByEmail.ToLower().Contains(search)
                || x.BusinessRegistrationNumber.ToLower().Contains(search));
        }

        var totalCount = await requestsQuery.CountAsync(cancellationToken);
        var items = await requestsQuery
            .OrderByDescending(x => x.SubmittedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SignupRequestListItemDto
            {
                Id = x.Id,
                RequestDate = x.SubmittedAt,
                CompanyName = x.CompanyName,
                CompanyEmail = x.CompanyEmail,
                CompanyPhone = x.CompanyPhone,
                TinNumber = x.TinNumber,
                BusinessRegistrationNumber = x.BusinessRegistrationNumber,
                RequestedByName = x.RequestedByName,
                RequestedByEmail = x.RequestedByEmail,
                Status = x.Status
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SignupRequestListItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private async Task<SignupRequestCountsDto> GetSignupRequestCountsInternalAsync(CancellationToken cancellationToken)
    {
        await EnsureSuperAdminAsync(cancellationToken);
        var query = _dbContext.SignupRequests.IgnoreQueryFilters().Where(x => !x.IsDeleted);

        return new SignupRequestCountsDto
        {
            Pending = await query.CountAsync(x => x.Status == SignupRequestStatus.Pending, cancellationToken),
            Accepted = await query.CountAsync(x => x.Status == SignupRequestStatus.Accepted, cancellationToken),
            Rejected = await query.CountAsync(x => x.Status == SignupRequestStatus.Rejected, cancellationToken)
        };
    }

    private async Task<SignupRequestDetailDto> GetSignupRequestByIdInternalAsync(Guid requestId, CancellationToken cancellationToken)
    {
        await EnsureSuperAdminAsync(cancellationToken);

        var request = await _dbContext.SignupRequests.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == requestId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Signup request not found.");

        string? reviewedByName = null;
        if (request.ReviewedByUserId.HasValue)
        {
            reviewedByName = await _dbContext.Users.IgnoreQueryFilters()
                .Where(x => x.Id == request.ReviewedByUserId.Value)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new SignupRequestDetailDto
        {
            Id = request.Id,
            RequestDate = request.SubmittedAt,
            CompanyName = request.CompanyName,
            CompanyEmail = request.CompanyEmail,
            CompanyPhone = request.CompanyPhone,
            TinNumber = request.TinNumber,
            BusinessRegistrationNumber = request.BusinessRegistrationNumber,
            RequestedByName = request.RequestedByName,
            RequestedByEmail = request.RequestedByEmail,
            Status = request.Status,
            RejectionReason = request.RejectionReason,
            ReviewNotes = request.ReviewNotes,
            ReviewedAt = request.ReviewedAt,
            ReviewedByUserId = request.ReviewedByUserId,
            ReviewedByUserName = reviewedByName,
            ApprovedTenantId = request.ApprovedTenantId
        };
    }

    private async Task ApproveSignupRequestInternalAsync(Guid requestId, ApproveSignupRequest request, CancellationToken cancellationToken)
    {
        var reviewer = await EnsureSuperAdminAsync(cancellationToken);
        var signupRequest = await _dbContext.SignupRequests.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == requestId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Signup request not found.");

        if (signupRequest.Status != SignupRequestStatus.Pending)
        {
            throw new AppException("Only pending signup requests can be approved.");
        }

        var companyEmail = signupRequest.CompanyEmail.Trim().ToLowerInvariant();
        var adminEmail = signupRequest.RequestedByEmail.Trim().ToLowerInvariant();
        var businessRegistration = signupRequest.BusinessRegistrationNumber.Trim().ToLowerInvariant();

        var tenantExists = await _dbContext.Tenants.IgnoreQueryFilters().AnyAsync(
            x => !x.IsDeleted
                && (x.CompanyEmail.ToLower() == companyEmail
                    || x.BusinessRegistrationNumber.ToLower() == businessRegistration),
            cancellationToken);
        if (tenantExists)
        {
            throw new AppException("A business with the same company email or registration number already exists.");
        }

        var userExists = await _dbContext.Users.IgnoreQueryFilters().AnyAsync(
            x => !x.IsDeleted && x.Email == adminEmail,
            cancellationToken);
        if (userExists)
        {
            throw new AppException("A user with the requested admin email already exists.");
        }

        var adminRole = await EnsureRoleAsync(UserRoleName.Admin, "Tenant administrator", cancellationToken);
        await EnsureRoleAsync(UserRoleName.Staff, "Tenant staff user", cancellationToken);

        var tenant = new Tenant
        {
            CompanyName = signupRequest.CompanyName.Trim(),
            CompanyEmail = signupRequest.CompanyEmail.Trim(),
            CompanyPhone = signupRequest.CompanyPhone.Trim(),
            TinNumber = signupRequest.TinNumber.Trim(),
            BusinessRegistrationNumber = signupRequest.BusinessRegistrationNumber.Trim(),
            IsActive = true,
            AccountStatus = BusinessAccountStatus.Active,
            ApprovedAt = DateTimeOffset.UtcNow,
            ApprovedByUserId = reviewer.Id
        };

        var user = new User
        {
            Tenant = tenant,
            Role = adminRole,
            FullName = signupRequest.RequestedByName.Trim(),
            Email = adminEmail,
            PasswordHash = signupRequest.PasswordHash,
            PasswordSalt = signupRequest.PasswordSalt,
            IsActive = true
        };

        signupRequest.Status = SignupRequestStatus.Accepted;
        signupRequest.RejectionReason = null;
        signupRequest.ReviewedAt = DateTimeOffset.UtcNow;
        signupRequest.ReviewedByUserId = reviewer.Id;
        signupRequest.ReviewNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.TenantSettings.Add(CreateDefaultTenantSettings(tenant, user));
        _dbContext.ExpenseCategories.AddRange(ExpenseCategoryService.BuildDefaultCategories(tenant.Id));

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        signupRequest.ApprovedTenantId = tenant.Id;

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            reviewer.Id,
            AdminAuditActionType.SignupRequestApproved,
            nameof(SignupRequest),
            signupRequest.Id.ToString(),
            signupRequest.CompanyName,
            tenant.Id,
            new
            {
                signupRequest.CompanyEmail,
                signupRequest.RequestedByEmail,
                request.Notes
            }));

        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await SendSignupAcceptedNotificationsAsync(signupRequest, cancellationToken);
    }

    private async Task RejectSignupRequestInternalAsync(Guid requestId, RejectSignupRequest request, CancellationToken cancellationToken)
    {
        var reviewer = await EnsureSuperAdminAsync(cancellationToken);
        var signupRequest = await _dbContext.SignupRequests.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == requestId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Signup request not found.");

        if (signupRequest.Status != SignupRequestStatus.Pending)
        {
            throw new AppException("Only pending signup requests can be rejected.");
        }

        signupRequest.Status = SignupRequestStatus.Rejected;
        signupRequest.RejectionReason = request.RejectionReason.Trim();
        signupRequest.ReviewedAt = DateTimeOffset.UtcNow;
        signupRequest.ReviewedByUserId = reviewer.Id;
        signupRequest.ReviewNotes = null;

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            reviewer.Id,
            AdminAuditActionType.SignupRequestRejected,
            nameof(SignupRequest),
            signupRequest.Id.ToString(),
            signupRequest.CompanyName,
            null,
            new { request.RejectionReason, signupRequest.CompanyEmail }));

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await SendSignupRejectedNotificationsAsync(signupRequest, request.RejectionReason.Trim(), cancellationToken);
    }

    private async Task<PagedResult<PortalAdminBusinessListItemDto>> GetBusinessesInternalAsync(PortalAdminBusinessListQuery query, CancellationToken cancellationToken)
    {
        await EnsureSuperAdminAsync(cancellationToken);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var superAdminTenantIds = SuperAdminTenantIdsQuery();
        var tenantsQuery = _dbContext.Tenants.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && !superAdminTenantIds.Contains(x.Id));

        if (query.Status.HasValue)
        {
            tenantsQuery = query.Status == BusinessAccountStatus.Active
                ? tenantsQuery.Where(x => x.IsActive && x.AccountStatus == BusinessAccountStatus.Active)
                : tenantsQuery.Where(x => !x.IsActive || x.AccountStatus == BusinessAccountStatus.Disabled);
        }

        if (query.IsDataTesting.HasValue)
        {
            tenantsQuery = tenantsQuery.Where(x => x.IsDataTesting == query.IsDataTesting.Value);
        }

        var search = query.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            tenantsQuery = tenantsQuery.Where(x =>
                x.CompanyName.ToLower().Contains(search)
                || x.CompanyEmail.ToLower().Contains(search)
                || x.CompanyPhone.ToLower().Contains(search)
                || x.TinNumber.ToLower().Contains(search)
                || x.BusinessRegistrationNumber.ToLower().Contains(search));
        }

        var totalCount = await tenantsQuery.CountAsync(cancellationToken);
        var tenants = await tenantsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var tenantIds = tenants.Select(x => x.Id).ToArray();
        var staffCounts = await _dbContext.Staff.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(x => new { TenantId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var vesselCounts = await _dbContext.Vessels.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(x => new { TenantId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var lastActivity = await _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && tenantIds.Contains(x.TenantId) && x.Role.Name != UserRoleName.SuperAdmin)
            .GroupBy(x => x.TenantId)
            .Select(x => new { TenantId = x.Key, LastActivity = x.Max(u => u.LastLoginAt) })
            .ToDictionaryAsync(x => x.TenantId, x => x.LastActivity, cancellationToken);

        var items = tenants.Select(tenant => new PortalAdminBusinessListItemDto
        {
            TenantId = tenant.Id,
            CompanyName = tenant.CompanyName,
            CompanyEmail = tenant.CompanyEmail,
            CompanyPhone = tenant.CompanyPhone,
            TinNumber = tenant.TinNumber,
            BusinessRegistrationNumber = tenant.BusinessRegistrationNumber,
            Status = ResolveStatus(tenant),
            IsDataTesting = tenant.IsDataTesting,
            StaffCount = staffCounts.GetValueOrDefault(tenant.Id),
            VesselCount = vesselCounts.GetValueOrDefault(tenant.Id),
            CreatedAt = tenant.CreatedAt,
            LastActivityAt = lastActivity.GetValueOrDefault(tenant.Id),
            DemoDataGeneratedAt = tenant.DemoDataGeneratedAt
        }).ToList();

        return new PagedResult<PortalAdminBusinessListItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private async Task<PortalAdminBusinessDetailDto> GetBusinessByIdInternalAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureSuperAdminAsync(cancellationToken);
        var superAdminTenantIds = SuperAdminTenantIdsQuery();

        var tenant = await _dbContext.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted && !superAdminTenantIds.Contains(x.Id), cancellationToken)
            ?? throw new NotFoundException("Business not found.");

        var primaryAdmin = await _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.TenantId == tenantId && x.Role.Name == UserRoleName.Admin)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new PortalAdminBusinessUserDto
            {
                Id = x.Id,
                TenantId = x.TenantId,
                CompanyName = tenant.CompanyName,
                FullName = x.FullName,
                Email = x.Email,
                Role = x.Role.Name,
                IsActive = x.IsActive,
                LastLoginAt = x.LastLoginAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new PortalAdminBusinessDetailDto
        {
            TenantId = tenant.Id,
            CompanyName = tenant.CompanyName,
            CompanyEmail = tenant.CompanyEmail,
            CompanyPhone = tenant.CompanyPhone,
            TinNumber = tenant.TinNumber,
            BusinessRegistrationNumber = tenant.BusinessRegistrationNumber,
            Status = ResolveStatus(tenant),
            IsDataTesting = tenant.IsDataTesting,
            StaffCount = await _dbContext.Staff.IgnoreQueryFilters().CountAsync(x => !x.IsDeleted && x.TenantId == tenantId, cancellationToken),
            VesselCount = await _dbContext.Vessels.IgnoreQueryFilters().CountAsync(x => !x.IsDeleted && x.TenantId == tenantId, cancellationToken),
            CreatedAt = tenant.CreatedAt,
            LastActivityAt = await _dbContext.Users.IgnoreQueryFilters()
                .Where(x => !x.IsDeleted && x.TenantId == tenantId && x.Role.Name != UserRoleName.SuperAdmin)
                .MaxAsync(x => x.LastLoginAt, cancellationToken),
            DemoDataGeneratedAt = tenant.DemoDataGeneratedAt,
            ApprovedAt = tenant.ApprovedAt,
            DisabledReason = tenant.DisabledReason,
            DisabledAt = tenant.DisabledAt,
            CustomerCount = await _dbContext.Customers.IgnoreQueryFilters().CountAsync(x => !x.IsDeleted && x.TenantId == tenantId, cancellationToken),
            InvoiceCount = await _dbContext.Invoices.IgnoreQueryFilters().CountAsync(x => !x.IsDeleted && x.TenantId == tenantId, cancellationToken),
            PrimaryAdmin = primaryAdmin
        };
    }

    private async Task DisableBusinessInternalAsync(Guid tenantId, PortalAdminSetBusinessStatusRequest request, CancellationToken cancellationToken)
    {
        var reviewer = await EnsureSuperAdminAsync(cancellationToken);
        var superAdminTenantIds = SuperAdminTenantIdsQuery();
        var tenant = await _dbContext.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted && !superAdminTenantIds.Contains(x.Id), cancellationToken)
            ?? throw new NotFoundException("Business not found.");

        tenant.IsActive = false;
        tenant.AccountStatus = BusinessAccountStatus.Disabled;
        tenant.DisabledAt = DateTimeOffset.UtcNow;
        tenant.DisabledByUserId = reviewer.Id;
        tenant.DisabledReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            reviewer.Id,
            AdminAuditActionType.BusinessDisabled,
            nameof(Tenant),
            tenant.Id.ToString(),
            tenant.CompanyName,
            tenant.Id,
            new { tenant.DisabledReason }));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnableBusinessInternalAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var reviewer = await EnsureSuperAdminAsync(cancellationToken);
        var superAdminTenantIds = SuperAdminTenantIdsQuery();
        var tenant = await _dbContext.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted && !superAdminTenantIds.Contains(x.Id), cancellationToken)
            ?? throw new NotFoundException("Business not found.");

        tenant.IsActive = true;
        tenant.AccountStatus = BusinessAccountStatus.Active;
        tenant.DisabledReason = null;
        tenant.DisabledAt = null;
        tenant.DisabledByUserId = null;

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            reviewer.Id,
            AdminAuditActionType.BusinessEnabled,
            nameof(Tenant),
            tenant.Id.ToString(),
            tenant.CompanyName,
            tenant.Id));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    private async Task SetBusinessDataTestingInternalAsync(Guid tenantId, PortalAdminSetBusinessDataTestingRequest request, CancellationToken cancellationToken)
    {
        var reviewer = await EnsureSuperAdminAsync(cancellationToken);
        var superAdminTenantIds = SuperAdminTenantIdsQuery();
        var tenant = await _dbContext.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted && !superAdminTenantIds.Contains(x.Id), cancellationToken)
            ?? throw new NotFoundException("Business not found.");

        if (tenant.IsDataTesting == request.IsDataTesting)
        {
            return;
        }

        tenant.IsDataTesting = request.IsDataTesting;

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            reviewer.Id,
            request.IsDataTesting ? AdminAuditActionType.BusinessMarkedDataTesting : AdminAuditActionType.BusinessUnmarkedDataTesting,
            nameof(Tenant),
            tenant.Id.ToString(),
            tenant.CompanyName,
            tenant.Id,
            new { tenant.IsDataTesting, tenant.DemoDataGeneratedAt }));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<PortalAdminDemoDataSeedResultDto> GenerateBusinessDemoDataInternalAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var reviewer = await EnsureSuperAdminAsync(cancellationToken);
        var superAdminTenantIds = SuperAdminTenantIdsQuery();
        var tenant = await _dbContext.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted && !superAdminTenantIds.Contains(x.Id), cancellationToken)
            ?? throw new NotFoundException("Business not found.");

        if (!tenant.IsDataTesting)
        {
            throw new AppException("Only data testing businesses can be seeded with demo data.");
        }

        var result = await _portalAdminDemoDataService.SeedBusinessDemoDataAsync(tenantId, reviewer.Id, cancellationToken);

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            reviewer.Id,
            AdminAuditActionType.BusinessDemoDataGenerated,
            nameof(Tenant),
            tenant.Id.ToString(),
            tenant.CompanyName,
            tenant.Id,
            result));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task DeleteBusinessPermanentlyInternalAsync(Guid tenantId, PortalAdminDeleteBusinessRequest request, CancellationToken cancellationToken)
    {
        var reviewer = await EnsureSuperAdminAsync(cancellationToken);
        var superAdminTenantIds = SuperAdminTenantIdsQuery();
        var tenant = await _dbContext.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted && !superAdminTenantIds.Contains(x.Id), cancellationToken)
            ?? throw new NotFoundException("Business not found.");

        if (!string.Equals(
                tenant.CompanyName.Trim(),
                request.CompanyNameConfirmation.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException("Company name confirmation does not match the selected business.");
        }

        var tenantUserIds = _dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Id);

        var adminInvoiceIds = _dbContext.AdminInvoices.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Id);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await RemoveTenantDocumentLinksAsync(tenantId, cancellationToken);

        await _dbContext.AdminInvoiceEmailLogs.IgnoreQueryFilters()
            .Where(x => adminInvoiceIds.Contains(x.AdminInvoiceId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.AdminInvoiceLineItems.IgnoreQueryFilters()
            .Where(x => adminInvoiceIds.Contains(x.AdminInvoiceId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.RefreshTokens.IgnoreQueryFilters()
            .Where(x => tenantUserIds.Contains(x.UserId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.PasswordResetTokens.IgnoreQueryFilters()
            .Where(x => tenantUserIds.Contains(x.UserId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.AdminEmailCampaignRecipients.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await DeleteTenantRowsAsync<SalarySlip>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<StaffConductForm>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<PayrollEntry>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<ReceivedInvoicePayment>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<ReceivedInvoiceAttachment>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<ReceivedInvoiceItem>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<InvoicePayment>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<InvoiceItem>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<QuotationItem>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<PurchaseOrderItem>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<DeliveryNoteItem>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<CustomerContact>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<CustomerOpeningBalance>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<BptAdjustment>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<BptMappingRule>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<PaymentVoucher>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<ExpenseEntry>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<RentEntry>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<SalesAdjustment>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<OtherIncomeEntry>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<Invoice>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<Quotation>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<PurchaseOrder>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<DeliveryNote>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<ReceivedInvoice>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<PayrollPeriod>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<Staff>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<Customer>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<Supplier>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<Vessel>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<ExchangeRate>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<DocumentSequence>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<ExpenseCategory>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<BptCategory>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<BusinessAuditLog>(tenantId, cancellationToken);
        await DeleteTenantRowsAsync<TenantSettings>(tenantId, cancellationToken);

        await _dbContext.AdminInvoices.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.BusinessCustomRates.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.AdminAuditLogs.IgnoreQueryFilters()
            .Where(x => x.RelatedTenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.SignupRequests.IgnoreQueryFilters()
            .Where(x => x.ApprovedTenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Tenants.IgnoreQueryFilters()
            .Where(x => x.Id == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogWarning(
            "Portal admin {ReviewerUserId} permanently deleted business {TenantId} ({CompanyName}) from the database.",
            reviewer.Id,
            tenantId,
            tenant.CompanyName);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task UpdateBusinessLoginDetailsInternalAsync(Guid tenantId, PortalAdminUpdateBusinessLoginRequest request, CancellationToken cancellationToken)
    {
        var reviewer = await EnsureSuperAdminAsync(cancellationToken);
        var superAdminTenantIds = SuperAdminTenantIdsQuery();
        var tenant = await _dbContext.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted && !superAdminTenantIds.Contains(x.Id), cancellationToken)
            ?? throw new NotFoundException("Business not found.");

        var adminUser = await _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.TenantId == tenantId && x.Role.Name == UserRoleName.Admin)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Business admin user not found.");

        var nextAdminEmail = request.AdminLoginEmail.Trim().ToLowerInvariant();
        var emailExists = await _dbContext.Users.IgnoreQueryFilters()
            .AnyAsync(x => !x.IsDeleted && x.Id != adminUser.Id && x.Email == nextAdminEmail, cancellationToken);
        if (emailExists)
        {
            throw new AppException("Admin login email already exists.");
        }

        adminUser.FullName = request.AdminFullName.Trim();
        adminUser.Email = nextAdminEmail;
        tenant.CompanyEmail = request.CompanyEmail.Trim();
        tenant.CompanyPhone = request.CompanyPhone.Trim();

        var settings = await _dbContext.TenantSettings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        if (settings is not null)
        {
            settings.CompanyName = tenant.CompanyName;
            settings.CompanyEmail = tenant.CompanyEmail;
            settings.CompanyPhone = tenant.CompanyPhone;
            if (string.IsNullOrWhiteSpace(settings.Username))
            {
                settings.Username = adminUser.FullName;
            }
        }

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            reviewer.Id,
            AdminAuditActionType.BusinessLoginUpdated,
            nameof(Tenant),
            tenant.Id.ToString(),
            tenant.CompanyName,
            tenant.Id,
            new { adminUser.FullName, adminUser.Email, tenant.CompanyEmail, tenant.CompanyPhone }));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SendBusinessPasswordResetLinkInternalAsync(Guid tenantId, PortalAdminSendResetLinkRequest request, CancellationToken cancellationToken)
    {
        var reviewer = await EnsureSuperAdminAsync(cancellationToken);
        var superAdminTenantIds = SuperAdminTenantIdsQuery();
        var tenant = await _dbContext.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted && !superAdminTenantIds.Contains(x.Id), cancellationToken)
            ?? throw new NotFoundException("Business not found.");

        var preferredEmail = request.AdminEmail?.Trim().ToLowerInvariant();
        var adminUsersQuery = _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.TenantId == tenantId && x.Role.Name == UserRoleName.Admin && x.IsActive);

        var adminUser = string.IsNullOrWhiteSpace(preferredEmail)
            ? await adminUsersQuery.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            : await adminUsersQuery.FirstOrDefaultAsync(x => x.Email == preferredEmail, cancellationToken);

        adminUser ??= await adminUsersQuery.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (adminUser is null)
        {
            throw new NotFoundException("Business admin user not found.");
        }

        var token = _tokenGenerator.GenerateRefreshToken();
        var tokenHash = _tokenGenerator.HashToken(token);

        var activeTokens = await _dbContext.PasswordResetTokens.IgnoreQueryFilters()
            .Where(x => x.UserId == adminUser.Id && x.UsedAt == null && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var active in activeTokens)
        {
            active.UsedAt = DateTimeOffset.UtcNow;
        }

        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = adminUser.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        });

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            reviewer.Id,
            AdminAuditActionType.BusinessPasswordResetSent,
            nameof(User),
            adminUser.Id.ToString(),
            adminUser.Email,
            tenant.Id,
            new { tenant.CompanyName, adminUser.Email }));

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _notificationService.SendPasswordResetAsync(adminUser.Email, token, isPortalAdmin: false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<PagedResult<PortalAdminBusinessUserDto>> GetBusinessUsersInternalAsync(PortalAdminBusinessUsersQuery query, CancellationToken cancellationToken)
    {
        await EnsureSuperAdminAsync(cancellationToken);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var usersQuery = _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.Role.Name != UserRoleName.SuperAdmin);

        if (query.TenantId.HasValue)
        {
            usersQuery = usersQuery.Where(x => x.TenantId == query.TenantId.Value);
        }

        var search = query.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            usersQuery = usersQuery.Where(x =>
                x.FullName.ToLower().Contains(search)
                || x.Email.ToLower().Contains(search)
                || x.Tenant.CompanyName.ToLower().Contains(search));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);
        var items = await usersQuery
            .OrderBy(x => x.Tenant.CompanyName)
            .ThenBy(x => x.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PortalAdminBusinessUserDto
            {
                Id = x.Id,
                TenantId = x.TenantId,
                CompanyName = x.Tenant.CompanyName,
                FullName = x.FullName,
                Email = x.Email,
                Role = x.Role.Name,
                IsActive = x.IsActive,
                LastLoginAt = x.LastLoginAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PortalAdminBusinessUserDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private async Task<PagedResult<PortalAdminAuditLogDto>> GetAuditLogsInternalAsync(PortalAdminAuditLogQuery query, CancellationToken cancellationToken)
    {
        await EnsureSuperAdminAsync(cancellationToken);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var logsQuery = _dbContext.AdminAuditLogs.IgnoreQueryFilters().Where(x => !x.IsDeleted);

        if (query.ActionType.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.ActionType == query.ActionType.Value);
        }

        if (query.FromDate.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.PerformedAt >= query.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        if (query.ToDate.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.PerformedAt < query.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        var search = query.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            logsQuery = logsQuery.Where(x =>
                x.TargetType.ToLower().Contains(search)
                || (x.TargetName != null && x.TargetName.ToLower().Contains(search))
                || (x.Details != null && x.Details.ToLower().Contains(search)));
        }

        var totalCount = await logsQuery.CountAsync(cancellationToken);
        var pagedLogs = logsQuery
            .OrderByDescending(x => x.PerformedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        var items = await BuildAuditLogDtosAsync(pagedLogs, cancellationToken);

        return new PagedResult<PortalAdminAuditLogDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private async Task<IReadOnlyList<PortalAdminAuditLogDto>> BuildAuditLogDtosAsync(IQueryable<AdminAuditLog> query, CancellationToken cancellationToken)
    {
        var logs = await query.ToListAsync(cancellationToken);
        var actorIds = logs.Select(x => x.PerformedByUserId).Distinct().ToArray();
        var actors = await _dbContext.Users.IgnoreQueryFilters()
            .Where(x => actorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        return logs.Select(log => new PortalAdminAuditLogDto
        {
            Id = log.Id,
            PerformedAt = log.PerformedAt,
            ActionType = log.ActionType.ToString(),
            TargetType = log.TargetType,
            TargetId = log.TargetId,
            TargetName = log.TargetName,
            RelatedTenantId = log.RelatedTenantId,
            PerformedByName = actors.GetValueOrDefault(log.PerformedByUserId),
            Details = log.Details
        }).ToList();
    }

    private async Task RemoveTenantDocumentLinksAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var deliveryNotes = await _dbContext.DeliveryNotes.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.InvoiceId != null)
            .ToListAsync(cancellationToken);
        foreach (var deliveryNote in deliveryNotes)
        {
            deliveryNote.InvoiceId = null;
        }

        var invoices = await _dbContext.Invoices.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.DeliveryNoteId != null)
            .ToListAsync(cancellationToken);
        foreach (var invoice in invoices)
        {
            invoice.DeliveryNoteId = null;
        }

        if (deliveryNotes.Count > 0 || invoices.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private Task DeleteTenantRowsAsync<TEntity>(Guid tenantId, CancellationToken cancellationToken)
        where TEntity : TenantEntity
    {
        return _dbContext.Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<User> EnsureSuperAdminAsync(CancellationToken cancellationToken)
    {
        var context = _currentUserService.GetContext();
        if (context.UserId is null || !string.Equals(context.Role, UserRoleName.SuperAdmin, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Portal admin access is required.");
        }

        var user = await _dbContext.Users.IgnoreQueryFilters()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == context.UserId.Value && !x.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedException("Unauthorized.");

        if (user.Role.Name != UserRoleName.SuperAdmin)
        {
            throw new ForbiddenException("Portal admin access is required.");
        }

        return user;
    }

    private AdminAuditLog CreateAuditLog(
        Guid performedByUserId,
        AdminAuditActionType actionType,
        string targetType,
        string? targetId,
        string? targetName,
        Guid? relatedTenantId,
        object? details = null)
    {
        return new AdminAuditLog
        {
            PerformedByUserId = performedByUserId,
            ActionType = actionType,
            TargetType = targetType,
            TargetId = targetId,
            TargetName = targetName,
            RelatedTenantId = relatedTenantId,
            Details = details is null ? null : JsonSerializer.Serialize(details),
            PerformedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task SendSignupAcceptedNotificationsAsync(SignupRequest signupRequest, CancellationToken cancellationToken)
    {
        await TrySendSignupNotificationAsync(
            signupRequest.CompanyEmail,
            signupRequest.CompanyName,
            () => _notificationService.SendSignupAcceptedAsync(signupRequest.CompanyEmail, signupRequest.CompanyName, cancellationToken));

        if (string.Equals(signupRequest.CompanyEmail, signupRequest.RequestedByEmail, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await TrySendSignupNotificationAsync(
            signupRequest.RequestedByEmail,
            signupRequest.CompanyName,
            () => _notificationService.SendSignupAcceptedAsync(signupRequest.RequestedByEmail, signupRequest.CompanyName, cancellationToken));
    }

    private async Task SendSignupRejectedNotificationsAsync(SignupRequest signupRequest, string rejectionReason, CancellationToken cancellationToken)
    {
        await TrySendSignupNotificationAsync(
            signupRequest.CompanyEmail,
            signupRequest.CompanyName,
            () => _notificationService.SendSignupRejectedAsync(signupRequest.CompanyEmail, signupRequest.CompanyName, rejectionReason, cancellationToken));

        if (string.Equals(signupRequest.CompanyEmail, signupRequest.RequestedByEmail, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await TrySendSignupNotificationAsync(
            signupRequest.RequestedByEmail,
            signupRequest.CompanyName,
            () => _notificationService.SendSignupRejectedAsync(signupRequest.RequestedByEmail, signupRequest.CompanyName, rejectionReason, cancellationToken));
    }

    private async Task TrySendSignupNotificationAsync(string recipientEmail, string companyName, Func<Task> sendAsync)
    {
        try
        {
            await sendAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Signup notification email failed for {RecipientEmail} and company {CompanyName}. Approval/rejection remains saved.",
                recipientEmail,
                companyName);
        }
    }

    private static BusinessAccountStatus ResolveStatus(Tenant tenant)
    {
        if (!tenant.IsActive || tenant.AccountStatus == BusinessAccountStatus.Disabled)
        {
            return BusinessAccountStatus.Disabled;
        }

        return BusinessAccountStatus.Active;
    }

    private TenantSettings CreateDefaultTenantSettings(Tenant tenant, User user)
    {
        return new TenantSettings
        {
            Tenant = tenant,
            Username = user.FullName,
            CompanyName = tenant.CompanyName,
            CompanyEmail = tenant.CompanyEmail,
            CompanyPhone = tenant.CompanyPhone,
            TinNumber = tenant.TinNumber,
            BusinessRegistrationNumber = tenant.BusinessRegistrationNumber,
            InvoicePrefix = "SL",
            DeliveryNotePrefix = "DN",
            QuotePrefix = "QT",
            PurchaseOrderPrefix = "PO",
            ReceivedInvoicePrefix = "RI",
            PaymentVoucherPrefix = "PV",
            RentEntryPrefix = "RENT",
            WarningFormPrefix = "WF",
            StatementPrefix = "SL",
            SalarySlipPrefix = "SAL",
            IsTaxApplicable = true,
            DefaultTaxRate = 0.08m,
            DefaultDueDays = 7,
            DefaultCurrency = "MVR",
            TaxableActivityNumber = string.Empty,
            IsInputTaxClaimEnabled = true,
            BmlMvrAccountName = string.Empty,
            BmlMvrAccountNumber = string.Empty,
            BmlUsdAccountName = string.Empty,
            BmlUsdAccountNumber = string.Empty,
            MibMvrAccountName = string.Empty,
            MibMvrAccountNumber = string.Empty,
            MibUsdAccountName = string.Empty,
            MibUsdAccountNumber = string.Empty,
            InvoiceOwnerName = string.Empty,
            InvoiceOwnerIdCard = string.Empty,
            QuotationEmailBodyTemplate = DocumentEmailTemplateDefaults.Quotation,
            InvoiceEmailBodyTemplate = DocumentEmailTemplateDefaults.Invoice,
            PurchaseOrderEmailBodyTemplate = DocumentEmailTemplateDefaults.PurchaseOrder,
            LogoUrl = "/newlogo.png",
            CompanyStampUrl = null,
            CompanySignatureUrl = null
        };
    }

    private async Task<Role> EnsureRoleAsync(string roleName, string description, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new Role
        {
            Name = roleName,
            Description = description
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return role;
    }

    private IQueryable<Guid> SuperAdminTenantIdsQuery()
    {
        return _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.Role.Name == UserRoleName.SuperAdmin)
            .Select(x => x.TenantId)
            .Distinct();
    }
}
