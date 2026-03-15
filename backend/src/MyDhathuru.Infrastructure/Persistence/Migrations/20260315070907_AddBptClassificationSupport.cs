using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBptClassificationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BptCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ClassificationGroup = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_BptCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RateToMvr = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtherIncomeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CounterpartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    AmountOriginal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountMvr = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_OtherIncomeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtherIncomeEntries_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SalesAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdjustmentNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AdjustmentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RelatedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedInvoiceNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    AmountOriginal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountMvr = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_SalesAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesAdjustments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesAdjustments_Invoices_RelatedInvoiceId",
                        column: x => x.RelatedInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BptAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdjustmentNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    AmountOriginal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountMvr = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BptCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_BptAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BptAdjustments_BptCategories_BptCategoryId",
                        column: x => x.BptCategoryId,
                        principalTable: "BptCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BptMappingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ExpenseCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesAdjustmentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RevenueCapitalClassification = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BptCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BptMappingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BptMappingRules_BptCategories_BptCategoryId",
                        column: x => x.BptCategoryId,
                        principalTable: "BptCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BptMappingRules_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BptAdjustments_BptCategoryId",
                table: "BptAdjustments",
                column: "BptCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BptAdjustments_TenantId_AdjustmentNumber",
                table: "BptAdjustments",
                columns: new[] { "TenantId", "AdjustmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BptAdjustments_TenantId_BptCategoryId_ApprovalStatus",
                table: "BptAdjustments",
                columns: new[] { "TenantId", "BptCategoryId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_BptAdjustments_TenantId_TransactionDate",
                table: "BptAdjustments",
                columns: new[] { "TenantId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BptCategories_TenantId_Code",
                table: "BptCategories",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BptCategories_TenantId_Name",
                table: "BptCategories",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BptMappingRules_BptCategoryId",
                table: "BptMappingRules",
                column: "BptCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BptMappingRules_ExpenseCategoryId",
                table: "BptMappingRules",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BptMappingRules_TenantId_BptCategoryId_IsActive",
                table: "BptMappingRules",
                columns: new[] { "TenantId", "BptCategoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BptMappingRules_TenantId_ExpenseCategoryId_SourceModule_IsA~",
                table: "BptMappingRules",
                columns: new[] { "TenantId", "ExpenseCategoryId", "SourceModule", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_TenantId_Currency_RateDate",
                table: "ExchangeRates",
                columns: new[] { "TenantId", "Currency", "RateDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_TenantId_RateDate",
                table: "ExchangeRates",
                columns: new[] { "TenantId", "RateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomeEntries_CustomerId",
                table: "OtherIncomeEntries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomeEntries_TenantId_ApprovalStatus",
                table: "OtherIncomeEntries",
                columns: new[] { "TenantId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomeEntries_TenantId_EntryNumber",
                table: "OtherIncomeEntries",
                columns: new[] { "TenantId", "EntryNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomeEntries_TenantId_TransactionDate",
                table: "OtherIncomeEntries",
                columns: new[] { "TenantId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAdjustments_CustomerId",
                table: "SalesAdjustments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAdjustments_RelatedInvoiceId",
                table: "SalesAdjustments",
                column: "RelatedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAdjustments_TenantId_AdjustmentNumber",
                table: "SalesAdjustments",
                columns: new[] { "TenantId", "AdjustmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesAdjustments_TenantId_AdjustmentType_ApprovalStatus",
                table: "SalesAdjustments",
                columns: new[] { "TenantId", "AdjustmentType", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAdjustments_TenantId_TransactionDate",
                table: "SalesAdjustments",
                columns: new[] { "TenantId", "TransactionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BptAdjustments");

            migrationBuilder.DropTable(
                name: "BptMappingRules");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "OtherIncomeEntries");

            migrationBuilder.DropTable(
                name: "SalesAdjustments");

            migrationBuilder.DropTable(
                name: "BptCategories");
        }
    }
}
