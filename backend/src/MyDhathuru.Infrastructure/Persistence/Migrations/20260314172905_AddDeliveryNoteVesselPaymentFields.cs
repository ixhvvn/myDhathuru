using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryNoteVesselPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VesselPaymentFee",
                table: "DeliveryNotes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VesselPaymentInvoiceNumber",
                table: "DeliveryNotes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VesselPaymentFee",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "VesselPaymentInvoiceNumber",
                table: "DeliveryNotes");
        }
    }
}
