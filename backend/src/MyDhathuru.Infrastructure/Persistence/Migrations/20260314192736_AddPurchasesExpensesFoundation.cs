using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasesExpensesFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInputTaxClaimEnabled",
                table: "TenantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentVoucherPrefix",
                table: "TenantSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceivedInvoicePrefix",
                table: "TenantSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RentEntryPrefix",
                table: "TenantSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxableActivityNumber",
                table: "TenantSettings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WarningFormPrefix",
                table: "TenantSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BusinessAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TargetName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    DetailsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PerformedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    BptCategoryCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TinNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Notes = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RentEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RentNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PropertyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExpenseCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentEntries_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpenseCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ClaimableTaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PendingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseEntries_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseEntries_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReceivedInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SupplierTin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupplierContactNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SupplierEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Outlet = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GstRate = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    GstAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BalanceDue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReceiptReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SettlementReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BankAccountDetails = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    MiraTaxableActivityNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RevenueCapitalClassification = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpenseCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsTaxClaimable = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivedInvoices_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceivedInvoices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PayTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ChequeNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Bank = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountInWords = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ReceivedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LinkedReceivedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedExpenseEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentVouchers_ExpenseEntries_LinkedExpenseEntryId",
                        column: x => x.LinkedExpenseEntryId,
                        principalTable: "ExpenseEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentVouchers_ReceivedInvoices_LinkedReceivedInvoiceId",
                        column: x => x.LinkedReceivedInvoiceId,
                        principalTable: "ReceivedInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReceivedInvoiceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedInvoiceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivedInvoiceAttachments_ReceivedInvoices_ReceivedInvoice~",
                        column: x => x.ReceivedInvoiceId,
                        principalTable: "ReceivedInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivedInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Uom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GstRate = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    GstAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivedInvoiceItems_ReceivedInvoices_ReceivedInvoiceId",
                        column: x => x.ReceivedInvoiceId,
                        principalTable: "ReceivedInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivedInvoicePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentVoucherId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedInvoicePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivedInvoicePayments_PaymentVouchers_PaymentVoucherId",
                        column: x => x.PaymentVoucherId,
                        principalTable: "PaymentVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReceivedInvoicePayments_ReceivedInvoices_ReceivedInvoiceId",
                        column: x => x.ReceivedInvoiceId,
                        principalTable: "ReceivedInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_TenantId_ActionType",
                table: "BusinessAuditLogs",
                columns: new[] { "TenantId", "ActionType" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_TenantId_PerformedAt",
                table: "BusinessAuditLogs",
                columns: new[] { "TenantId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_TenantId_TargetType",
                table: "BusinessAuditLogs",
                columns: new[] { "TenantId", "TargetType" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_TenantId_Code",
                table: "ExpenseCategories",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_TenantId_Name",
                table: "ExpenseCategories",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_ExpenseCategoryId",
                table: "ExpenseEntries",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_SupplierId",
                table: "ExpenseEntries",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_TenantId_ExpenseCategoryId",
                table: "ExpenseEntries",
                columns: new[] { "TenantId", "ExpenseCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_TenantId_SourceType_SourceId",
                table: "ExpenseEntries",
                columns: new[] { "TenantId", "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_TenantId_TransactionDate",
                table: "ExpenseEntries",
                columns: new[] { "TenantId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_LinkedExpenseEntryId",
                table: "PaymentVouchers",
                column: "LinkedExpenseEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_LinkedReceivedInvoiceId",
                table: "PaymentVouchers",
                column: "LinkedReceivedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_TenantId_Date",
                table: "PaymentVouchers",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_TenantId_Status",
                table: "PaymentVouchers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_TenantId_VoucherNumber",
                table: "PaymentVouchers",
                columns: new[] { "TenantId", "VoucherNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoiceAttachments_ReceivedInvoiceId",
                table: "ReceivedInvoiceAttachments",
                column: "ReceivedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoiceAttachments_TenantId_ReceivedInvoiceId_Creat~",
                table: "ReceivedInvoiceAttachments",
                columns: new[] { "TenantId", "ReceivedInvoiceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoiceItems_ReceivedInvoiceId_CreatedAt",
                table: "ReceivedInvoiceItems",
                columns: new[] { "ReceivedInvoiceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoicePayments_PaymentVoucherId",
                table: "ReceivedInvoicePayments",
                column: "PaymentVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoicePayments_ReceivedInvoiceId",
                table: "ReceivedInvoicePayments",
                column: "ReceivedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoicePayments_TenantId_ReceivedInvoiceId_PaymentD~",
                table: "ReceivedInvoicePayments",
                columns: new[] { "TenantId", "ReceivedInvoiceId", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoices_ExpenseCategoryId",
                table: "ReceivedInvoices",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoices_SupplierId",
                table: "ReceivedInvoices",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoices_TenantId_DueDate",
                table: "ReceivedInvoices",
                columns: new[] { "TenantId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoices_TenantId_ExpenseCategoryId",
                table: "ReceivedInvoices",
                columns: new[] { "TenantId", "ExpenseCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoices_TenantId_InvoiceDate",
                table: "ReceivedInvoices",
                columns: new[] { "TenantId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoices_TenantId_InvoiceNumber",
                table: "ReceivedInvoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoices_TenantId_PaymentStatus",
                table: "ReceivedInvoices",
                columns: new[] { "TenantId", "PaymentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedInvoices_TenantId_SupplierId",
                table: "ReceivedInvoices",
                columns: new[] { "TenantId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_RentEntries_ExpenseCategoryId",
                table: "RentEntries",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RentEntries_TenantId_Date",
                table: "RentEntries",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_RentEntries_TenantId_ExpenseCategoryId",
                table: "RentEntries",
                columns: new[] { "TenantId", "ExpenseCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_RentEntries_TenantId_RentNumber",
                table: "RentEntries",
                columns: new[] { "TenantId", "RentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_Name",
                table: "Suppliers",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessAuditLogs");

            migrationBuilder.DropTable(
                name: "ReceivedInvoiceAttachments");

            migrationBuilder.DropTable(
                name: "ReceivedInvoiceItems");

            migrationBuilder.DropTable(
                name: "ReceivedInvoicePayments");

            migrationBuilder.DropTable(
                name: "RentEntries");

            migrationBuilder.DropTable(
                name: "PaymentVouchers");

            migrationBuilder.DropTable(
                name: "ExpenseEntries");

            migrationBuilder.DropTable(
                name: "ReceivedInvoices");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsInputTaxClaimEnabled",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "PaymentVoucherPrefix",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "ReceivedInvoicePrefix",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "RentEntryPrefix",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "TaxableActivityNumber",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "WarningFormPrefix",
                table: "TenantSettings");
        }
    }
}
