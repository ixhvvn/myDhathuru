using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Configuration;
using MyDhathuru.Infrastructure.Persistence;
using MyDhathuru.Infrastructure.Security;
using MyDhathuru.Infrastructure.Services;

namespace MyDhathuru.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is missing.");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            });
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPortalAdminAuthService, PortalAdminAuthService>();
        services.AddScoped<IPortalAdminService, PortalAdminService>();
        services.AddScoped<IPortalAdminBillingService, PortalAdminBillingService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IVesselService, VesselService>();
        services.AddScoped<IDeliveryNoteService, DeliveryNoteService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IStatementService, StatementService>();
        services.AddScoped<IPayrollService, PayrollService>();
        services.AddScoped<ISupportService, SupportService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IDocumentNumberService, DocumentNumberService>();
        services.AddScoped<IPdfExportService, PdfExportService>();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? throw new InvalidOperationException("Jwt settings missing.");

                // Keep JWT claim types as issued ("role", "tenant_id", etc.) to match policy checks.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole(UserRoleName.SuperAdmin));
            options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRoleName.Admin));
            options.AddPolicy("StaffOrAdmin", policy => policy.RequireRole(UserRoleName.Admin, UserRoleName.Staff));
        });

        services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>();

        return services;
    }
}
