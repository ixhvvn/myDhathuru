using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Support.Dtos;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class SupportService : ISupportService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;

    public SupportService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
    }

    public async Task ReportBugAsync(
        ReportBugRequest request,
        BugReportAttachment? attachment = null,
        CancellationToken cancellationToken = default)
    {
        var context = _currentUserService.GetContext();
        if (!context.UserId.HasValue || !context.TenantId.HasValue)
        {
            throw new AppException("Unable to resolve the current user context.");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == context.UserId.Value, cancellationToken)
            ?? throw new AppException("Unable to resolve the current user profile.");

        var companyName = await _dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == context.TenantId.Value)
            .Select(x => x.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);

        await _notificationService.SendBugReportAsync(
            user.FullName,
            context.Email,
            companyName,
            request.Subject.Trim(),
            request.Description.Trim(),
            request.PageUrl?.Trim(),
            attachment,
            cancellationToken);
    }
}
