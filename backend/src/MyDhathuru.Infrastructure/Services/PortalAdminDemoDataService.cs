using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.PortalAdmin.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Persistence;
using MyDhathuru.Infrastructure.Security;

namespace MyDhathuru.Infrastructure.Services;

public class PortalAdminDemoDataService : IPortalAdminDemoDataService
{
    private const string DemoStaffPassword = "Demo@12345";

    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;

    public PortalAdminDemoDataService(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<PortalAdminDemoDataSeedResultDto> SeedBusinessDemoDataAsync(
        Guid tenantId,
        Guid performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (performedByUserId == Guid.Empty)
        {
            throw new AppException("Portal admin context is required to generate demo data.");
        }

        var tenant = await _dbContext.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Business not found.");

        if (!tenant.IsDataTesting)
        {
            throw new AppException("Only businesses marked as data testing can be seeded with demo data.");
        }

        var primaryAdmin = await _dbContext.Users.IgnoreQueryFilters()
            .Include(x => x.Role)
            .Where(x => !x.IsDeleted && x.TenantId == tenantId && x.Role.Name == UserRoleName.Admin)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Primary admin user not found for this business.");

        var staffRole = await EnsureRoleAsync(UserRoleName.Staff, "Tenant staff user", cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await ClearExistingTenantDataAsync(tenantId, primaryAdmin.Id, cancellationToken);

        var generatedAt = DateTimeOffset.UtcNow;
        var year = generatedAt.Year;

        var tenantSettings = await UpsertTenantSettingsAsync(tenant, primaryAdmin, cancellationToken);

        var expenseCategories = ExpenseCategoryService.BuildDefaultCategories(tenantId).ToList();
        var cogsCategory = new ExpenseCategory
        {
            TenantId = tenantId,
            Name = "Inventory / COGS",
            Code = "COGS",
            Description = "Inventory and direct cost purchases used for BPT cost of goods sold demo data.",
            BptCategoryCode = BptCategoryCode.CostOfGoodsSold,
            IsActive = true,
            IsSystem = true,
            SortOrder = 15
        };
        expenseCategories.Add(cogsCategory);
        _dbContext.ExpenseCategories.AddRange(expenseCategories);

        var categoriesByCode = expenseCategories.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        var customers = BuildCustomers(tenantId);
        var customerContacts = BuildCustomerContacts(tenantId, customers);
        var customerOpeningBalances = BuildCustomerOpeningBalances(tenantId, year, customers);

        var suppliers = BuildSuppliers(tenantId);
        var vessels = BuildVessels(tenantId);
        var staff = BuildStaff(tenantId);
        var staffUsers = BuildStaffUsers(tenantId, staffRole, staff);

        var quotations = BuildQuotations(tenantId, customers, vessels);
        var quotationItems = BuildQuotationItems(tenantId, quotations);

        var deliveryNotes = BuildDeliveryNotes(tenantId, customers, vessels);
        var deliveryNoteItems = BuildDeliveryNoteItems(tenantId, deliveryNotes);

        var invoices = BuildInvoices(tenantId, customers, vessels, quotations, deliveryNotes);
        var invoiceItems = BuildInvoiceItems(tenantId, invoices);
        var invoicePayments = BuildInvoicePayments(tenantId, invoices);

        var purchaseOrders = BuildPurchaseOrders(tenantId, suppliers, vessels);
        var purchaseOrderItems = BuildPurchaseOrderItems(tenantId, purchaseOrders);

        var exchangeRates = BuildExchangeRates(tenantId);

        var receivedInvoices = BuildReceivedInvoices(tenantId, suppliers, primaryAdmin.Id, tenantSettings, categoriesByCode);
        var receivedInvoiceItems = BuildReceivedInvoiceItems(tenantId, receivedInvoices);

        var expenseEntries = BuildExpenseEntries(tenantId, suppliers, categoriesByCode);
        var rentEntries = BuildRentEntries(tenantId, categoriesByCode);

        var paymentVouchers = BuildPaymentVouchers(tenantId, receivedInvoices, expenseEntries);
        var receivedInvoicePayments = BuildReceivedInvoicePayments(tenantId, receivedInvoices, paymentVouchers);
        ApplyReceivedInvoiceSettlements(receivedInvoices, receivedInvoicePayments);

        var payrollPeriods = BuildPayrollPeriods(tenantId);
        var payrollEntries = BuildPayrollEntries(tenantId, payrollPeriods, staff);
        var salarySlips = BuildSalarySlips(tenantId, payrollEntries);
        ApplyPayrollTotals(payrollPeriods, payrollEntries);

        var staffConductForms = BuildStaffConductForms(tenantId, staff);
        var otherIncomeEntries = BuildOtherIncomeEntries(tenantId, customers);
        var salesAdjustments = BuildSalesAdjustments(tenantId, customers, invoices);
        var documentSequences = BuildDocumentSequences(tenantId, year);

        _dbContext.Customers.AddRange(customers);
        _dbContext.CustomerContacts.AddRange(customerContacts);
        _dbContext.CustomerOpeningBalances.AddRange(customerOpeningBalances);
        _dbContext.Suppliers.AddRange(suppliers);
        _dbContext.Vessels.AddRange(vessels);
        _dbContext.Staff.AddRange(staff);
        _dbContext.Users.AddRange(staffUsers);
        _dbContext.Quotations.AddRange(quotations);
        _dbContext.QuotationItems.AddRange(quotationItems);
        _dbContext.DeliveryNotes.AddRange(deliveryNotes);
        _dbContext.DeliveryNoteItems.AddRange(deliveryNoteItems);
        _dbContext.Invoices.AddRange(invoices);
        _dbContext.InvoiceItems.AddRange(invoiceItems);
        _dbContext.InvoicePayments.AddRange(invoicePayments);
        _dbContext.PurchaseOrders.AddRange(purchaseOrders);
        _dbContext.PurchaseOrderItems.AddRange(purchaseOrderItems);
        _dbContext.ExchangeRates.AddRange(exchangeRates);
        _dbContext.ReceivedInvoices.AddRange(receivedInvoices);
        _dbContext.ReceivedInvoiceItems.AddRange(receivedInvoiceItems);
        _dbContext.ExpenseEntries.AddRange(expenseEntries);
        _dbContext.RentEntries.AddRange(rentEntries);
        _dbContext.PaymentVouchers.AddRange(paymentVouchers);
        _dbContext.ReceivedInvoicePayments.AddRange(receivedInvoicePayments);
        _dbContext.PayrollPeriods.AddRange(payrollPeriods);
        _dbContext.PayrollEntries.AddRange(payrollEntries);
        _dbContext.SalarySlips.AddRange(salarySlips);
        _dbContext.StaffConductForms.AddRange(staffConductForms);
        _dbContext.OtherIncomeEntries.AddRange(otherIncomeEntries);
        _dbContext.SalesAdjustments.AddRange(salesAdjustments);
        _dbContext.DocumentSequences.AddRange(documentSequences);

        tenant.DemoDataGeneratedAt = generatedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PortalAdminDemoDataSeedResultDto
        {
            TenantId = tenant.Id,
            CompanyName = tenant.CompanyName,
            CustomersCreated = customers.Count,
            SuppliersCreated = suppliers.Count,
            VesselsCreated = vessels.Count,
            StaffCreated = staff.Count,
            QuotationsCreated = quotations.Count,
            DeliveryNotesCreated = deliveryNotes.Count,
            InvoicesCreated = invoices.Count,
            ReceivedInvoicesCreated = receivedInvoices.Count,
            PurchaseOrdersCreated = purchaseOrders.Count,
            PaymentVouchersCreated = paymentVouchers.Count,
            ExpenseEntriesCreated = expenseEntries.Count,
            RentEntriesCreated = rentEntries.Count,
            PayrollPeriodsCreated = payrollPeriods.Count,
            StaffConductFormsCreated = staffConductForms.Count,
            ExchangeRatesCreated = exchangeRates.Count,
            OtherIncomeEntriesCreated = otherIncomeEntries.Count,
            SalesAdjustmentsCreated = salesAdjustments.Count,
            GeneratedAt = generatedAt
        };
    }

    private async Task ClearExistingTenantDataAsync(Guid tenantId, Guid primaryAdminId, CancellationToken cancellationToken)
    {
        var tenantUserIds = await _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.TenantId == tenantId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await _dbContext.RefreshTokens.IgnoreQueryFilters().Where(x => tenantUserIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PasswordResetTokens.IgnoreQueryFilters().Where(x => tenantUserIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.BusinessAuditLogs.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.SalarySlips.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PayrollEntries.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PayrollPeriods.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.StaffConductForms.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InvoicePayments.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InvoiceItems.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Invoices.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.DeliveryNoteItems.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.DeliveryNotes.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.QuotationItems.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Quotations.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PurchaseOrderItems.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PurchaseOrders.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ReceivedInvoicePayments.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PaymentVouchers.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ReceivedInvoiceAttachments.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ReceivedInvoiceItems.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ReceivedInvoices.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RentEntries.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ExpenseEntries.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CustomerOpeningBalances.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CustomerContacts.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.SalesAdjustments.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.OtherIncomeEntries.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ExchangeRates.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.BptAdjustments.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.BptMappingRules.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.BptCategories.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.DocumentSequences.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ExpenseCategories.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Staff.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Vessels.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Suppliers.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Customers.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Id != primaryAdminId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<TenantSettings> UpsertTenantSettingsAsync(Tenant tenant, User primaryAdmin, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.TenantSettings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && !x.IsDeleted, cancellationToken);
        var isNew = settings is null;

        settings ??= new TenantSettings
        {
            TenantId = tenant.Id,
            CompanyName = tenant.CompanyName,
            CompanyEmail = tenant.CompanyEmail,
            CompanyPhone = tenant.CompanyPhone,
            TinNumber = tenant.TinNumber,
            BusinessRegistrationNumber = tenant.BusinessRegistrationNumber
        };

        settings.Username = primaryAdmin.FullName;
        settings.CompanyName = tenant.CompanyName;
        settings.CompanyEmail = tenant.CompanyEmail;
        settings.CompanyPhone = tenant.CompanyPhone;
        settings.TinNumber = tenant.TinNumber;
        settings.BusinessRegistrationNumber = tenant.BusinessRegistrationNumber;
        settings.InvoicePrefix = "INV";
        settings.DeliveryNotePrefix = "DN";
        settings.QuotePrefix = "QT";
        settings.PurchaseOrderPrefix = "PO";
        settings.ReceivedInvoicePrefix = "RI";
        settings.PaymentVoucherPrefix = "PV";
        settings.RentEntryPrefix = "RNT";
        settings.WarningFormPrefix = "WF";
        settings.StatementPrefix = "ST";
        settings.SalarySlipPrefix = "SAL";
        settings.IsTaxApplicable = true;
        settings.DefaultTaxRate = 0.08m;
        settings.DefaultDueDays = 14;
        settings.DefaultCurrency = "MVR";
        settings.TaxableActivityNumber = "1005491GST501";
        settings.IsInputTaxClaimEnabled = true;
        settings.BmlMvrAccountName = $"{tenant.CompanyName} Operations";
        settings.BmlMvrAccountNumber = "200017523456";
        settings.BmlUsdAccountName = $"{tenant.CompanyName} USD Collection";
        settings.BmlUsdAccountNumber = "100017523456";
        settings.MibMvrAccountName = $"{tenant.CompanyName} Payroll";
        settings.MibMvrAccountNumber = "770017523456";
        settings.MibUsdAccountName = $"{tenant.CompanyName} Treasury";
        settings.MibUsdAccountNumber = "660017523456";
        settings.InvoiceOwnerName = primaryAdmin.FullName;
        settings.InvoiceOwnerIdCard = "A123456";

        if (isNew)
        {
            _dbContext.TenantSettings.Add(settings);
        }

        return settings;
    }

    private async Task<Role> EnsureRoleAsync(string roleName, string description, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);

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

    private List<Customer> BuildCustomers(Guid tenantId)
    {
        return
        [
            new Customer { TenantId = tenantId, Name = "Blue Lagoon Trading", TinNumber = "GST7701", Phone = "+9607711200", Email = "accounts@bluelagoon.mv" },
            new Customer { TenantId = tenantId, Name = "Harborline Retail", TinNumber = "GST7702", Phone = "+9607711300", Email = "finance@harborline.mv" },
            new Customer { TenantId = tenantId, Name = "Island Fresh Market", TinNumber = "GST7703", Phone = "+9607711400", Email = "procurement@islandfresh.mv" },
            new Customer { TenantId = tenantId, Name = "Atoll Engineering", TinNumber = "GST7704", Phone = "+9607711500", Email = "admin@atolleng.mv" }
        ];
    }

    private List<CustomerContact> BuildCustomerContacts(Guid tenantId, IReadOnlyList<Customer> customers)
    {
        return
        [
            new CustomerContact { TenantId = tenantId, Customer = customers[0], Label = "Accounts", Value = "Rasheed - +9607778811" },
            new CustomerContact { TenantId = tenantId, Customer = customers[1], Label = "Collections", Value = "Nadhee - +9607778822" },
            new CustomerContact { TenantId = tenantId, Customer = customers[2], Label = "Buyer", Value = "Fathuna - procurement@islandfresh.mv" },
            new CustomerContact { TenantId = tenantId, Customer = customers[3], Label = "Projects", Value = "Shiyaam - +9607778844" }
        ];
    }

    private List<CustomerOpeningBalance> BuildCustomerOpeningBalances(Guid tenantId, int year, IReadOnlyList<Customer> customers)
    {
        return
        [
            new CustomerOpeningBalance
            {
                TenantId = tenantId,
                Customer = customers[0],
                Year = year,
                OpeningBalanceMvr = 18500m,
                OpeningBalanceUsd = 0m,
                Notes = "Brought forward project balance."
            },
            new CustomerOpeningBalance
            {
                TenantId = tenantId,
                Customer = customers[1],
                Year = year,
                OpeningBalanceMvr = 0m,
                OpeningBalanceUsd = 420m,
                Notes = "USD statement opening balance."
            }
        ];
    }

    private List<Supplier> BuildSuppliers(Guid tenantId)
    {
        return
        [
            new Supplier { TenantId = tenantId, Name = "Seaside Wholesale", TinNumber = "GST8801", ContactNumber = "+9607751100", Email = "supply@seaside.mv", Address = "Male, Maldives", Notes = "Primary inventory supplier." },
            new Supplier { TenantId = tenantId, Name = "North Harbor Fuel", TinNumber = "GST8802", ContactNumber = "+9607752200", Email = "billing@northharborfuel.mv", Address = "Hulhumale, Maldives", Notes = "Fuel and transport support." },
            new Supplier { TenantId = tenantId, Name = "Orbit Office Solutions", TinNumber = "GST8803", ContactNumber = "+9607753300", Email = "accounts@orbitoffice.mv", Address = "Male, Maldives", Notes = "Office and print supplies." },
            new Supplier { TenantId = tenantId, Name = "Coastal Engineering Works", TinNumber = "GST8804", ContactNumber = "+9607754400", Email = "projects@coastaleng.mv", Address = "Villimale, Maldives", Notes = "Repair and maintenance partner." }
        ];
    }

    private List<Vessel> BuildVessels(Guid tenantId)
    {
        return
        [
            new Vessel { TenantId = tenantId, Name = "MV Horizon One", RegistrationNumber = "MV-2026-101", IssuedDate = new DateOnly(2024, 5, 12), PassengerCapacity = 120, VesselType = "Cargo Ferry", HomePort = "Male", OwnerName = "myDhathuru Demo Operations", ContactPhone = "+9607515618", Notes = "Primary inter-atoll delivery vessel." },
            new Vessel { TenantId = tenantId, Name = "MV Coral Route", RegistrationNumber = "MV-2026-102", IssuedDate = new DateOnly(2023, 8, 18), PassengerCapacity = 80, VesselType = "Supply Vessel", HomePort = "Hulhumale", OwnerName = "myDhathuru Demo Operations", ContactPhone = "+9607515618", Notes = "Receivables and dispatch support vessel." },
            new Vessel { TenantId = tenantId, Name = "MV Atoll Runner", RegistrationNumber = "MV-2026-103", IssuedDate = new DateOnly(2022, 11, 7), PassengerCapacity = 65, VesselType = "Fast Ferry", HomePort = "Male", OwnerName = "myDhathuru Demo Operations", ContactPhone = "+9607515618", Notes = "Fast route service vessel." }
        ];
    }

    private List<Staff> BuildStaff(Guid tenantId)
    {
        return
        [
            new Staff
            {
                TenantId = tenantId,
                StaffId = "STF-001",
                StaffName = "Aishath Naza",
                IdNumber = "A350001",
                PhoneNumber = "+9607901101",
                Email = "operations.lead@mydhathuru-demo.com",
                HiredDate = new DateOnly(2024, 1, 15),
                Designation = "Operations Lead",
                WorkSite = "Male",
                BankName = "BML",
                AccountName = "Aishath Naza",
                AccountNumber = "7701101001",
                Basic = 12000m,
                ServiceAllowance = 1800m,
                OtherAllowance = 500m,
                PhoneAllowance = 300m,
                FoodRate = 125m
            },
            new Staff
            {
                TenantId = tenantId,
                StaffId = "STF-002",
                StaffName = "Ahmed Riyaz",
                IdNumber = "A350002",
                PhoneNumber = "+9607901102",
                Email = "finance.exec@mydhathuru-demo.com",
                HiredDate = new DateOnly(2024, 3, 10),
                Designation = "Finance Executive",
                WorkSite = "Male",
                BankName = "BML",
                AccountName = "Ahmed Riyaz",
                AccountNumber = "7701101002",
                Basic = 9800m,
                ServiceAllowance = 1250m,
                OtherAllowance = 350m,
                PhoneAllowance = 250m,
                FoodRate = 110m
            },
            new Staff
            {
                TenantId = tenantId,
                StaffId = "STF-003",
                StaffName = "Mariyam Shina",
                IdNumber = "A350003",
                PhoneNumber = "+9607901103",
                Email = "payroll.coord@mydhathuru-demo.com",
                HiredDate = new DateOnly(2024, 6, 1),
                Designation = "Payroll Coordinator",
                WorkSite = "Hulhumale",
                BankName = "MIB",
                AccountName = "Mariyam Shina",
                AccountNumber = "7701101003",
                Basic = 8800m,
                ServiceAllowance = 950m,
                OtherAllowance = 300m,
                PhoneAllowance = 200m,
                FoodRate = 95m
            },
            new Staff
            {
                TenantId = tenantId,
                StaffId = "STF-004",
                StaffName = "Mohamed Jinan",
                IdNumber = "A350004",
                PhoneNumber = "+9607901104",
                Email = "fleet.supervisor@mydhathuru-demo.com",
                HiredDate = new DateOnly(2025, 1, 5),
                Designation = "Fleet Supervisor",
                WorkSite = "Male",
                BankName = "BML",
                AccountName = "Mohamed Jinan",
                AccountNumber = "7701101004",
                Basic = 9300m,
                ServiceAllowance = 1100m,
                OtherAllowance = 250m,
                PhoneAllowance = 220m,
                FoodRate = 100m
            }
        ];
    }

    private List<User> BuildStaffUsers(Guid tenantId, Role staffRole, IReadOnlyList<Staff> staff)
    {
        return
        [
            CreateDemoUser(tenantId, staffRole, staff[0].StaffName, BuildDemoUserEmail(tenantId, "opsdemo")),
            CreateDemoUser(tenantId, staffRole, staff[1].StaffName, BuildDemoUserEmail(tenantId, "financedemo"))
        ];
    }

    private static string BuildDemoUserEmail(Guid tenantId, string alias)
    {
        var tenantKey = tenantId.ToString("N")[..12];
        return $"mydhathuru+{alias}-{tenantKey}@gmail.com";
    }

    private User CreateDemoUser(Guid tenantId, Role role, string fullName, string email)
    {
        var (hash, salt) = _passwordHasher.HashPassword(DemoStaffPassword);
        return new User
        {
            TenantId = tenantId,
            Role = role,
            FullName = fullName,
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true
        };
    }

    private List<Quotation> BuildQuotations(Guid tenantId, IReadOnlyList<Customer> customers, IReadOnlyList<Vessel> vessels)
    {
        return
        [
            new Quotation
            {
                TenantId = tenantId,
                QuotationNo = "QT-2026-001",
                Customer = customers[0],
                CourierVessel = vessels[0],
                DateIssued = new DateOnly(2026, 1, 8),
                ValidUntil = new DateOnly(2026, 1, 22),
                PoNumber = "BLT-PO-2201",
                Currency = "MVR",
                Subtotal = 11850m,
                TaxRate = 0.08m,
                TaxAmount = 948m,
                GrandTotal = 12798m,
                EmailStatus = DocumentEmailStatus.Emailed,
                LastEmailedAt = new DateTimeOffset(2026, 1, 8, 9, 15, 0, TimeSpan.Zero),
                LastEmailedTo = customers[0].Email,
                Notes = "Quarter opening supply quote."
            },
            new Quotation
            {
                TenantId = tenantId,
                QuotationNo = "QT-2026-002",
                Customer = customers[2],
                CourierVessel = vessels[1],
                DateIssued = new DateOnly(2026, 2, 2),
                ValidUntil = new DateOnly(2026, 2, 16),
                PoNumber = "IFM-PO-1902",
                Currency = "USD",
                Subtotal = 940m,
                TaxRate = 0m,
                TaxAmount = 0m,
                GrandTotal = 940m,
                EmailStatus = DocumentEmailStatus.Pending,
                Notes = "Export goods quotation."
            },
            new Quotation
            {
                TenantId = tenantId,
                QuotationNo = "QT-2026-003",
                Customer = customers[3],
                CourierVessel = vessels[2],
                DateIssued = new DateOnly(2026, 3, 3),
                ValidUntil = new DateOnly(2026, 3, 17),
                PoNumber = "ATL-PO-0303",
                Currency = "MVR",
                Subtotal = 6800m,
                TaxRate = 0.08m,
                TaxAmount = 544m,
                GrandTotal = 7344m,
                EmailStatus = DocumentEmailStatus.Emailed,
                LastEmailedAt = new DateTimeOffset(2026, 3, 3, 8, 45, 0, TimeSpan.Zero),
                LastEmailedTo = customers[3].Email,
                Notes = "Dockside equipment handling quote."
            }
        ];
    }

    private List<QuotationItem> BuildQuotationItems(Guid tenantId, IReadOnlyList<Quotation> quotations)
    {
        return
        [
            new QuotationItem { TenantId = tenantId, Quotation = quotations[0], Description = "Inbound goods handling", Qty = 45m, Rate = 120m, Total = 5400m },
            new QuotationItem { TenantId = tenantId, Quotation = quotations[0], Description = "Inter-atoll dispatch lane", Qty = 3m, Rate = 2150m, Total = 6450m },
            new QuotationItem { TenantId = tenantId, Quotation = quotations[1], Description = "Cold-chain shipment batch", Qty = 4m, Rate = 235m, Total = 940m },
            new QuotationItem { TenantId = tenantId, Quotation = quotations[2], Description = "Dock crane support", Qty = 2m, Rate = 1800m, Total = 3600m },
            new QuotationItem { TenantId = tenantId, Quotation = quotations[2], Description = "Engineering support crew", Qty = 2m, Rate = 1600m, Total = 3200m }
        ];
    }

    private List<DeliveryNote> BuildDeliveryNotes(Guid tenantId, IReadOnlyList<Customer> customers, IReadOnlyList<Vessel> vessels)
    {
        return
        [
            new DeliveryNote
            {
                TenantId = tenantId,
                DeliveryNoteNo = "DN-2026-001",
                PoNumber = "BLT-PO-2201",
                Currency = "MVR",
                Date = new DateOnly(2026, 1, 14),
                Customer = customers[0],
                Vessel = vessels[0],
                VesselPaymentFee = 950m,
                Notes = "Goods delivered to central warehouse."
            },
            new DeliveryNote
            {
                TenantId = tenantId,
                DeliveryNoteNo = "DN-2026-002",
                PoNumber = "IFM-PO-1902",
                Currency = "USD",
                Date = new DateOnly(2026, 2, 18),
                Customer = customers[2],
                Vessel = vessels[1],
                VesselPaymentFee = 0m,
                Notes = "USD export dispatch."
            },
            new DeliveryNote
            {
                TenantId = tenantId,
                DeliveryNoteNo = "DN-2026-003",
                PoNumber = "ATL-PO-0303",
                Currency = "MVR",
                Date = new DateOnly(2026, 3, 9),
                Customer = customers[3],
                Vessel = vessels[2],
                VesselPaymentFee = 650m,
                Notes = "Engineering materials delivered."
            }
        ];
    }

    private List<DeliveryNoteItem> BuildDeliveryNoteItems(Guid tenantId, IReadOnlyList<DeliveryNote> deliveryNotes)
    {
        return
        [
            new DeliveryNoteItem { TenantId = tenantId, DeliveryNote = deliveryNotes[0], Details = "Dry goods crates", Qty = 40m, Rate = 120m, Total = 4800m, CashPayment = 2400m, VesselPayment = 2400m },
            new DeliveryNoteItem { TenantId = tenantId, DeliveryNote = deliveryNotes[0], Details = "Dispatch documentation", Qty = 1m, Rate = 1500m, Total = 1500m, CashPayment = 1500m, VesselPayment = 0m },
            new DeliveryNoteItem { TenantId = tenantId, DeliveryNote = deliveryNotes[1], Details = "Chilled produce pallets", Qty = 4m, Rate = 205m, Total = 820m, CashPayment = 0m, VesselPayment = 820m },
            new DeliveryNoteItem { TenantId = tenantId, DeliveryNote = deliveryNotes[2], Details = "Engineering consumables", Qty = 12m, Rate = 320m, Total = 3840m, CashPayment = 1920m, VesselPayment = 1920m },
            new DeliveryNoteItem { TenantId = tenantId, DeliveryNote = deliveryNotes[2], Details = "Loading support", Qty = 2m, Rate = 900m, Total = 1800m, CashPayment = 1800m, VesselPayment = 0m }
        ];
    }

    private List<Invoice> BuildInvoices(
        Guid tenantId,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<Vessel> vessels,
        IReadOnlyList<Quotation> quotations,
        IReadOnlyList<DeliveryNote> deliveryNotes)
    {
        return
        [
            new Invoice
            {
                TenantId = tenantId,
                InvoiceNo = "INV-2026-001",
                Customer = customers[0],
                Quotation = quotations[0],
                DeliveryNote = deliveryNotes[0],
                CourierVessel = vessels[0],
                DateIssued = new DateOnly(2026, 1, 15),
                DateDue = new DateOnly(2026, 1, 29),
                PoNumber = "BLT-PO-2201",
                Currency = "MVR",
                Subtotal = 6300m,
                TaxRate = 0.08m,
                TaxAmount = 504m,
                GrandTotal = 6804m,
                AmountPaid = 6804m,
                Balance = 0m,
                PaymentStatus = PaymentStatus.Paid,
                EmailStatus = DocumentEmailStatus.Emailed,
                LastEmailedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero),
                LastEmailedTo = customers[0].Email,
                Notes = "Converted from quotation and delivery note."
            },
            new Invoice
            {
                TenantId = tenantId,
                InvoiceNo = "INV-2026-002",
                Customer = customers[1],
                CourierVessel = vessels[0],
                DateIssued = new DateOnly(2026, 2, 4),
                DateDue = new DateOnly(2026, 2, 18),
                PoNumber = "HR-PO-0204",
                Currency = "MVR",
                Subtotal = 9550m,
                TaxRate = 0.08m,
                TaxAmount = 764m,
                GrandTotal = 10314m,
                AmountPaid = 4300m,
                Balance = 6014m,
                PaymentStatus = PaymentStatus.Partial,
                EmailStatus = DocumentEmailStatus.Emailed,
                LastEmailedAt = new DateTimeOffset(2026, 2, 4, 9, 30, 0, TimeSpan.Zero),
                LastEmailedTo = customers[1].Email,
                Notes = "Monthly collections account."
            },
            new Invoice
            {
                TenantId = tenantId,
                InvoiceNo = "INV-2026-003",
                Customer = customers[2],
                Quotation = quotations[1],
                DeliveryNote = deliveryNotes[1],
                CourierVessel = vessels[1],
                DateIssued = new DateOnly(2026, 2, 18),
                DateDue = new DateOnly(2026, 3, 4),
                PoNumber = "IFM-PO-1902",
                Currency = "USD",
                Subtotal = 820m,
                TaxRate = 0m,
                TaxAmount = 0m,
                GrandTotal = 820m,
                AmountPaid = 0m,
                Balance = 820m,
                PaymentStatus = PaymentStatus.Unpaid,
                EmailStatus = DocumentEmailStatus.Pending,
                Notes = "USD delivery for export route."
            },
            new Invoice
            {
                TenantId = tenantId,
                InvoiceNo = "INV-2026-004",
                Customer = customers[3],
                DeliveryNote = deliveryNotes[2],
                CourierVessel = vessels[2],
                DateIssued = new DateOnly(2026, 3, 10),
                DateDue = new DateOnly(2026, 3, 24),
                PoNumber = "ATL-PO-0303",
                Currency = "MVR",
                Subtotal = 5645m,
                TaxRate = 0.08m,
                TaxAmount = 452m,
                GrandTotal = 6097m,
                AmountPaid = 6097m,
                Balance = 0m,
                PaymentStatus = PaymentStatus.Paid,
                EmailStatus = DocumentEmailStatus.Emailed,
                LastEmailedAt = new DateTimeOffset(2026, 3, 10, 8, 20, 0, TimeSpan.Zero),
                LastEmailedTo = customers[3].Email,
                Notes = "March engineering dispatch."
            }
        ];
    }

    private List<InvoiceItem> BuildInvoiceItems(Guid tenantId, IReadOnlyList<Invoice> invoices)
    {
        return
        [
            new InvoiceItem { TenantId = tenantId, Invoice = invoices[0], Description = "Dry goods crates", Qty = 40m, Rate = 120m, Total = 4800m },
            new InvoiceItem { TenantId = tenantId, Invoice = invoices[0], Description = "Dispatch documentation", Qty = 1m, Rate = 1500m, Total = 1500m },
            new InvoiceItem { TenantId = tenantId, Invoice = invoices[1], Description = "Monthly retail replenishment", Qty = 5m, Rate = 1310m, Total = 6550m },
            new InvoiceItem { TenantId = tenantId, Invoice = invoices[1], Description = "Harbor delivery support", Qty = 2m, Rate = 1500m, Total = 3000m },
            new InvoiceItem { TenantId = tenantId, Invoice = invoices[2], Description = "Chilled produce pallets", Qty = 4m, Rate = 205m, Total = 820m },
            new InvoiceItem { TenantId = tenantId, Invoice = invoices[3], Description = "Engineering consumables", Qty = 12m, Rate = 320m, Total = 3840m },
            new InvoiceItem { TenantId = tenantId, Invoice = invoices[3], Description = "Loading support", Qty = 2m, Rate = 902.5m, Total = 1805m }
        ];
    }

    private List<InvoicePayment> BuildInvoicePayments(Guid tenantId, IReadOnlyList<Invoice> invoices)
    {
        return
        [
            new InvoicePayment { TenantId = tenantId, Invoice = invoices[0], Currency = "MVR", Amount = 6804m, PaymentDate = new DateOnly(2026, 1, 21), Method = PaymentMethod.Transfer, Reference = "RCV-INV001", Notes = "Paid in full." },
            new InvoicePayment { TenantId = tenantId, Invoice = invoices[1], Currency = "MVR", Amount = 4300m, PaymentDate = new DateOnly(2026, 2, 17), Method = PaymentMethod.Transfer, Reference = "RCV-INV002-A", Notes = "Part payment received." },
            new InvoicePayment { TenantId = tenantId, Invoice = invoices[3], Currency = "MVR", Amount = 6097m, PaymentDate = new DateOnly(2026, 3, 16), Method = PaymentMethod.Cheque, Reference = "CHQ-6097", Notes = "Paid against engineering account." }
        ];
    }

    private List<PurchaseOrder> BuildPurchaseOrders(Guid tenantId, IReadOnlyList<Supplier> suppliers, IReadOnlyList<Vessel> vessels)
    {
        return
        [
            new PurchaseOrder
            {
                TenantId = tenantId,
                PurchaseOrderNo = "PO-2026-001",
                Supplier = suppliers[0],
                CourierVessel = vessels[0],
                DateIssued = new DateOnly(2026, 1, 5),
                RequiredDate = new DateOnly(2026, 1, 18),
                Currency = "MVR",
                Subtotal = 14200m,
                TaxRate = 0m,
                TaxAmount = 0m,
                GrandTotal = 14200m,
                EmailStatus = DocumentEmailStatus.Emailed,
                LastEmailedAt = new DateTimeOffset(2026, 1, 5, 11, 30, 0, TimeSpan.Zero),
                LastEmailedTo = suppliers[0].Email,
                Notes = "Inventory replenishment order."
            },
            new PurchaseOrder
            {
                TenantId = tenantId,
                PurchaseOrderNo = "PO-2026-002",
                Supplier = suppliers[2],
                CourierVessel = vessels[1],
                DateIssued = new DateOnly(2026, 2, 9),
                RequiredDate = new DateOnly(2026, 2, 20),
                Currency = "USD",
                Subtotal = 560m,
                TaxRate = 0m,
                TaxAmount = 0m,
                GrandTotal = 560m,
                EmailStatus = DocumentEmailStatus.Pending,
                Notes = "Imported office equipment order."
            },
            new PurchaseOrder
            {
                TenantId = tenantId,
                PurchaseOrderNo = "PO-2026-003",
                Supplier = suppliers[3],
                CourierVessel = vessels[2],
                DateIssued = new DateOnly(2026, 3, 1),
                RequiredDate = new DateOnly(2026, 3, 12),
                Currency = "MVR",
                Subtotal = 7800m,
                TaxRate = 0.08m,
                TaxAmount = 624m,
                GrandTotal = 8424m,
                EmailStatus = DocumentEmailStatus.Emailed,
                LastEmailedAt = new DateTimeOffset(2026, 3, 1, 9, 10, 0, TimeSpan.Zero),
                LastEmailedTo = suppliers[3].Email,
                Notes = "Repair materials order."
            }
        ];
    }

    private List<PurchaseOrderItem> BuildPurchaseOrderItems(Guid tenantId, IReadOnlyList<PurchaseOrder> purchaseOrders)
    {
        return
        [
            new PurchaseOrderItem { TenantId = tenantId, PurchaseOrder = purchaseOrders[0], Description = "Warehouse inventory mix", Qty = 1m, Rate = 14200m, Total = 14200m },
            new PurchaseOrderItem { TenantId = tenantId, PurchaseOrder = purchaseOrders[1], Description = "Label printer and scanners", Qty = 1m, Rate = 560m, Total = 560m },
            new PurchaseOrderItem { TenantId = tenantId, PurchaseOrder = purchaseOrders[2], Description = "Hull repair materials", Qty = 1m, Rate = 4200m, Total = 4200m },
            new PurchaseOrderItem { TenantId = tenantId, PurchaseOrder = purchaseOrders[2], Description = "Workshop safety equipment", Qty = 1m, Rate = 3600m, Total = 3600m }
        ];
    }

    private List<ExchangeRate> BuildExchangeRates(Guid tenantId)
    {
        return
        [
            new ExchangeRate { TenantId = tenantId, RateDate = new DateOnly(2026, 1, 1), Currency = "USD", RateToMvr = 15.38m, Source = "MMA", Notes = "Quarter opening reference.", IsActive = true },
            new ExchangeRate { TenantId = tenantId, RateDate = new DateOnly(2026, 2, 1), Currency = "USD", RateToMvr = 15.40m, Source = "MMA", Notes = "February reference.", IsActive = true },
            new ExchangeRate { TenantId = tenantId, RateDate = new DateOnly(2026, 3, 1), Currency = "USD", RateToMvr = 15.42m, Source = "MMA", Notes = "March opening reference.", IsActive = true },
            new ExchangeRate { TenantId = tenantId, RateDate = new DateOnly(2026, 3, 15), Currency = "USD", RateToMvr = 15.43m, Source = "Bank", Notes = "Mid-month treasury rate.", IsActive = true }
        ];
    }

    private List<ReceivedInvoice> BuildReceivedInvoices(
        Guid tenantId,
        IReadOnlyList<Supplier> suppliers,
        Guid approvedByUserId,
        TenantSettings settings,
        IReadOnlyDictionary<string, ExpenseCategory> categoriesByCode)
    {
        return
        [
            new ReceivedInvoice
            {
                TenantId = tenantId,
                InvoiceNumber = "RI-2026-001",
                Supplier = suppliers[0],
                SupplierName = suppliers[0].Name,
                SupplierTin = suppliers[0].TinNumber,
                SupplierContactNumber = suppliers[0].ContactNumber,
                SupplierEmail = suppliers[0].Email,
                InvoiceDate = new DateOnly(2026, 1, 12),
                DueDate = new DateOnly(2026, 1, 26),
                Outlet = "Warehouse",
                Description = "Inventory stock purchase",
                Notes = "Revenue purchase mapped to COGS.",
                Currency = "MVR",
                Subtotal = 14200m,
                DiscountAmount = 0m,
                GstRate = 0m,
                GstAmount = 0m,
                TotalAmount = 14200m,
                BalanceDue = 0m,
                PaymentStatus = ReceivedInvoiceStatus.Paid,
                PaymentMethod = PaymentMethod.Transfer,
                ReceiptReference = "SEASIDE-0112",
                SettlementReference = "PV-2026-001",
                BankName = "BML",
                BankAccountDetails = "200017523456",
                MiraTaxableActivityNumber = settings.TaxableActivityNumber,
                RevenueCapitalClassification = RevenueCapitalType.Revenue,
                ExpenseCategory = categoriesByCode["COGS"],
                IsTaxClaimable = false,
                ApprovalStatus = ApprovalStatus.Approved,
                ApprovedByUserId = approvedByUserId,
                ApprovedAt = new DateTimeOffset(2026, 1, 12, 10, 20, 0, TimeSpan.Zero)
            },
            new ReceivedInvoice
            {
                TenantId = tenantId,
                InvoiceNumber = "RI-2026-002",
                Supplier = suppliers[1],
                SupplierName = suppliers[1].Name,
                SupplierTin = suppliers[1].TinNumber,
                SupplierContactNumber = suppliers[1].ContactNumber,
                SupplierEmail = suppliers[1].Email,
                InvoiceDate = new DateOnly(2026, 2, 11),
                DueDate = new DateOnly(2026, 2, 20),
                Outlet = "Fleet",
                Description = "Diesel and route fuel",
                Notes = "Operating fuel invoice.",
                Currency = "MVR",
                Subtotal = 2559.23m,
                DiscountAmount = 0m,
                GstRate = 0.08m,
                GstAmount = 204.74m,
                TotalAmount = 2763.97m,
                BalanceDue = 1363.97m,
                PaymentStatus = ReceivedInvoiceStatus.Partial,
                PaymentMethod = PaymentMethod.Transfer,
                ReceiptReference = "NHF-0211",
                SettlementReference = "PV-2026-002",
                BankName = "BML",
                BankAccountDetails = "200017523456",
                MiraTaxableActivityNumber = settings.TaxableActivityNumber,
                RevenueCapitalClassification = RevenueCapitalType.Revenue,
                ExpenseCategory = categoriesByCode["DSL"],
                IsTaxClaimable = true,
                ApprovalStatus = ApprovalStatus.Approved,
                ApprovedByUserId = approvedByUserId,
                ApprovedAt = new DateTimeOffset(2026, 2, 11, 13, 0, 0, TimeSpan.Zero)
            },
            new ReceivedInvoice
            {
                TenantId = tenantId,
                InvoiceNumber = "RI-2026-003",
                Supplier = suppliers[2],
                SupplierName = suppliers[2].Name,
                SupplierTin = suppliers[2].TinNumber,
                SupplierContactNumber = suppliers[2].ContactNumber,
                SupplierEmail = suppliers[2].Email,
                InvoiceDate = new DateOnly(2026, 3, 5),
                DueDate = new DateOnly(2026, 3, 19),
                Outlet = "Admin",
                Description = "Office equipment acquisition",
                Notes = "Capital purchase for demo BPT exclusions.",
                Currency = "USD",
                Subtotal = 880m,
                DiscountAmount = 0m,
                GstRate = 0.08m,
                GstAmount = 70.40m,
                TotalAmount = 950.40m,
                BalanceDue = 950.40m,
                PaymentStatus = ReceivedInvoiceStatus.Unpaid,
                ReceiptReference = "ORB-0305",
                BankName = "MIB",
                BankAccountDetails = "660017523456",
                MiraTaxableActivityNumber = settings.TaxableActivityNumber,
                RevenueCapitalClassification = RevenueCapitalType.Capital,
                ExpenseCategory = categoriesByCode["OFF"],
                IsTaxClaimable = true,
                ApprovalStatus = ApprovalStatus.Approved,
                ApprovedByUserId = approvedByUserId,
                ApprovedAt = new DateTimeOffset(2026, 3, 5, 9, 40, 0, TimeSpan.Zero)
            },
            new ReceivedInvoice
            {
                TenantId = tenantId,
                InvoiceNumber = "RI-2026-004",
                Supplier = suppliers[3],
                SupplierName = suppliers[3].Name,
                SupplierTin = suppliers[3].TinNumber,
                SupplierContactNumber = suppliers[3].ContactNumber,
                SupplierEmail = suppliers[3].Email,
                InvoiceDate = new DateOnly(2026, 3, 8),
                DueDate = new DateOnly(2026, 3, 22),
                Outlet = "Workshop",
                Description = "March repair works",
                Notes = "Workshop maintenance support.",
                Currency = "MVR",
                Subtotal = 2339.81m,
                DiscountAmount = 0m,
                GstRate = 0.08m,
                GstAmount = 187.19m,
                TotalAmount = 2527.00m,
                BalanceDue = 0m,
                PaymentStatus = ReceivedInvoiceStatus.Paid,
                PaymentMethod = PaymentMethod.Transfer,
                ReceiptReference = "CEW-0308",
                SettlementReference = "PV-2026-003",
                BankName = "BML",
                BankAccountDetails = "200017523456",
                MiraTaxableActivityNumber = settings.TaxableActivityNumber,
                RevenueCapitalClassification = RevenueCapitalType.Revenue,
                ExpenseCategory = categoriesByCode["RPM"],
                IsTaxClaimable = true,
                ApprovalStatus = ApprovalStatus.Approved,
                ApprovedByUserId = approvedByUserId,
                ApprovedAt = new DateTimeOffset(2026, 3, 8, 15, 5, 0, TimeSpan.Zero)
            }
        ];
    }

    private List<ReceivedInvoiceItem> BuildReceivedInvoiceItems(Guid tenantId, IReadOnlyList<ReceivedInvoice> receivedInvoices)
    {
        return
        [
            new ReceivedInvoiceItem { TenantId = tenantId, ReceivedInvoice = receivedInvoices[0], Description = "Inventory stock mix", Uom = "Lot", Qty = 1m, Rate = 14200m, DiscountAmount = 0m, LineTotal = 14200m, GstRate = 0m, GstAmount = 0m },
            new ReceivedInvoiceItem { TenantId = tenantId, ReceivedInvoice = receivedInvoices[1], Description = "Fuel supply batch", Uom = "Lot", Qty = 1m, Rate = 2559.23m, DiscountAmount = 0m, LineTotal = 2559.23m, GstRate = 0.08m, GstAmount = 204.74m },
            new ReceivedInvoiceItem { TenantId = tenantId, ReceivedInvoice = receivedInvoices[2], Description = "Office equipment", Uom = "Set", Qty = 1m, Rate = 880m, DiscountAmount = 0m, LineTotal = 880m, GstRate = 0.08m, GstAmount = 70.40m },
            new ReceivedInvoiceItem { TenantId = tenantId, ReceivedInvoice = receivedInvoices[3], Description = "Repair service visit", Uom = "Job", Qty = 1m, Rate = 2339.81m, DiscountAmount = 0m, LineTotal = 2339.81m, GstRate = 0.08m, GstAmount = 187.19m }
        ];
    }

    private List<ExpenseEntry> BuildExpenseEntries(Guid tenantId, IReadOnlyList<Supplier> suppliers, IReadOnlyDictionary<string, ExpenseCategory> categoriesByCode)
    {
        return
        [
            new ExpenseEntry
            {
                TenantId = tenantId,
                SourceType = ExpenseSourceType.Manual,
                SourceId = Guid.NewGuid(),
                DocumentNumber = "EXP-2026-001",
                TransactionDate = new DateOnly(2026, 1, 18),
                ExpenseCategory = categoriesByCode["LIC"],
                Supplier = suppliers[2],
                PayeeName = "Male City Council",
                Currency = "MVR",
                NetAmount = 1800m,
                TaxAmount = 0m,
                GrossAmount = 1800m,
                ClaimableTaxAmount = 0m,
                PendingAmount = 0m,
                Description = "Annual trade license renewal",
                Notes = "License and registration fee."
            },
            new ExpenseEntry
            {
                TenantId = tenantId,
                SourceType = ExpenseSourceType.Manual,
                SourceId = Guid.NewGuid(),
                DocumentNumber = "EXP-2026-002",
                TransactionDate = new DateOnly(2026, 2, 7),
                ExpenseCategory = categoriesByCode["FRY"],
                Supplier = suppliers[1],
                PayeeName = "Hired Ferry Route Partner",
                Currency = "MVR",
                NetAmount = 3200m,
                TaxAmount = 0m,
                GrossAmount = 3200m,
                ClaimableTaxAmount = 0m,
                PendingAmount = 0m,
                Description = "Supplementary hired ferry route",
                Notes = "Peak route support."
            },
            new ExpenseEntry
            {
                TenantId = tenantId,
                SourceType = ExpenseSourceType.Manual,
                SourceId = Guid.NewGuid(),
                DocumentNumber = "EXP-2026-003",
                TransactionDate = new DateOnly(2026, 2, 20),
                ExpenseCategory = categoriesByCode["UTL"],
                PayeeName = "Dhiraagu Business",
                Currency = "MVR",
                NetAmount = 1450m,
                TaxAmount = 0m,
                GrossAmount = 1450m,
                ClaimableTaxAmount = 0m,
                PendingAmount = 0m,
                Description = "Internet and phone services",
                Notes = "Monthly utilities."
            },
            new ExpenseEntry
            {
                TenantId = tenantId,
                SourceType = ExpenseSourceType.Manual,
                SourceId = Guid.NewGuid(),
                DocumentNumber = "EXP-2026-004",
                TransactionDate = new DateOnly(2026, 3, 4),
                ExpenseCategory = categoriesByCode["SUP"],
                Supplier = suppliers[2],
                PayeeName = suppliers[2].Name,
                Currency = "MVR",
                NetAmount = 980m,
                TaxAmount = 0m,
                GrossAmount = 980m,
                ClaimableTaxAmount = 0m,
                PendingAmount = 0m,
                Description = "Office stationery and print stock",
                Notes = "Administration support."
            },
            new ExpenseEntry
            {
                TenantId = tenantId,
                SourceType = ExpenseSourceType.Manual,
                SourceId = Guid.NewGuid(),
                DocumentNumber = "EXP-2026-005",
                TransactionDate = new DateOnly(2026, 3, 6),
                ExpenseCategory = categoriesByCode["OTH"],
                PayeeName = "Operations Sundry",
                Currency = "MVR",
                NetAmount = 620m,
                TaxAmount = 0m,
                GrossAmount = 620m,
                ClaimableTaxAmount = 0m,
                PendingAmount = 0m,
                Description = "Small sundry operating purchase",
                Notes = "Other operating expense."
            }
        ];
    }

    private List<RentEntry> BuildRentEntries(Guid tenantId, IReadOnlyDictionary<string, ExpenseCategory> categoriesByCode)
    {
        return
        [
            new RentEntry
            {
                TenantId = tenantId,
                RentNumber = "RNT-2026-001",
                Date = new DateOnly(2026, 1, 31),
                PropertyName = "Male Operations Office",
                PayTo = "Capital Property Holdings",
                Currency = "MVR",
                Amount = 4750m,
                ExpenseCategory = categoriesByCode["RNT"],
                ApprovalStatus = ApprovalStatus.Approved,
                Notes = "January rent."
            },
            new RentEntry
            {
                TenantId = tenantId,
                RentNumber = "RNT-2026-002",
                Date = new DateOnly(2026, 2, 28),
                PropertyName = "Male Operations Office",
                PayTo = "Capital Property Holdings",
                Currency = "MVR",
                Amount = 4750m,
                ExpenseCategory = categoriesByCode["RNT"],
                ApprovalStatus = ApprovalStatus.Approved,
                Notes = "February rent."
            }
        ];
    }

    private List<PaymentVoucher> BuildPaymentVouchers(
        Guid tenantId,
        IReadOnlyList<ReceivedInvoice> receivedInvoices,
        IReadOnlyList<ExpenseEntry> expenseEntries)
    {
        return
        [
            new PaymentVoucher
            {
                TenantId = tenantId,
                VoucherNumber = "PV-2026-001",
                Date = new DateOnly(2026, 1, 20),
                PayTo = receivedInvoices[0].SupplierName,
                Details = "Settlement of inventory invoice RI-2026-001",
                PaymentMethod = PaymentMethod.Transfer,
                AccountNumber = "200017523456",
                Bank = "BML",
                Amount = 14200m,
                AmountInWords = "Fourteen Thousand Two Hundred Rufiyaa Only",
                ApprovedBy = "myDhathuru Demo Admin",
                ReceivedBy = "Seaside Wholesale",
                LinkedReceivedInvoice = receivedInvoices[0],
                Notes = "Paid in full.",
                Status = PaymentVoucherStatus.Posted,
                ApprovedAt = new DateTimeOffset(2026, 1, 20, 10, 0, 0, TimeSpan.Zero),
                PostedAt = new DateTimeOffset(2026, 1, 20, 10, 5, 0, TimeSpan.Zero)
            },
            new PaymentVoucher
            {
                TenantId = tenantId,
                VoucherNumber = "PV-2026-002",
                Date = new DateOnly(2026, 2, 18),
                PayTo = receivedInvoices[1].SupplierName,
                Details = "Partial settlement of diesel invoice RI-2026-002",
                PaymentMethod = PaymentMethod.Transfer,
                AccountNumber = "200017523456",
                Bank = "BML",
                Amount = 1400m,
                AmountInWords = "One Thousand Four Hundred Rufiyaa Only",
                ApprovedBy = "myDhathuru Demo Admin",
                ReceivedBy = "North Harbor Fuel",
                LinkedReceivedInvoice = receivedInvoices[1],
                Notes = "Part payment applied.",
                Status = PaymentVoucherStatus.Posted,
                ApprovedAt = new DateTimeOffset(2026, 2, 18, 11, 0, 0, TimeSpan.Zero),
                PostedAt = new DateTimeOffset(2026, 2, 18, 11, 4, 0, TimeSpan.Zero)
            },
            new PaymentVoucher
            {
                TenantId = tenantId,
                VoucherNumber = "PV-2026-003",
                Date = new DateOnly(2026, 3, 14),
                PayTo = receivedInvoices[3].SupplierName,
                Details = "Repair work settlement for RI-2026-004",
                PaymentMethod = PaymentMethod.Transfer,
                AccountNumber = "200017523456",
                Bank = "BML",
                Amount = 2527m,
                AmountInWords = "Two Thousand Five Hundred Twenty Seven Rufiyaa Only",
                ApprovedBy = "myDhathuru Demo Admin",
                ReceivedBy = "Coastal Engineering Works",
                LinkedReceivedInvoice = receivedInvoices[3],
                LinkedExpenseEntry = expenseEntries[2],
                Notes = "Repair works cleared.",
                Status = PaymentVoucherStatus.Posted,
                ApprovedAt = new DateTimeOffset(2026, 3, 14, 16, 10, 0, TimeSpan.Zero),
                PostedAt = new DateTimeOffset(2026, 3, 14, 16, 12, 0, TimeSpan.Zero)
            }
        ];
    }

    private List<ReceivedInvoicePayment> BuildReceivedInvoicePayments(
        Guid tenantId,
        IReadOnlyList<ReceivedInvoice> receivedInvoices,
        IReadOnlyList<PaymentVoucher> paymentVouchers)
    {
        return
        [
            new ReceivedInvoicePayment
            {
                TenantId = tenantId,
                ReceivedInvoice = receivedInvoices[0],
                PaymentVoucher = paymentVouchers[0],
                PaymentDate = paymentVouchers[0].Date,
                Amount = 14200m,
                Method = PaymentMethod.Transfer,
                Reference = paymentVouchers[0].VoucherNumber,
                Notes = "Settled in full."
            },
            new ReceivedInvoicePayment
            {
                TenantId = tenantId,
                ReceivedInvoice = receivedInvoices[1],
                PaymentVoucher = paymentVouchers[1],
                PaymentDate = paymentVouchers[1].Date,
                Amount = 1400m,
                Method = PaymentMethod.Transfer,
                Reference = paymentVouchers[1].VoucherNumber,
                Notes = "Partial diesel payment."
            },
            new ReceivedInvoicePayment
            {
                TenantId = tenantId,
                ReceivedInvoice = receivedInvoices[3],
                PaymentVoucher = paymentVouchers[2],
                PaymentDate = paymentVouchers[2].Date,
                Amount = 2527m,
                Method = PaymentMethod.Transfer,
                Reference = paymentVouchers[2].VoucherNumber,
                Notes = "Repair invoice settled."
            }
        ];
    }

    private void ApplyReceivedInvoiceSettlements(IReadOnlyList<ReceivedInvoice> invoices, IReadOnlyList<ReceivedInvoicePayment> payments)
    {
        foreach (var invoice in invoices)
        {
            var totalPaid = payments.Where(x => x.ReceivedInvoice == invoice).Sum(x => x.Amount);
            invoice.BalanceDue = Math.Max(0m, invoice.TotalAmount - totalPaid);
            invoice.PaymentStatus = totalPaid switch
            {
                <= 0m => ReceivedInvoiceStatus.Unpaid,
                var amount when amount >= invoice.TotalAmount => ReceivedInvoiceStatus.Paid,
                _ => ReceivedInvoiceStatus.Partial
            };
        }
    }

    private List<PayrollPeriod> BuildPayrollPeriods(Guid tenantId)
    {
        return
        [
            new PayrollPeriod
            {
                TenantId = tenantId,
                Year = 2026,
                Month = 1,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 1, 31),
                PeriodDays = 31,
                Status = PayrollPeriodStatus.Finalized
            },
            new PayrollPeriod
            {
                TenantId = tenantId,
                Year = 2026,
                Month = 2,
                StartDate = new DateOnly(2026, 2, 1),
                EndDate = new DateOnly(2026, 2, 28),
                PeriodDays = 28,
                Status = PayrollPeriodStatus.Finalized
            }
        ];
    }

    private List<PayrollEntry> BuildPayrollEntries(Guid tenantId, IReadOnlyList<PayrollPeriod> periods, IReadOnlyList<Staff> staff)
    {
        return
        [
            CreatePayrollEntry(tenantId, periods[0], staff[0], attendedDays: 30, overtimePay: 350m, salaryAdvance: 0m),
            CreatePayrollEntry(tenantId, periods[0], staff[1], attendedDays: 31, overtimePay: 120m, salaryAdvance: 200m),
            CreatePayrollEntry(tenantId, periods[0], staff[2], attendedDays: 29, overtimePay: 0m, salaryAdvance: 0m),
            CreatePayrollEntry(tenantId, periods[0], staff[3], attendedDays: 31, overtimePay: 260m, salaryAdvance: 0m),
            CreatePayrollEntry(tenantId, periods[1], staff[0], attendedDays: 27, overtimePay: 280m, salaryAdvance: 0m),
            CreatePayrollEntry(tenantId, periods[1], staff[1], attendedDays: 28, overtimePay: 140m, salaryAdvance: 0m),
            CreatePayrollEntry(tenantId, periods[1], staff[2], attendedDays: 26, overtimePay: 0m, salaryAdvance: 100m),
            CreatePayrollEntry(tenantId, periods[1], staff[3], attendedDays: 28, overtimePay: 300m, salaryAdvance: 0m)
        ];
    }

    private PayrollEntry CreatePayrollEntry(Guid tenantId, PayrollPeriod period, Staff staff, int attendedDays, decimal overtimePay, decimal salaryAdvance)
    {
        var grossBase = staff.Basic;
        var grossAllowances = staff.ServiceAllowance + staff.OtherAllowance + staff.PhoneAllowance;
        var subtotal = grossBase + grossAllowances;
        var absentDays = Math.Max(0, period.PeriodDays - attendedDays);
        var ratePerDay = Math.Round(staff.Basic / period.PeriodDays, 4);
        var absentDeduction = Math.Round(ratePerDay * absentDays, 2);
        var foodAllowance = attendedDays * staff.FoodRate;
        var totalPay = subtotal - absentDeduction + foodAllowance + overtimePay;
        var pensionDeduction = Math.Round(staff.Basic * 0.07m, 2);
        var netPayable = totalPay - pensionDeduction - salaryAdvance;

        return new PayrollEntry
        {
            TenantId = tenantId,
            PayrollPeriod = period,
            Staff = staff,
            Basic = staff.Basic,
            ServiceAllowance = staff.ServiceAllowance,
            OtherAllowance = staff.OtherAllowance,
            PhoneAllowance = staff.PhoneAllowance,
            GrossBase = grossBase,
            GrossAllowances = grossAllowances,
            SubTotal = subtotal,
            PeriodDays = period.PeriodDays,
            AttendedDays = attendedDays,
            AbsentDays = absentDays,
            RatePerDay = ratePerDay,
            AbsentDeduction = absentDeduction,
            FoodAllowanceDays = attendedDays,
            FoodAllowanceRate = staff.FoodRate,
            FoodAllowance = foodAllowance,
            OvertimePay = overtimePay,
            PensionDeduction = pensionDeduction,
            SalaryAdvanceDeduction = salaryAdvance,
            TotalPay = totalPay,
            NetPayable = netPayable
        };
    }

    private void ApplyPayrollTotals(IReadOnlyList<PayrollPeriod> periods, IReadOnlyList<PayrollEntry> entries)
    {
        foreach (var period in periods)
        {
            period.TotalNetPayable = entries
                .Where(x => x.PayrollPeriod == period)
                .Sum(x => x.NetPayable);
        }
    }

    private List<SalarySlip> BuildSalarySlips(Guid tenantId, IReadOnlyList<PayrollEntry> payrollEntries)
    {
        return payrollEntries
            .Select((entry, index) => new SalarySlip
            {
                TenantId = tenantId,
                SlipNo = $"SAL-2026-{(index + 1).ToString("D3")}",
                PayrollEntry = entry,
                GeneratedAt = DateTimeOffset.UtcNow.AddDays(-(payrollEntries.Count - index))
            })
            .ToList();
    }

    private List<StaffConductForm> BuildStaffConductForms(Guid tenantId, IReadOnlyList<Staff> staff)
    {
        return
        [
            new StaffConductForm
            {
                TenantId = tenantId,
                Staff = staff[1],
                FormNumber = "WF-2026-001",
                FormType = StaffConductFormType.Warning,
                IssueDate = new DateOnly(2026, 2, 10),
                IncidentDate = new DateOnly(2026, 2, 9),
                Subject = "Late payroll file submission",
                IncidentDetails = "Supporting payroll file reached finance review after the internal cutoff window.",
                ActionTaken = "Formal warning issued and workflow handover review completed.",
                RequiredImprovement = "Submit payroll support pack one day before review cutoff.",
                Severity = StaffConductSeverity.Low,
                Status = StaffConductStatus.Acknowledged,
                IssuedBy = "myDhathuru Demo Admin",
                WitnessedBy = "Aishath Naza",
                FollowUpDate = new DateOnly(2026, 2, 24),
                IsAcknowledgedByStaff = true,
                AcknowledgedDate = new DateOnly(2026, 2, 10),
                EmployeeRemarks = "Acknowledged and process updated.",
                StaffCodeSnapshot = staff[1].StaffId,
                StaffNameSnapshot = staff[1].StaffName,
                DesignationSnapshot = staff[1].Designation,
                WorkSiteSnapshot = staff[1].WorkSite,
                IdNumberSnapshot = staff[1].IdNumber
            },
            new StaffConductForm
            {
                TenantId = tenantId,
                Staff = staff[3],
                FormNumber = "DF-2026-001",
                FormType = StaffConductFormType.Disciplinary,
                IssueDate = new DateOnly(2026, 3, 6),
                IncidentDate = new DateOnly(2026, 3, 5),
                Subject = "Missed vessel dispatch checklist",
                IncidentDetails = "Dispatch checklist was left unsigned before departure, requiring a same-day compliance follow-up.",
                ActionTaken = "Disciplinary record opened and checklist approval process retraining completed.",
                RequiredImprovement = "Close all dispatch checklists before departure and submit a verified copy to operations.",
                Severity = StaffConductSeverity.Medium,
                Status = StaffConductStatus.Open,
                IssuedBy = "Aishath Naza",
                WitnessedBy = "Mariyam Shina",
                FollowUpDate = new DateOnly(2026, 3, 20),
                IsAcknowledgedByStaff = false,
                StaffCodeSnapshot = staff[3].StaffId,
                StaffNameSnapshot = staff[3].StaffName,
                DesignationSnapshot = staff[3].Designation,
                WorkSiteSnapshot = staff[3].WorkSite,
                IdNumberSnapshot = staff[3].IdNumber
            }
        ];
    }

    private List<OtherIncomeEntry> BuildOtherIncomeEntries(Guid tenantId, IReadOnlyList<Customer> customers)
    {
        return
        [
            new OtherIncomeEntry
            {
                TenantId = tenantId,
                EntryNumber = "OTH-2026-001",
                TransactionDate = new DateOnly(2026, 2, 14),
                Customer = customers[0],
                CounterpartyName = customers[0].Name,
                Description = "Storage recovery fee",
                Currency = "MVR",
                ExchangeRate = 1m,
                AmountOriginal = 255m,
                AmountMvr = 255m,
                ApprovalStatus = ApprovalStatus.Approved,
                Notes = "Ancillary storage charge recovered."
            },
            new OtherIncomeEntry
            {
                TenantId = tenantId,
                EntryNumber = "OTH-2026-002",
                TransactionDate = new DateOnly(2026, 3, 11),
                Customer = customers[2],
                CounterpartyName = customers[2].Name,
                Description = "Export handling surcharge",
                Currency = "USD",
                ExchangeRate = 15.42m,
                AmountOriginal = 45m,
                AmountMvr = 693.90m,
                ApprovalStatus = ApprovalStatus.Approved,
                Notes = "USD ancillary income."
            }
        ];
    }

    private List<SalesAdjustment> BuildSalesAdjustments(Guid tenantId, IReadOnlyList<Customer> customers, IReadOnlyList<Invoice> invoices)
    {
        return
        [
            new SalesAdjustment
            {
                TenantId = tenantId,
                AdjustmentNumber = "SADJ-2026-001",
                AdjustmentType = SalesAdjustmentType.Return,
                TransactionDate = new DateOnly(2026, 3, 2),
                RelatedInvoice = invoices[1],
                RelatedInvoiceNumber = invoices[1].InvoiceNo,
                Customer = customers[1],
                CustomerName = customers[1].Name,
                Currency = "MVR",
                ExchangeRate = 1m,
                AmountOriginal = 77m,
                AmountMvr = 77m,
                ApprovalStatus = ApprovalStatus.Approved,
                Notes = "Returned short shipment line."
            },
            new SalesAdjustment
            {
                TenantId = tenantId,
                AdjustmentNumber = "SADJ-2026-002",
                AdjustmentType = SalesAdjustmentType.Allowance,
                TransactionDate = new DateOnly(2026, 3, 13),
                RelatedInvoice = invoices[2],
                RelatedInvoiceNumber = invoices[2].InvoiceNo,
                Customer = customers[2],
                CustomerName = customers[2].Name,
                Currency = "USD",
                ExchangeRate = 15.42m,
                AmountOriginal = 12m,
                AmountMvr = 185.04m,
                ApprovalStatus = ApprovalStatus.Approved,
                Notes = "Allowance on export dispatch timing."
            }
        ];
    }

    private List<DocumentSequence> BuildDocumentSequences(Guid tenantId, int year)
    {
        return
        [
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.Quotation, Year = year, NextNumber = 4 },
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.DeliveryNote, Year = year, NextNumber = 4 },
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.Invoice, Year = year, NextNumber = 5 },
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.PurchaseOrder, Year = year, NextNumber = 4 },
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.ReceivedInvoice, Year = year, NextNumber = 5 },
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.PaymentVoucher, Year = year, NextNumber = 4 },
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.RentEntry, Year = year, NextNumber = 3 },
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.WarningForm, Year = year, NextNumber = 2 },
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.DisciplinaryForm, Year = year, NextNumber = 2 },
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.SalarySlip, Year = year, NextNumber = 9 },
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.SalesAdjustment, Year = year, NextNumber = 3 },
            new DocumentSequence { TenantId = tenantId, DocumentType = DocumentType.OtherIncome, Year = year, NextNumber = 3 }
        ];
    }
}
