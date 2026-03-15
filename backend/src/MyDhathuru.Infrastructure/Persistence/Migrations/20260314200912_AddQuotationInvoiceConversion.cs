using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationInvoiceConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "QuotationId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_QuotationId",
                table: "Invoices",
                column: "QuotationId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Quotations_QuotationId",
                table: "Invoices",
                column: "QuotationId",
                principalTable: "Quotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Quotations_QuotationId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_QuotationId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "QuotationId",
                table: "Invoices");
        }
    }
}
