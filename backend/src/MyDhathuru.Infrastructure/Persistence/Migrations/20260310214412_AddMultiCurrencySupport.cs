using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiCurrencySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Invoices",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "InvoicePayments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "MVR");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "DeliveryNotes",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "MVR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "InvoicePayments");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "DeliveryNotes");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Invoices",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);
        }
    }
}
