using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePurchaseOrderCustomerToSupplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Customers_CustomerId",
                table: "PurchaseOrders");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "PurchaseOrders",
                newName: "SupplierId");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseOrders_CustomerId",
                table: "PurchaseOrders",
                newName: "IX_PurchaseOrders_SupplierId");

            migrationBuilder.Sql("""
                -- Reuse an existing supplier with the same tenant/name when the old PO supplier
                -- was incorrectly stored against Customers.
                UPDATE "Suppliers" AS s
                SET "IsDeleted" = FALSE,
                    "IsActive" = TRUE,
                    "UpdatedAt" = COALESCE(s."UpdatedAt", NOW())
                FROM "Customers" AS c
                JOIN "PurchaseOrders" AS po
                    ON po."TenantId" = c."TenantId"
                   AND po."SupplierId" = c."Id"
                WHERE s."TenantId" = c."TenantId"
                  AND s."Name" = c."Name";
                """);

            migrationBuilder.Sql("""
                UPDATE "PurchaseOrders" AS po
                SET "SupplierId" = s."Id"
                FROM "Customers" AS c
                JOIN "Suppliers" AS s
                    ON s."TenantId" = c."TenantId"
                   AND s."Name" = c."Name"
                WHERE po."TenantId" = c."TenantId"
                  AND po."SupplierId" = c."Id"
                  AND s."Id" <> c."Id";
                """);

            migrationBuilder.Sql("""
                -- For any remaining PO rows, create a supplier record from the original customer row
                -- so the renamed foreign key still points to a real tenant-scoped supplier.
                INSERT INTO "Suppliers" (
                    "Id",
                    "Name",
                    "TinNumber",
                    "ContactNumber",
                    "Email",
                    "Address",
                    "Notes",
                    "IsActive",
                    "CreatedAt",
                    "UpdatedAt",
                    "CreatedByUserId",
                    "UpdatedByUserId",
                    "IsDeleted",
                    "TenantId"
                )
                SELECT DISTINCT
                    c."Id",
                    c."Name",
                    c."TinNumber",
                    c."Phone",
                    c."Email",
                    NULL,
                    'Migrated from Customers during purchase order supplier rename.',
                    TRUE,
                    c."CreatedAt",
                    c."UpdatedAt",
                    c."CreatedByUserId",
                    c."UpdatedByUserId",
                    FALSE,
                    c."TenantId"
                FROM "Customers" AS c
                JOIN "PurchaseOrders" AS po
                    ON po."TenantId" = c."TenantId"
                   AND po."SupplierId" = c."Id"
                LEFT JOIN "Suppliers" AS s
                    ON s."Id" = c."Id"
                WHERE s."Id" IS NULL;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                table: "PurchaseOrders",
                newName: "CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders",
                newName: "IX_PurchaseOrders_CustomerId");

            migrationBuilder.Sql("""
                UPDATE "PurchaseOrders" AS po
                SET "CustomerId" = c."Id"
                FROM "Suppliers" AS s
                JOIN "Customers" AS c
                    ON c."TenantId" = s."TenantId"
                   AND c."Name" = s."Name"
                WHERE po."TenantId" = s."TenantId"
                  AND po."CustomerId" = s."Id"
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "Customers" AS existingCustomer
                      WHERE existingCustomer."Id" = po."CustomerId"
                  );
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Customers_CustomerId",
                table: "PurchaseOrders",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
