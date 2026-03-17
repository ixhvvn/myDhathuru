using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationDeliveryNoteConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "QuotationId",
                table: "DeliveryNotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_QuotationId",
                table: "DeliveryNotes",
                column: "QuotationId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryNotes_Quotations_QuotationId",
                table: "DeliveryNotes",
                column: "QuotationId",
                principalTable: "Quotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryNotes_Quotations_QuotationId",
                table: "DeliveryNotes");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryNotes_QuotationId",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "QuotationId",
                table: "DeliveryNotes");
        }
    }
}
