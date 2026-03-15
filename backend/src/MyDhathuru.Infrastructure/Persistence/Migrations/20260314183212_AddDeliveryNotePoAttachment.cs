using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryNotePoAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PoAttachmentContent",
                table: "DeliveryNotes",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoAttachmentContentType",
                table: "DeliveryNotes",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoAttachmentFileName",
                table: "DeliveryNotes",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PoAttachmentSizeBytes",
                table: "DeliveryNotes",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PoAttachmentContent",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "PoAttachmentContentType",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "PoAttachmentFileName",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "PoAttachmentSizeBytes",
                table: "DeliveryNotes");
        }
    }
}
