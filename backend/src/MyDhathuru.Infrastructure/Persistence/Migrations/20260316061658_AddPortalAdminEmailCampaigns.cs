using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalAdminEmailCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminEmailCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SentByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AudienceMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    CcAdminUsers = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeDisabledBusinesses = table.Column<bool>(type: "boolean", nullable: false),
                    RequestedCompanyCount = table.Column<int>(type: "integer", nullable: false),
                    SentCompanyCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCompanyCount = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminEmailCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminEmailCampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminEmailCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ToEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CcEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminEmailCampaignRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminEmailCampaignRecipients_AdminEmailCampaigns_AdminEmail~",
                        column: x => x.AdminEmailCampaignId,
                        principalTable: "AdminEmailCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminEmailCampaignRecipients_AdminEmailCampaignId_Attempted~",
                table: "AdminEmailCampaignRecipients",
                columns: new[] { "AdminEmailCampaignId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminEmailCampaignRecipients_TenantId_AttemptedAt",
                table: "AdminEmailCampaignRecipients",
                columns: new[] { "TenantId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminEmailCampaigns_SentAt",
                table: "AdminEmailCampaigns",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminEmailCampaigns_SentByUserId",
                table: "AdminEmailCampaigns",
                column: "SentByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminEmailCampaignRecipients");

            migrationBuilder.DropTable(
                name: "AdminEmailCampaigns");
        }
    }
}
