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
    public DbSet<AdminEmailCampaign> AdminEmailCampaigns => Set<AdminEmailCampaign>();
    public DbSet<AdminEmailCampaignRecipient> AdminEmailCampaignRecipients => Set<AdminEmailCampaignRecipient>();
    public DbSet<BusinessCustomRate> BusinessCustomRates => Set<BusinessCustomRate>();
    public DbSet<AdminInvoice> AdminInvoices => Set<AdminInvoice>();
    public DbSet<AdminInvoiceLineItem> AdminInvoiceLineItems => Set<AdminInvoiceLineItem>();
    public DbSet<AdminInvoiceEmailLog> AdminInvoiceEmailLogs => Set<AdminInvoiceEmailLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<BusinessAuditLog> BusinessAuditLogs => Set<BusinessAuditLog>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<BptCategory> BptCategories => Set<BptCategory>();
    public DbSet<BptMappingRule> BptMappingRules => Set<BptMappingRule>();
    public DbSet<BptAdjustment> BptAdjustments => Set<BptAdjustment>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<SalesAdjustment> SalesAdjustments => Set<SalesAdjustment>();
    public DbSet<OtherIncomeEntry> OtherIncomeEntries => Set<OtherIncomeEntry>();
    public DbSet<Vessel> Vessels => Set<Vessel>();
    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();
    public DbSet<DeliveryNoteItem> DeliveryNoteItems => Set<DeliveryNoteItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<ReceivedInvoice> ReceivedInvoices => Set<ReceivedInvoice>();
    public DbSet<ReceivedInvoiceItem> ReceivedInvoiceItems => Set<ReceivedInvoiceItem>();
    public DbSet<ReceivedInvoicePayment> ReceivedInvoicePayments => Set<ReceivedInvoicePayment>();
    public DbSet<ReceivedInvoiceAttachment> ReceivedInvoiceAttachments => Set<ReceivedInvoiceAttachment>();
    public DbSet<PaymentVoucher> PaymentVouchers => Set<PaymentVoucher>();
    public DbSet<ExpenseEntry> ExpenseEntries => Set<ExpenseEntry>();
    public DbSet<RentEntry> RentEntries => Set<RentEntry>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<CustomerOpeningBalance> CustomerOpeningBalances => Set<CustomerOpeningBalance>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();
    public DbSet<SalarySlip> SalarySlips => Set<SalarySlip>();
    public DbSet<StaffConductForm> StaffConductForms => Set<StaffConductForm>();
    public DbSet<StaffConductExportDocument> StaffConductExportDocuments => Set<StaffConductExportDocument>();

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
            entity.HasIndex(x => x.IsDataTesting);
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

        modelBuilder.Entity<AdminEmailCampaign>(entity =>
        {
            entity.HasIndex(x => x.SentAt);
            entity.HasIndex(x => x.SentByUserId);
            entity.Property(x => x.AudienceMode).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Subject).HasMaxLength(250);
            entity.Property(x => x.Body).HasMaxLength(5000);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AdminEmailCampaignRecipient>(entity =>
        {
            entity.HasIndex(x => new { x.AdminEmailCampaignId, x.AttemptedAt });
            entity.HasIndex(x => new { x.TenantId, x.AttemptedAt });
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.Property(x => x.ToEmail).HasMaxLength(200);
            entity.Property(x => x.CcEmails).HasMaxLength(2000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ErrorMessage).HasMaxLength(1200);
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
            entity.Property(x => x.QuotePrefix).HasMaxLength(20);
            entity.Property(x => x.PurchaseOrderPrefix).HasMaxLength(20);
            entity.Property(x => x.ReceivedInvoicePrefix).HasMaxLength(20);
            entity.Property(x => x.PaymentVoucherPrefix).HasMaxLength(20);
            entity.Property(x => x.RentEntryPrefix).HasMaxLength(20);
            entity.Property(x => x.WarningFormPrefix).HasMaxLength(20);
            entity.Property(x => x.StatementPrefix).HasMaxLength(20);
            entity.Property(x => x.SalarySlipPrefix).HasMaxLength(20);
            entity.Property(x => x.IsTaxApplicable).HasDefaultValue(true);
            entity.Property(x => x.TaxableActivityNumber).HasMaxLength(50);
            entity.Property(x => x.IsInputTaxClaimEnabled).HasDefaultValue(true);
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
            entity.Property(x => x.QuotationEmailBodyTemplate).HasMaxLength(4000);
            entity.Property(x => x.InvoiceEmailBodyTemplate).HasMaxLength(4000);
            entity.Property(x => x.PurchaseOrderEmailBodyTemplate).HasMaxLength(4000);
            entity.Property(x => x.LogoUrl).HasMaxLength(400);
            entity.Property(x => x.CompanyStampUrl).HasMaxLength(400);
            entity.Property(x => x.CompanySignatureUrl).HasMaxLength(400);
        });

        modelBuilder.Entity<DocumentSequence>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.DocumentType, x.Year }).IsUnique();
        });

        modelBuilder.Entity<BusinessAuditLog>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.PerformedAt });
            entity.HasIndex(x => new { x.TenantId, x.ActionType });
            entity.HasIndex(x => new { x.TenantId, x.TargetType });
            entity.Property(x => x.ActionType).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.TargetType).HasMaxLength(120);
            entity.Property(x => x.TargetId).HasMaxLength(120);
            entity.Property(x => x.TargetName).HasMaxLength(250);
            entity.Property(x => x.DetailsJson).HasMaxLength(4000);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.TinNumber).HasMaxLength(100);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(200);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.TinNumber).HasMaxLength(100);
            entity.Property(x => x.ContactNumber).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Address).HasMaxLength(400);
            entity.Property(x => x.Notes).HasMaxLength(600);
        });

        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.Code).HasMaxLength(50);
            entity.Property(x => x.Description).HasMaxLength(400);
            entity.Property(x => x.BptCategoryCode).HasConversion<string>().HasMaxLength(60);
        });

        modelBuilder.Entity<BptCategory>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.Property(x => x.Code).HasConversion<string>().HasMaxLength(60);
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.ClassificationGroup).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<BptMappingRule>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ExpenseCategoryId, x.SourceModule, x.IsActive });
            entity.HasIndex(x => new { x.TenantId, x.BptCategoryId, x.IsActive });
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.SourceModule).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.SalesAdjustmentType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.RevenueCapitalClassification).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(600);
            entity.HasOne(x => x.ExpenseCategory)
                .WithMany()
                .HasForeignKey(x => x.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.BptCategory)
                .WithMany(x => x.MappingRules)
                .HasForeignKey(x => x.BptCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BptAdjustment>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.AdjustmentNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.TransactionDate });
            entity.HasIndex(x => new { x.TenantId, x.BptCategoryId, x.ApprovalStatus });
            entity.Property(x => x.AdjustmentNumber).HasMaxLength(60);
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.ExchangeRate).HasColumnType("numeric(18,6)");
            entity.Property(x => x.AmountOriginal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.AmountMvr).HasColumnType("numeric(18,2)");
            entity.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.BptCategory)
                .WithMany(x => x.Adjustments)
                .HasForeignKey(x => x.BptCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExchangeRate>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Currency, x.RateDate }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.RateDate });
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.RateToMvr).HasColumnType("numeric(18,6)");
            entity.Property(x => x.Source).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(600);
        });

        modelBuilder.Entity<SalesAdjustment>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.AdjustmentNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.TransactionDate });
            entity.HasIndex(x => new { x.TenantId, x.AdjustmentType, x.ApprovalStatus });
            entity.Property(x => x.AdjustmentNumber).HasMaxLength(60);
            entity.Property(x => x.AdjustmentType).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.RelatedInvoiceNumber).HasMaxLength(60);
            entity.Property(x => x.CustomerName).HasMaxLength(200);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.ExchangeRate).HasColumnType("numeric(18,6)");
            entity.Property(x => x.AmountOriginal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.AmountMvr).HasColumnType("numeric(18,2)");
            entity.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.RelatedInvoice)
                .WithMany()
                .HasForeignKey(x => x.RelatedInvoiceId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OtherIncomeEntry>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.EntryNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.TransactionDate });
            entity.HasIndex(x => new { x.TenantId, x.ApprovalStatus });
            entity.Property(x => x.EntryNumber).HasMaxLength(60);
            entity.Property(x => x.CounterpartyName).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.ExchangeRate).HasColumnType("numeric(18,6)");
            entity.Property(x => x.AmountOriginal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.AmountMvr).HasColumnType("numeric(18,2)");
            entity.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
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
            entity.Property(x => x.PoAttachmentFileName).HasMaxLength(260);
            entity.Property(x => x.PoAttachmentContentType).HasMaxLength(150);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.VesselPaymentFee).HasColumnType("numeric(18,2)");
            entity.Property(x => x.VesselPaymentInvoiceNumber).HasMaxLength(100);
            entity.Property(x => x.VesselPaymentInvoiceAttachmentFileName).HasMaxLength(260);
            entity.Property(x => x.VesselPaymentInvoiceAttachmentContentType).HasMaxLength(150);
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
            entity.HasIndex(x => x.QuotationId).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.EmailStatus });
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
            entity.Property(x => x.EmailStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.LastEmailedTo).HasMaxLength(200);
            entity.Property(x => x.LastEmailedCc).HasMaxLength(500);
            entity.HasOne(x => x.CourierVessel)
                .WithMany()
                .HasForeignKey(x => x.CourierVesselId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Quotation)
                .WithOne(x => x.ConvertedInvoice)
                .HasForeignKey<Invoice>(x => x.QuotationId)
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

        modelBuilder.Entity<ReceivedInvoice>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.InvoiceNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.InvoiceDate });
            entity.HasIndex(x => new { x.TenantId, x.DueDate });
            entity.HasIndex(x => new { x.TenantId, x.SupplierId });
            entity.HasIndex(x => new { x.TenantId, x.ExpenseCategoryId });
            entity.HasIndex(x => new { x.TenantId, x.PaymentStatus });
            entity.Property(x => x.InvoiceNumber).HasMaxLength(60);
            entity.Property(x => x.SupplierName).HasMaxLength(200);
            entity.Property(x => x.SupplierTin).HasMaxLength(100);
            entity.Property(x => x.SupplierContactNumber).HasMaxLength(50);
            entity.Property(x => x.SupplierEmail).HasMaxLength(200);
            entity.Property(x => x.Outlet).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.DiscountAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.GstRate).HasColumnType("numeric(10,4)");
            entity.Property(x => x.GstAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TotalAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.BalanceDue).HasColumnType("numeric(18,2)");
            entity.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ReceiptReference).HasMaxLength(150);
            entity.Property(x => x.SettlementReference).HasMaxLength(150);
            entity.Property(x => x.BankName).HasMaxLength(120);
            entity.Property(x => x.BankAccountDetails).HasMaxLength(180);
            entity.Property(x => x.MiraTaxableActivityNumber).HasMaxLength(50);
            entity.Property(x => x.RevenueCapitalClassification).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.ReceivedInvoices)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ExpenseCategory)
                .WithMany(x => x.ReceivedInvoices)
                .HasForeignKey(x => x.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReceivedInvoiceItem>(entity =>
        {
            entity.HasIndex(x => new { x.ReceivedInvoiceId, x.CreatedAt });
            entity.Property(x => x.Description).HasMaxLength(400);
            entity.Property(x => x.Uom).HasMaxLength(30);
            entity.Property(x => x.Qty).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Rate).HasColumnType("numeric(18,2)");
            entity.Property(x => x.DiscountAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.LineTotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.GstRate).HasColumnType("numeric(10,4)");
            entity.Property(x => x.GstAmount).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<ReceivedInvoicePayment>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ReceivedInvoiceId, x.PaymentDate });
            entity.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Reference).HasMaxLength(150);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.PaymentVoucher)
                .WithMany(x => x.ReceivedInvoicePayments)
                .HasForeignKey(x => x.PaymentVoucherId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ReceivedInvoiceAttachment>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ReceivedInvoiceId, x.CreatedAt });
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(150);
        });

        modelBuilder.Entity<PaymentVoucher>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.VoucherNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Date });
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.Property(x => x.VoucherNumber).HasMaxLength(60);
            entity.Property(x => x.PayTo).HasMaxLength(200);
            entity.Property(x => x.Details).HasMaxLength(600);
            entity.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.AccountNumber).HasMaxLength(120);
            entity.Property(x => x.ChequeNumber).HasMaxLength(120);
            entity.Property(x => x.Bank).HasMaxLength(120);
            entity.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.AmountInWords).HasMaxLength(300);
            entity.Property(x => x.ApprovedBy).HasMaxLength(150);
            entity.Property(x => x.ReceivedBy).HasMaxLength(150);
            entity.Property(x => x.Notes).HasMaxLength(800);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(x => x.LinkedReceivedInvoice)
                .WithMany()
                .HasForeignKey(x => x.LinkedReceivedInvoiceId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.LinkedExpenseEntry)
                .WithMany()
                .HasForeignKey(x => x.LinkedExpenseEntryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ExpenseEntry>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.SourceType, x.SourceId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.TransactionDate });
            entity.HasIndex(x => new { x.TenantId, x.ExpenseCategoryId });
            entity.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DocumentNumber).HasMaxLength(80);
            entity.Property(x => x.PayeeName).HasMaxLength(200);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.NetAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.GrossAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.ClaimableTaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.PendingAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.ExpenseCategory)
                .WithMany(x => x.ExpenseEntries)
                .HasForeignKey(x => x.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RentEntry>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.RentNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Date });
            entity.HasIndex(x => new { x.TenantId, x.ExpenseCategoryId });
            entity.Property(x => x.RentNumber).HasMaxLength(60);
            entity.Property(x => x.PropertyName).HasMaxLength(200);
            entity.Property(x => x.PayTo).HasMaxLength(200);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(800);
            entity.HasOne(x => x.ExpenseCategory)
                .WithMany(x => x.RentEntries)
                .HasForeignKey(x => x.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.PurchaseOrderNo }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.DateIssued });
            entity.HasIndex(x => x.SupplierId);
            entity.HasIndex(x => x.CourierVesselId);
            entity.HasIndex(x => new { x.TenantId, x.EmailStatus });
            entity.Property(x => x.PurchaseOrderNo).HasMaxLength(50);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TaxRate).HasColumnType("numeric(8,4)");
            entity.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.GrandTotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.EmailStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.LastEmailedTo).HasMaxLength(200);
            entity.Property(x => x.LastEmailedCc).HasMaxLength(500);
            entity.HasOne(x => x.CourierVessel)
                .WithMany()
                .HasForeignKey(x => x.CourierVesselId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(400);
            entity.Property(x => x.Qty).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Rate).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Total).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<Quotation>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.QuotationNo }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.DateIssued });
            entity.HasIndex(x => x.CourierVesselId);
            entity.HasIndex(x => new { x.TenantId, x.EmailStatus });
            entity.Property(x => x.QuotationNo).HasMaxLength(50);
            entity.Property(x => x.PoNumber).HasMaxLength(100);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TaxRate).HasColumnType("numeric(8,4)");
            entity.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.GrandTotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.EmailStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.LastEmailedTo).HasMaxLength(200);
            entity.Property(x => x.LastEmailedCc).HasMaxLength(500);
            entity.HasOne(x => x.CourierVessel)
                .WithMany()
                .HasForeignKey(x => x.CourierVesselId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QuotationItem>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(400);
            entity.Property(x => x.Qty).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Rate).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Total).HasColumnType("numeric(18,2)");
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
            entity.Property(x => x.IdNumber).HasMaxLength(100);
            entity.Property(x => x.PhoneNumber).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(200);
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

        modelBuilder.Entity<StaffConductForm>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.FormNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.StaffId, x.IssueDate });
            entity.HasIndex(x => new { x.TenantId, x.FormType, x.IssueDate });
            entity.HasIndex(x => new { x.TenantId, x.Status, x.IssueDate });
            entity.Property(x => x.FormNumber).HasMaxLength(50);
            entity.Property(x => x.FormType).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Subject).HasMaxLength(200);
            entity.Property(x => x.IncidentDetails).HasMaxLength(2000);
            entity.Property(x => x.ActionTaken).HasMaxLength(1000);
            entity.Property(x => x.RequiredImprovement).HasMaxLength(1000);
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.IssuedBy).HasMaxLength(150);
            entity.Property(x => x.WitnessedBy).HasMaxLength(150);
            entity.Property(x => x.EmployeeRemarks).HasMaxLength(1000);
            entity.Property(x => x.ResolutionNotes).HasMaxLength(1000);
            entity.Property(x => x.SubjectDv).HasMaxLength(200);
            entity.Property(x => x.IncidentDetailsDv).HasMaxLength(2000);
            entity.Property(x => x.ActionTakenDv).HasMaxLength(1000);
            entity.Property(x => x.RequiredImprovementDv).HasMaxLength(1000);
            entity.Property(x => x.EmployeeRemarksDv).HasMaxLength(1000);
            entity.Property(x => x.AcknowledgementDv).HasMaxLength(1000);
            entity.Property(x => x.ResolutionNotesDv).HasMaxLength(1000);
            entity.Property(x => x.StaffCodeSnapshot).HasMaxLength(40);
            entity.Property(x => x.StaffNameSnapshot).HasMaxLength(200);
            entity.Property(x => x.DesignationSnapshot).HasMaxLength(120);
            entity.Property(x => x.WorkSiteSnapshot).HasMaxLength(120);
            entity.Property(x => x.IdNumberSnapshot).HasMaxLength(100);
            entity.HasOne(x => x.Staff)
                .WithMany(x => x.ConductForms)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StaffConductExportDocument>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.StaffConductFormId, x.Language }).IsUnique();
            entity.Property(x => x.FormType).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Language).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(150);
            entity.Property(x => x.ContentHash).HasMaxLength(128);
            entity.Property(x => x.FileSizeBytes).HasColumnType("bigint");
            entity.HasOne(x => x.StaffConductForm)
                .WithMany(x => x.ExportDocuments)
                .HasForeignKey(x => x.StaffConductFormId)
                .OnDelete(DeleteBehavior.Cascade);
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
