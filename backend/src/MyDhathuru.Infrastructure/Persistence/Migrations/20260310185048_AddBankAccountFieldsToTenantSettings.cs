using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAccountFieldsToTenantSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BmlMvrAccountName",
                table: "TenantSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BmlMvrAccountNumber",
                table: "TenantSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BmlUsdAccountName",
                table: "TenantSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BmlUsdAccountNumber",
                table: "TenantSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MibMvrAccountName",
                table: "TenantSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MibMvrAccountNumber",
                table: "TenantSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MibUsdAccountName",
                table: "TenantSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MibUsdAccountNumber",
                table: "TenantSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BmlMvrAccountName",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "BmlMvrAccountNumber",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "BmlUsdAccountName",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "BmlUsdAccountNumber",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "MibMvrAccountName",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "MibMvrAccountNumber",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "MibUsdAccountName",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "MibUsdAccountNumber",
                table: "TenantSettings");
        }
    }
}
