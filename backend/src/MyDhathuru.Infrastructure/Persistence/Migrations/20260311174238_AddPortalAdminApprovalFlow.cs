using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalAdminApprovalFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountStatus",
                table: "Tenants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisabledAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DisabledByUserId",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabledReason",
                table: "Tenants",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TargetName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    RelatedTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PerformedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignupRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TinNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BusinessRegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RequestedByEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    PasswordSalt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignupRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_ActionType",
                table: "AdminAuditLogs",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_PerformedAt",
                table: "AdminAuditLogs",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_RelatedTenantId",
                table: "AdminAuditLogs",
                column: "RelatedTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SignupRequests_CompanyEmail",
                table: "SignupRequests",
                column: "CompanyEmail");

            migrationBuilder.CreateIndex(
                name: "IX_SignupRequests_RequestedByEmail",
                table: "SignupRequests",
                column: "RequestedByEmail");

            migrationBuilder.CreateIndex(
                name: "IX_SignupRequests_Status",
                table: "SignupRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SignupRequests_SubmittedAt",
                table: "SignupRequests",
                column: "SubmittedAt");

            migrationBuilder.Sql("""
                UPDATE "Tenants"
                SET "AccountStatus" = 'Active'
                WHERE "AccountStatus" = '' OR "AccountStatus" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropTable(
                name: "SignupRequests");

            migrationBuilder.DropColumn(
                name: "AccountStatus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DisabledAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DisabledByUserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DisabledReason",
                table: "Tenants");
        }
    }
}
