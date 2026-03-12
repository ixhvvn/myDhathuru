using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentTenantService currentTenantService,
        ICurrentUserService currentUserService) : base(options)
    {
        _currentTenantService = currentTenantService;
        _currentUserService = currentUserService;
    }

    private Guid CurrentTenantId => _currentTenantService.TenantId ?? Guid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<SignupRequest> SignupRequests => Set<SignupRequest>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<AdminBillingSettings> AdminBillingSettings => Set<AdminBillingSettings>();
    public DbSet<BusinessCustomRate> BusinessCustomRates => Set<BusinessCustomRate>();
    public DbSet<AdminInvoice> AdminInvoices => Set<AdminInvoice>();
    public DbSet<AdminInvoiceLineItem> AdminInvoiceLineItems => Set<AdminInvoiceLineItem>();
    public DbSet<AdminInvoiceEmailLog> AdminInvoiceEmailLogs => Set<AdminInvoiceEmailLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
    public DbSet<Vessel> Vessels => Set<Vessel>();
    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();
    public DbSet<DeliveryNoteItem> DeliveryNoteItems => Set<DeliveryNoteItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<CustomerOpeningBalance> CustomerOpeningBalances => Set<CustomerOpeningBalance>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();
    public DbSet<SalarySlip> SalarySlips => Set<SalarySlip>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(x => x.CompanyEmail).IsUnique();
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.Property(x => x.CompanyEmail).HasMaxLength(200);
            entity.Property(x => x.CompanyPhone).HasMaxLength(50);
            entity.Property(x => x.TinNumber).HasMaxLength(100);
            entity.Property(x => x.BusinessRegistrationNumber).HasMaxLength(100);
            entity.Property(x => x.AccountStatus).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.DisabledReason).HasMaxLength(300);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            entity.Property(x => x.FullName).HasMaxLength(150);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<SignupRequest>(entity =>
        {
            entity.HasIndex(x => x.CompanyEmail);
            entity.HasIndex(x => x.RequestedByEmail);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.SubmittedAt);
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.Property(x => x.CompanyEmail).HasMaxLength(200);
            entity.Property(x => x.CompanyPhone).HasMaxLength(50);
            entity.Property(x => x.TinNumber).HasMaxLength(100);
            entity.Property(x => x.BusinessRegistrationNumber).HasMaxLength(100);
            entity.Property(x => x.RequestedByName).HasMaxLength(150);
            entity.Property(x => x.RequestedByEmail).HasMaxLength(200);
            entity.Property(x => x.PasswordHash).HasMaxLength(400);
            entity.Property(x => x.PasswordSalt).HasMaxLength(200);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.Property(x => x.ReviewNotes).HasMaxLength(500);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AdminAuditLog>(entity =>
        {
            entity.HasIndex(x => x.PerformedAt);
            entity.HasIndex(x => x.ActionType);
            entity.HasIndex(x => x.RelatedTenantId);
            entity.Property(x => x.ActionType).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.TargetType).HasMaxLength(120);
            entity.Property(x => x.TargetId).HasMaxLength(120);
            entity.Property(x => x.TargetName).HasMaxLength(250);
            entity.Property(x => x.Details).HasMaxLength(2000);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AdminBillingSettings>(entity =>
        {
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.BasicSoftwareFee).HasColumnType("numeric(18,2)");
            entity.Property(x => x.VesselFee).HasColumnType("numeric(18,2)");
            entity.Property(x => x.StaffFee).HasColumnType("numeric(18,2)");
            entity.Property(x => x.InvoicePrefix).HasMaxLength(20);
            entity.Property(x => x.DefaultCurrency).HasMaxLength(3);
            entity.Property(x => x.AccountName).HasMaxLength(200);
            entity.Property(x => x.AccountNumber).HasMaxLength(120);
            entity.Property(x => x.BankName).HasMaxLength(120);
            entity.Property(x => x.Branch).HasMaxLength(120);
            entity.Property(x => x.PaymentInstructions).HasMaxLength(600);
            entity.Property(x => x.InvoiceFooterNote).HasMaxLength(600);
            entity.Property(x => x.InvoiceTerms).HasMaxLength(1200);
            entity.Property(x => x.LogoUrl).HasMaxLength(400);
            entity.Property(x => x.EmailFromName).HasMaxLength(120);
            entity.Property(x => x.ReplyToEmail).HasMaxLength(200);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<BusinessCustomRate>(entity =>
        {
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.IsActive, x.EffectiveFrom, x.EffectiveTo });
            entity.Property(x => x.SoftwareFee).HasColumnType("numeric(18,2)");
            entity.Property(x => x.VesselFee).HasColumnType("numeric(18,2)");
            entity.Property(x => x.StaffFee).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AdminInvoice>(entity =>
        {
            entity.HasIndex(x => x.InvoiceNumber).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.BillingMonth });
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.InvoiceNumber).HasMaxLength(60);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.CompanyNameSnapshot).HasMaxLength(200);
            entity.Property(x => x.CompanyEmailSnapshot).HasMaxLength(200);
            entity.Property(x => x.CompanyPhoneSnapshot).HasMaxLength(50);
            entity.Property(x => x.CompanyTinSnapshot).HasMaxLength(100);
            entity.Property(x => x.CompanyRegistrationSnapshot).HasMaxLength(100);
            entity.Property(x => x.CompanyAdminNameSnapshot).HasMaxLength(150);
            entity.Property(x => x.CompanyAdminEmailSnapshot).HasMaxLength(200);
            entity.Property(x => x.BaseSoftwareFee).HasColumnType("numeric(18,2)");
            entity.Property(x => x.VesselRate).HasColumnType("numeric(18,2)");
            entity.Property(x => x.VesselAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.StaffRate).HasColumnType("numeric(18,2)");
            entity.Property(x => x.StaffAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Total).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Notes).HasMaxLength(700);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AdminInvoiceLineItem>(entity =>
        {
            entity.HasIndex(x => new { x.AdminInvoiceId, x.SortOrder });
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Quantity).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Rate).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AdminInvoiceEmailLog>(entity =>
        {
            entity.HasIndex(x => new { x.AdminInvoiceId, x.AttemptedAt });
            entity.Property(x => x.ToEmail).HasMaxLength(200);
            entity.Property(x => x.CcEmail).HasMaxLength(200);
            entity.Property(x => x.Subject).HasMaxLength(250);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ErrorMessage).HasMaxLength(1200);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.TokenHash).HasMaxLength(128);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasIndex(x => x.TokenHash);
            entity.Property(x => x.TokenHash).HasMaxLength(128);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<TenantSettings>(entity =>
        {
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(120);
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.Property(x => x.CompanyEmail).HasMaxLength(200);
            entity.Property(x => x.CompanyPhone).HasMaxLength(50);
            entity.Property(x => x.TinNumber).HasMaxLength(100);
            entity.Property(x => x.BusinessRegistrationNumber).HasMaxLength(100);
            entity.Property(x => x.InvoicePrefix).HasMaxLength(20);
            entity.Property(x => x.DeliveryNotePrefix).HasMaxLength(20);
            entity.Property(x => x.StatementPrefix).HasMaxLength(20);
            entity.Property(x => x.SalarySlipPrefix).HasMaxLength(20);
            entity.Property(x => x.BmlMvrAccountName).HasMaxLength(200);
            entity.Property(x => x.BmlMvrAccountNumber).HasMaxLength(100);
            entity.Property(x => x.BmlUsdAccountName).HasMaxLength(200);
            entity.Property(x => x.BmlUsdAccountNumber).HasMaxLength(100);
            entity.Property(x => x.MibMvrAccountName).HasMaxLength(200);
            entity.Property(x => x.MibMvrAccountNumber).HasMaxLength(100);
            entity.Property(x => x.MibUsdAccountName).HasMaxLength(200);
            entity.Property(x => x.MibUsdAccountNumber).HasMaxLength(100);
            entity.Property(x => x.InvoiceOwnerName).HasMaxLength(200);
            entity.Property(x => x.InvoiceOwnerIdCard).HasMaxLength(100);
            entity.Property(x => x.LogoUrl).HasMaxLength(400);
        });

        modelBuilder.Entity<DocumentSequence>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.DocumentType, x.Year }).IsUnique();
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.TinNumber).HasMaxLength(100);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(200);
        });

        modelBuilder.Entity<CustomerContact>(entity =>
        {
            entity.Property(x => x.Value).HasMaxLength(200);
            entity.Property(x => x.Label).HasMaxLength(50);
        });

        modelBuilder.Entity<Vessel>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.RegistrationNumber }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.RegistrationNumber).HasMaxLength(100);
            entity.Property(x => x.VesselType).HasMaxLength(100);
            entity.Property(x => x.HomePort).HasMaxLength(120);
            entity.Property(x => x.OwnerName).HasMaxLength(200);
            entity.Property(x => x.ContactPhone).HasMaxLength(50);
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<DeliveryNote>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.DeliveryNoteNo }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Date });
            entity.Property(x => x.DeliveryNoteNo).HasMaxLength(50);
            entity.Property(x => x.PoNumber).HasMaxLength(100);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.Invoice)
                .WithOne(x => x.DeliveryNote)
                .HasForeignKey<Invoice>(x => x.DeliveryNoteId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DeliveryNoteItem>(entity =>
        {
            entity.Property(x => x.Details).HasMaxLength(400);
            entity.Property(x => x.Qty).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Rate).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Total).HasColumnType("numeric(18,2)");
            entity.Property(x => x.CashPayment).HasColumnType("numeric(18,2)");
            entity.Property(x => x.VesselPayment).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.InvoiceNo }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.DateIssued });
            entity.HasIndex(x => x.CourierVesselId);
            entity.Property(x => x.InvoiceNo).HasMaxLength(50);
            entity.Property(x => x.PoNumber).HasMaxLength(100);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TaxRate).HasColumnType("numeric(8,4)");
            entity.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.GrandTotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.AmountPaid).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Balance).HasColumnType("numeric(18,2)");
            entity.HasOne(x => x.CourierVessel)
                .WithMany()
                .HasForeignKey(x => x.CourierVesselId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(400);
            entity.Property(x => x.Qty).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Rate).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Total).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<InvoicePayment>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.PaymentDate });
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Reference).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(300);
        });

        modelBuilder.Entity<CustomerOpeningBalance>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.CustomerId, x.Year }).IsUnique();
            entity.Property(x => x.OpeningBalanceMvr).HasColumnType("numeric(18,2)");
            entity.Property(x => x.OpeningBalanceUsd).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Notes).HasMaxLength(300);
        });

        modelBuilder.Entity<Staff>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.StaffId }).IsUnique();
            entity.Property(x => x.StaffId).HasMaxLength(40);
            entity.Property(x => x.StaffName).HasMaxLength(200);
            entity.Property(x => x.Designation).HasMaxLength(120);
            entity.Property(x => x.WorkSite).HasMaxLength(120);
            entity.Property(x => x.BankName).HasMaxLength(10);
            entity.Property(x => x.AccountName).HasMaxLength(200);
            entity.Property(x => x.AccountNumber).HasMaxLength(100);
            entity.Property(x => x.Basic).HasColumnType("numeric(18,2)");
            entity.Property(x => x.ServiceAllowance).HasColumnType("numeric(18,2)");
            entity.Property(x => x.OtherAllowance).HasColumnType("numeric(18,2)");
            entity.Property(x => x.PhoneAllowance).HasColumnType("numeric(18,2)");
            entity.Property(x => x.FoodRate).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<PayrollPeriod>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Year, x.Month }).IsUnique();
            entity.Property(x => x.TotalNetPayable).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<PayrollEntry>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.PayrollPeriodId, x.StaffId }).IsUnique();
            entity.Property(x => x.Basic).HasColumnType("numeric(18,2)");
            entity.Property(x => x.ServiceAllowance).HasColumnType("numeric(18,2)");
            entity.Property(x => x.OtherAllowance).HasColumnType("numeric(18,2)");
            entity.Property(x => x.PhoneAllowance).HasColumnType("numeric(18,2)");
            entity.Property(x => x.GrossBase).HasColumnType("numeric(18,2)");
            entity.Property(x => x.GrossAllowances).HasColumnType("numeric(18,2)");
            entity.Property(x => x.SubTotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.RatePerDay).HasColumnType("numeric(18,4)");
            entity.Property(x => x.AbsentDeduction).HasColumnType("numeric(18,2)");
            entity.Property(x => x.FoodAllowanceRate).HasColumnType("numeric(18,2)");
            entity.Property(x => x.FoodAllowance).HasColumnType("numeric(18,2)");
            entity.Property(x => x.OvertimePay).HasColumnType("numeric(18,2)");
            entity.Property(x => x.PensionDeduction).HasColumnType("numeric(18,2)");
            entity.Property(x => x.SalaryAdvanceDeduction).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TotalPay).HasColumnType("numeric(18,2)");
            entity.Property(x => x.NetPayable).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<SalarySlip>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.SlipNo }).IsUnique();
            entity.HasIndex(x => x.PayrollEntryId).IsUnique();
            entity.Property(x => x.SlipNo).HasMaxLength(50);
        });

        ApplyTenantFilters(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        var tenantEntityType = typeof(TenantEntity);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!tenantEntityType.IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(ApplicationDbContext)
                .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, new object[] { modelBuilder });
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : TenantEntity
    {
        Expression<Func<TEntity, bool>> filter = x => !x.IsDeleted && x.TenantId == CurrentTenantId;
        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndTenant();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditAndTenant();
        return base.SaveChanges();
    }

    private void ApplyAuditAndTenant()
    {
        var now = DateTimeOffset.UtcNow;
        var context = _currentUserService.GetContext();

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedByUserId = context.UserId;

                if (entry.Entity is TenantEntity tenantEntity && tenantEntity.TenantId == Guid.Empty && _currentTenantService.TenantId.HasValue)
                {
                    tenantEntity.TenantId = _currentTenantService.TenantId.Value;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedByUserId = context.UserId;
            }
            else if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedByUserId = context.UserId;
            }
        }
    }
}
