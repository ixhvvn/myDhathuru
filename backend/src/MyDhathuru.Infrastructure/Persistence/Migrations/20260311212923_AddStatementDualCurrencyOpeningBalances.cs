using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementDualCurrencyOpeningBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "CustomerOpeningBalances",
                newName: "OpeningBalanceMvr");

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBalanceUsd",
                table: "CustomerOpeningBalances",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpeningBalanceUsd",
                table: "CustomerOpeningBalances");

            migrationBuilder.RenameColumn(
                name: "OpeningBalanceMvr",
                table: "CustomerOpeningBalances",
                newName: "Amount");
        }
    }
}
