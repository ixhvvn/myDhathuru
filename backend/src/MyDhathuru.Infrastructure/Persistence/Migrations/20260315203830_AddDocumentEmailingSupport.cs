using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentEmailingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceEmailBodyTemplate",
                table: "TenantSettings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PurchaseOrderEmailBodyTemplate",
                table: "TenantSettings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "QuotationEmailBodyTemplate",
                table: "TenantSettings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmailStatus",
                table: "Quotations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastEmailedAt",
                table: "Quotations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEmailedCc",
                table: "Quotations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEmailedTo",
                table: "Quotations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailStatus",
                table: "PurchaseOrders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastEmailedAt",
                table: "PurchaseOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEmailedCc",
                table: "PurchaseOrders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEmailedTo",
                table: "PurchaseOrders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailStatus",
                table: "Invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastEmailedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEmailedCc",
                table: "Invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEmailedTo",
                table: "Invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_TenantId_EmailStatus",
                table: "Quotations",
                columns: new[] { "TenantId", "EmailStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_EmailStatus",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "EmailStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_EmailStatus",
                table: "Invoices",
                columns: new[] { "TenantId", "EmailStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quotations_TenantId_EmailStatus",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_TenantId_EmailStatus",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_EmailStatus",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceEmailBodyTemplate",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderEmailBodyTemplate",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "QuotationEmailBodyTemplate",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "EmailStatus",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "LastEmailedAt",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "LastEmailedCc",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "LastEmailedTo",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "EmailStatus",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "LastEmailedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "LastEmailedCc",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "LastEmailedTo",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "EmailStatus",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LastEmailedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LastEmailedCc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LastEmailedTo",
                table: "Invoices");
        }
    }
}
