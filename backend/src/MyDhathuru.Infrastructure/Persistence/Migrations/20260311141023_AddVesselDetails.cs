using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVesselDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Vessels",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomePort",
                table: "Vessels",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "IssuedDate",
                table: "Vessels",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Vessels",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "Vessels",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassengerCapacity",
                table: "Vessels",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "Vessels",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VesselType",
                table: "Vessels",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vessels_TenantId_RegistrationNumber",
                table: "Vessels",
                columns: new[] { "TenantId", "RegistrationNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vessels_TenantId_RegistrationNumber",
                table: "Vessels");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Vessels");

            migrationBuilder.DropColumn(
                name: "HomePort",
                table: "Vessels");

            migrationBuilder.DropColumn(
                name: "IssuedDate",
                table: "Vessels");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Vessels");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "Vessels");

            migrationBuilder.DropColumn(
                name: "PassengerCapacity",
                table: "Vessels");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "Vessels");

            migrationBuilder.DropColumn(
                name: "VesselType",
                table: "Vessels");
        }
    }
}
