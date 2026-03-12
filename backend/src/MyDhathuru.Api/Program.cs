using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json.Serialization;
using MyDhathuru.Api.Filters;
using MyDhathuru.Api.Middlewares;
using MyDhathuru.Application;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure;
using MyDhathuru.Infrastructure.Persistence;
using MyDhathuru.Infrastructure.Security;
using QuestPDF.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ValidationActionFilter>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;

    // The production reverse proxy runs on the VPS host and reaches the app over Docker networking.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim())
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AppCors");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

await ApplyMigrationsAsync(app);

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await dbContext.Database.MigrateAsync();
    await EnsurePortalAdminBootstrapAsync(dbContext, configuration, passwordHasher);
}

static async Task EnsurePortalAdminBootstrapAsync(ApplicationDbContext dbContext, IConfiguration configuration, IPasswordHasher passwordHasher)
{
    var email = (configuration["PortalAdmin:Email"] ?? "mydhathuru@gmail.com").Trim().ToLowerInvariant();
    var password = configuration["PortalAdmin:Password"] ?? "Admin@12345";
    var fullName = (configuration["PortalAdmin:FullName"] ?? "Portal Super Admin").Trim();

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
    {
        return;
    }

    var superAdminRole = await EnsureRoleAsync(dbContext, UserRoleName.SuperAdmin, "Platform super admin");
    await EnsureRoleAsync(dbContext, UserRoleName.Admin, "Tenant administrator");
    await EnsureRoleAsync(dbContext, UserRoleName.Staff, "Tenant staff user");

    var existingSuperAdmin = await dbContext.Users.IgnoreQueryFilters()
        .Include(x => x.Role)
        .FirstOrDefaultAsync(x => !x.IsDeleted && x.Email == email && x.Role.Name == UserRoleName.SuperAdmin);

    if (existingSuperAdmin is not null)
    {
        if (!existingSuperAdmin.IsActive)
        {
            existingSuperAdmin.IsActive = true;
            await dbContext.SaveChangesAsync();
        }
        return;
    }

    var systemTenant = await dbContext.Tenants.IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => !x.IsDeleted && x.BusinessRegistrationNumber == "SYSTEM-PORTAL-ADMIN");

    if (systemTenant is null)
    {
        systemTenant = new Tenant
        {
            CompanyName = "myDhathuru Platform",
            CompanyEmail = email,
            CompanyPhone = "0000000",
            TinNumber = "SYSTEM",
            BusinessRegistrationNumber = "SYSTEM-PORTAL-ADMIN",
            IsActive = true,
            AccountStatus = BusinessAccountStatus.Active
        };
        dbContext.Tenants.Add(systemTenant);
        await dbContext.SaveChangesAsync();
    }

    var (hash, salt) = passwordHasher.HashPassword(password);
    dbContext.Users.Add(new User
    {
        TenantId = systemTenant.Id,
        RoleId = superAdminRole.Id,
        FullName = fullName,
        Email = email,
        PasswordHash = hash,
        PasswordSalt = salt,
        IsActive = true
    });

    await dbContext.SaveChangesAsync();
}

static async Task<Role> EnsureRoleAsync(ApplicationDbContext dbContext, string roleName, string description)
{
    var role = await dbContext.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Name == roleName);
    if (role is not null)
    {
        return role;
    }

    role = new Role
    {
        Name = roleName,
        Description = description
    };
    dbContext.Roles.Add(role);
    await dbContext.SaveChangesAsync();
    return role;
}
