using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryNoteVesselPaymentAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "VesselPaymentInvoiceAttachmentContent",
                table: "DeliveryNotes",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VesselPaymentInvoiceAttachmentContentType",
                table: "DeliveryNotes",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VesselPaymentInvoiceAttachmentFileName",
                table: "DeliveryNotes",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VesselPaymentInvoiceAttachmentSizeBytes",
                table: "DeliveryNotes",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VesselPaymentInvoiceAttachmentContent",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "VesselPaymentInvoiceAttachmentContentType",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "VesselPaymentInvoiceAttachmentFileName",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "VesselPaymentInvoiceAttachmentSizeBytes",
                table: "DeliveryNotes");
        }
    }
}
