using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDhathuru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSettingsUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "TenantSettings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Username",
                table: "TenantSettings");
        }
    }
}
