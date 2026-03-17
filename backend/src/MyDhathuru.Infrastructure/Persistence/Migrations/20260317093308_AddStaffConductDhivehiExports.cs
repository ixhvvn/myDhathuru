using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffConductDhivehiExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementDv",
                table: "StaffConductForms",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionTakenDv",
                table: "StaffConductForms",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeRemarksDv",
                table: "StaffConductForms",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncidentDetailsDv",
                table: "StaffConductForms",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredImprovementDv",
                table: "StaffConductForms",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNotesDv",
                table: "StaffConductForms",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectDv",
                table: "StaffConductForms",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StaffConductExportDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffConductFormId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffConductExportDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffConductExportDocuments_StaffConductForms_StaffConductF~",
                        column: x => x.StaffConductFormId,
                        principalTable: "StaffConductForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffConductExportDocuments_StaffConductFormId",
                table: "StaffConductExportDocuments",
                column: "StaffConductFormId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffConductExportDocuments_TenantId_StaffConductFormId_Lan~",
                table: "StaffConductExportDocuments",
                columns: new[] { "TenantId", "StaffConductFormId", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffConductExportDocuments");

            migrationBuilder.DropColumn(
                name: "AcknowledgementDv",
                table: "StaffConductForms");

            migrationBuilder.DropColumn(
                name: "ActionTakenDv",
                table: "StaffConductForms");

            migrationBuilder.DropColumn(
                name: "EmployeeRemarksDv",
                table: "StaffConductForms");

            migrationBuilder.DropColumn(
                name: "IncidentDetailsDv",
                table: "StaffConductForms");

            migrationBuilder.DropColumn(
                name: "RequiredImprovementDv",
                table: "StaffConductForms");

            migrationBuilder.DropColumn(
                name: "ResolutionNotesDv",
                table: "StaffConductForms");

            migrationBuilder.DropColumn(
                name: "SubjectDv",
                table: "StaffConductForms");
        }
    }
}
