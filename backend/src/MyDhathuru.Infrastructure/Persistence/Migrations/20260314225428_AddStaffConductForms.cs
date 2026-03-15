using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffConductForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffConductForms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FormType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IncidentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IncidentDetails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActionTaken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequiredImprovement = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssuedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    WitnessedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsAcknowledgedByStaff = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployeeRemarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StaffCodeSnapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StaffNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DesignationSnapshot = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    WorkSiteSnapshot = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IdNumberSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffConductForms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffConductForms_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffConductForms_StaffId",
                table: "StaffConductForms",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffConductForms_TenantId_FormNumber",
                table: "StaffConductForms",
                columns: new[] { "TenantId", "FormNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffConductForms_TenantId_FormType_IssueDate",
                table: "StaffConductForms",
                columns: new[] { "TenantId", "FormType", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffConductForms_TenantId_StaffId_IssueDate",
                table: "StaffConductForms",
                columns: new[] { "TenantId", "StaffId", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffConductForms_TenantId_Status_IssueDate",
                table: "StaffConductForms",
                columns: new[] { "TenantId", "Status", "IssueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffConductForms");
        }
    }
}
