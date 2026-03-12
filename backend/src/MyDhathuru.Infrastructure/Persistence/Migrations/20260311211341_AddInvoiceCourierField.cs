using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceCourierField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CourierVesselId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CourierVesselId",
                table: "Invoices",
                column: "CourierVesselId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Vessels_CourierVesselId",
                table: "Invoices",
                column: "CourierVesselId",
                principalTable: "Vessels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Vessels_CourierVesselId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CourierVesselId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CourierVesselId",
                table: "Invoices");
        }
    }
}
