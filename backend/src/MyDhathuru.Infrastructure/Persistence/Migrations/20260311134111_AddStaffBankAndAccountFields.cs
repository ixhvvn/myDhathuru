using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffBankAndAccountFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                table: "Staff",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Staff",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountName",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Staff");
        }
    }
}
