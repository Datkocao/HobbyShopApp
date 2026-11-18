using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HobbyShopApp.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressDefaultIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_addresses_user_id",
                table: "addresses");

            migrationBuilder.CreateIndex(
                name: "ux_addresses_one_default",
                table: "addresses",
                column: "user_id",
                unique: true,
                filter: "is_default = true AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_addresses_one_default",
                table: "addresses");

            migrationBuilder.CreateIndex(
                name: "ix_addresses_user_id",
                table: "addresses",
                column: "user_id");
        }
    }
}
