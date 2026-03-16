using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class PortalAdminEmailService : IPortalAdminEmailService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PortalAdminEmailService> _logger;

    public PortalAdminEmailService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<PortalAdminEmailService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PortalAdminEmailBusinessOptionDto>> GetBusinessOptionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSuperAdminAsync(cancellationToken);

        var businesses = await GetBusinessTenantsQuery()
            .OrderBy(x => x.CompanyName)
            .Select(x => new
            {
                x.Id,
                x.CompanyName,
                x.CompanyEmail,
                x.IsActive,
                x.AccountStatus
            })
            .ToListAsync(cancellationToken);

        if (businesses.Count == 0)
        {
            return Array.Empty<PortalAdminEmailBusinessOptionDto>();
        }

        var tenantIds = businesses.Select(x => x.Id).ToArray();
        var adminUsers = await _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.IsActive && tenantIds.Contains(x.TenantId) && x.Role.Name == UserRoleName.Admin)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.TenantId,
                x.FullName,
                x.Email
            })
            .ToListAsync(cancellationToken);

        var adminsByTenant = adminUsers
            .GroupBy(x => x.TenantId)
            .ToDictionary(x => x.Key, x => x.ToList());

        return businesses.Select(x =>
        {
            adminsByTenant.TryGetValue(x.Id, out var adminGroup);
            var primaryAdmin = adminGroup?.FirstOrDefault();

            return new PortalAdminEmailBusinessOptionDto
            {
                TenantId = x.Id,
                CompanyName = x.CompanyName,
                CompanyEmail = x.CompanyEmail,
                Status = ResolveStatus(x.IsActive, x.AccountStatus),
                ActiveAdminCount = adminGroup?.Count ?? 0,
                PrimaryAdminName = primaryAdmin?.FullName,
                PrimaryAdminEmail = primaryAdmin?.Email
            };
        }).ToList();
    }

    public async Task<PagedResult<PortalAdminEmailCampaignListItemDto>> GetCampaignsAsync(PortalAdminEmailCampaignQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureSuperAdminAsync(cancellationToken);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var campaignsQuery = _dbContext.AdminEmailCampaigns.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.SentAt);

        var totalCount = await campaignsQuery.CountAsync(cancellationToken);
        var campaigns = await campaignsQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var actorIds = campaigns.Select(x => x.SentByUserId).Distinct().ToArray();
        var actorNames = await _dbContext.Users.IgnoreQueryFilters()
            .Where(x => actorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        return new PagedResult<PortalAdminEmailCampaignListItemDto>
        {
            Items = campaigns.Select(x => new PortalAdminEmailCampaignListItemDto
            {
                Id = x.Id,
                Subject = x.Subject,
                AudienceMode = x.AudienceMode,
                CcAdminUsers = x.CcAdminUsers,
                IncludeDisabledBusinesses = x.IncludeDisabledBusinesses,
                RequestedCompanyCount = x.RequestedCompanyCount,
                SentCompanyCount = x.SentCompanyCount,
                FailedCompanyCount = x.FailedCompanyCount,
                SentAt = x.SentAt,
                SentByName = actorNames.GetValueOrDefault(x.SentByUserId)
            }).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PortalAdminEmailCampaignSendResultDto> SendCampaignAsync(PortalAdminSendEmailCampaignRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureSuperAdminAsync(cancellationToken);
        var normalizedSubject = request.Subject.Trim();
        var normalizedBody = NormalizeBody(request.Body);
        var selectedTenantIds = request.TenantIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        var tenantsQuery = GetBusinessTenantsQuery();
        if (!request.IncludeDisabledBusinesses)
        {
            tenantsQuery = tenantsQuery.Where(x => x.IsActive && x.AccountStatus == BusinessAccountStatus.Active);
        }

        if (request.AudienceMode == AdminEmailAudienceMode.SelectedBusinesses)
        {
            tenantsQuery = tenantsQuery.Where(x => selectedTenantIds.Contains(x.Id));
        }

        var tenants = await tenantsQuery
            .OrderBy(x => x.CompanyName)
            .Select(x => new
            {
                x.Id,
                x.CompanyName,
                x.CompanyEmail
            })
            .ToListAsync(cancellationToken);

        if (tenants.Count == 0)
        {
            throw new AppException("No businesses match the selected email audience.");
        }

        var tenantIds = tenants.Select(x => x.Id).ToArray();
        var adminEmailsByTenant = request.CcAdminUsers
            ? await LoadAdminEmailsByTenantAsync(tenantIds, cancellationToken)
            : new Dictionary<Guid, List<string>>();

        var campaign = new AdminEmailCampaign
        {
            SentByUserId = actor.Id,
            AudienceMode = request.AudienceMode,
            Subject = normalizedSubject,
            Body = normalizedBody,
            CcAdminUsers = request.CcAdminUsers,
            IncludeDisabledBusinesses = request.IncludeDisabledBusinesses,
            RequestedCompanyCount = tenants.Count,
            SentAt = DateTimeOffset.UtcNow
        };

        var recipientLogs = new List<AdminEmailCampaignRecipient>(tenants.Count);
        var resultRows = new List<PortalAdminEmailCampaignSendCompanyResultDto>(tenants.Count);
        var sentCount = 0;
        var failedCount = 0;

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var companyEmail = NormalizeEmail(tenant.CompanyEmail);
            var ccEmails = adminEmailsByTenant.GetValueOrDefault(tenant.Id) ?? new List<string>();
            ccEmails = ccEmails
                .Where(email => !string.Equals(email, companyEmail, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var recipientLog = new AdminEmailCampaignRecipient
            {
                AdminEmailCampaignId = campaign.Id,
                TenantId = tenant.Id,
                CompanyName = tenant.CompanyName,
                ToEmail = companyEmail,
                CcEmails = ccEmails.Count == 0 ? null : string.Join(", ", ccEmails),
                AttemptedAt = DateTimeOffset.UtcNow
            };

            try
            {
                if (string.IsNullOrWhiteSpace(companyEmail))
                {
                    throw new AppException("Company email is missing.");
                }

                var personalizedBody = ReplaceCompanyTokens(normalizedBody, tenant.CompanyName);
                await _notificationService.SendPortalAdminAnnouncementAsync(
                    companyEmail,
                    ccEmails,
                    normalizedSubject,
                    personalizedBody,
                    cancellationToken);

                recipientLog.Status = AdminEmailRecipientStatus.Sent;
                sentCount++;
            }
            catch (Exception ex)
            {
                var errorMessage = ex is AppException appException
                    ? appException.Message
                    : "Unexpected email delivery error.";

                recipientLog.Status = AdminEmailRecipientStatus.Failed;
                recipientLog.ErrorMessage = Truncate(errorMessage, 1200);
                failedCount++;

                _logger.LogWarning(
                    ex,
                    "Portal admin email campaign {CampaignId} failed for tenant {TenantId} ({CompanyName})",
                    campaign.Id,
                    tenant.Id,
                    tenant.CompanyName);
            }

            recipientLogs.Add(recipientLog);
            resultRows.Add(new PortalAdminEmailCampaignSendCompanyResultDto
            {
                TenantId = tenant.Id,
                CompanyName = tenant.CompanyName,
                ToEmail = companyEmail,
                CcAdminCount = ccEmails.Count,
                Status = recipientLog.Status,
                ErrorMessage = recipientLog.ErrorMessage
            });
        }

        campaign.SentCompanyCount = sentCount;
        campaign.FailedCompanyCount = failedCount;

        _dbContext.AdminEmailCampaigns.Add(campaign);
        _dbContext.AdminEmailCampaignRecipients.AddRange(recipientLogs);
        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            actor.Id,
            AdminAuditActionType.EmailCampaignSent,
            nameof(AdminEmailCampaign),
            campaign.Id.ToString(),
            normalizedSubject,
            tenants.Count == 1 ? tenants[0].Id : null,
            new
            {
                request.AudienceMode,
                request.CcAdminUsers,
                request.IncludeDisabledBusinesses,
                RequestedCompanyCount = tenants.Count,
                SentCompanyCount = sentCount,
                FailedCompanyCount = failedCount,
                TenantIds = tenantIds
            }));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PortalAdminEmailCampaignSendResultDto
        {
            CampaignId = campaign.Id,
            RequestedCompanyCount = tenants.Count,
            SentCompanyCount = sentCount,
            FailedCompanyCount = failedCount,
            Results = resultRows
        };
    }

    private async Task<Dictionary<Guid, List<string>>> LoadAdminEmailsByTenantAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var adminRows = await _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.IsActive && tenantIds.Contains(x.TenantId) && x.Role.Name == UserRoleName.Admin)
            .Select(x => new
            {
                x.TenantId,
                x.Email
            })
            .ToListAsync(cancellationToken);

        return adminRows
            .GroupBy(x => x.TenantId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(row => NormalizeEmail(row.Email))
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList());
    }

    private IQueryable<Tenant> GetBusinessTenantsQuery()
    {
        var superAdminTenantIds = _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.Role.Name == UserRoleName.SuperAdmin)
            .Select(x => x.TenantId)
            .Distinct();

        return _dbContext.Tenants.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && !superAdminTenantIds.Contains(x.Id));
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

    private static BusinessAccountStatus ResolveStatus(bool isActive, BusinessAccountStatus accountStatus)
        => !isActive || accountStatus == BusinessAccountStatus.Disabled
            ? BusinessAccountStatus.Disabled
            : BusinessAccountStatus.Active;

    private static string NormalizeBody(string body)
        => body.Replace("\r\n", "\n").Replace("\r", "\n").Trim();

    private static string NormalizeEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();

    private static string ReplaceCompanyTokens(string body, string companyName)
    {
        var normalizedCompanyName = companyName.Trim();
        return body
            .Replace("[company name]", normalizedCompanyName, StringComparison.OrdinalIgnoreCase)
            .Replace("[user company name]", normalizedCompanyName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
