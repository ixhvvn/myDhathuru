using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalAdminBillingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminBillingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BasicSoftwareFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VesselFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StaffFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    InvoicePrefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartingSequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    DefaultCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DefaultDueDays = table.Column<int>(type: "integer", nullable: false),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Branch = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PaymentInstructions = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    InvoiceFooterNote = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    InvoiceTerms = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    EmailFromName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReplyToEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AutoGenerationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoEmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminBillingSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessCustomRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwareFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VesselFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StaffFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessCustomRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessCustomRates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CompanyNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyEmailSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyPhoneSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CompanyTinSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompanyRegistrationSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompanyAdminNameSnapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CompanyAdminEmailSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BaseSoftwareFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VesselCount = table.Column<int>(type: "integer", nullable: false),
                    VesselRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VesselAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StaffCount = table.Column<int>(type: "integer", nullable: false),
                    StaffRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StaffAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsCustom = table.Column<bool>(type: "boolean", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CustomRateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminInvoices_BusinessCustomRates_CustomRateId",
                        column: x => x.CustomRateId,
                        principalTable: "BusinessCustomRates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AdminInvoices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminInvoiceEmailLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CcEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    AttemptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminInvoiceEmailLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminInvoiceEmailLogs_AdminInvoices_AdminInvoiceId",
                        column: x => x.AdminInvoiceId,
                        principalTable: "AdminInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminInvoiceLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminInvoiceLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminInvoiceLineItems_AdminInvoices_AdminInvoiceId",
                        column: x => x.AdminInvoiceId,
                        principalTable: "AdminInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminBillingSettings_CreatedAt",
                table: "AdminBillingSettings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminInvoiceEmailLogs_AdminInvoiceId_AttemptedAt",
                table: "AdminInvoiceEmailLogs",
                columns: new[] { "AdminInvoiceId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminInvoiceLineItems_AdminInvoiceId_SortOrder",
                table: "AdminInvoiceLineItems",
                columns: new[] { "AdminInvoiceId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminInvoices_CreatedAt",
                table: "AdminInvoices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminInvoices_CustomRateId",
                table: "AdminInvoices",
                column: "CustomRateId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminInvoices_InvoiceNumber",
                table: "AdminInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminInvoices_Status",
                table: "AdminInvoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AdminInvoices_TenantId_BillingMonth",
                table: "AdminInvoices",
                columns: new[] { "TenantId", "BillingMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCustomRates_TenantId",
                table: "BusinessCustomRates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCustomRates_TenantId_IsActive_EffectiveFrom_Effecti~",
                table: "BusinessCustomRates",
                columns: new[] { "TenantId", "IsActive", "EffectiveFrom", "EffectiveTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminBillingSettings");

            migrationBuilder.DropTable(
                name: "AdminInvoiceEmailLogs");

            migrationBuilder.DropTable(
                name: "AdminInvoiceLineItems");

            migrationBuilder.DropTable(
                name: "AdminInvoices");

            migrationBuilder.DropTable(
                name: "BusinessCustomRates");
        }
    }
}
