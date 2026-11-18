using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HobbyShopApp.Migrations
{
    /// <inheritdoc />
    public partial class FixCatalogMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_categories_categories_parent_id",
                table: "categories");

            migrationBuilder.DropForeignKey(
                name: "fk_products_categories_category_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "fk_products_users_created_by_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "fk_products_users_seller_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_category_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_created_by_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_seller_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_product_images_product_id",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "seller_id",
                table: "products");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "slug",
                table: "products",
                type: "character varying(280)",
                maxLength: 280,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "products",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "storage_key",
                table: "product_images",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "content_type",
                table: "product_images",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "slug",
                table: "categories",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "categories",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "ix_products_category_id_status",
                table: "products",
                columns: new[] { "category_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_products_created_by_user_id",
                table: "products",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_price",
                table: "products",
                column: "price");

            migrationBuilder.CreateIndex(
                name: "ix_products_seller_user_id",
                table: "products",
                column: "seller_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_slug",
                table: "products",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_status_is_featured",
                table: "products",
                columns: new[] { "status", "is_featured" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_price_non_negative",
                table: "products",
                sql: "price >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_stock_non_negative",
                table: "products",
                sql: "stock_quantity >= 0");

            migrationBuilder.CreateIndex(
                name: "ux_product_images_one_primary",
                table: "product_images",
                column: "product_id",
                unique: true,
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "ix_categories_slug",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_categories_categories_parent_id",
                table: "categories",
                column: "parent_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_products_categories_category_id",
                table: "products",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_products_users_created_by_user_id",
                table: "products",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_products_users_seller_user_id",
                table: "products",
                column: "seller_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_categories_categories_parent_id",
                table: "categories");

            migrationBuilder.DropForeignKey(
                name: "fk_products_categories_category_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "fk_products_users_created_by_user_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "fk_products_users_seller_user_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_category_id_status",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_created_by_user_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_price",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_seller_user_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_slug",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_status_is_featured",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_price_non_negative",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_stock_non_negative",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ux_product_images_one_primary",
                table: "product_images");

            migrationBuilder.DropIndex(
                name: "ix_categories_slug",
                table: "categories");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                table: "products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "slug",
                table: "products",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(280)",
                oldMaxLength: 280);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "products",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250);

            migrationBuilder.AddColumn<long>(
                name: "created_by_id",
                table: "products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "seller_id",
                table: "products",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "storage_key",
                table: "product_images",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "content_type",
                table: "product_images",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "slug",
                table: "categories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(180)",
                oldMaxLength: 180);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "categories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.CreateIndex(
                name: "ix_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_created_by_id",
                table: "products",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_seller_id",
                table: "products",
                column: "seller_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_images_product_id",
                table: "product_images",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "fk_categories_categories_parent_id",
                table: "categories",
                column: "parent_id",
                principalTable: "categories",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_products_categories_category_id",
                table: "products",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_products_users_created_by_id",
                table: "products",
                column: "created_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_products_users_seller_id",
                table: "products",
                column: "seller_id",
                principalTable: "users",
                principalColumn: "id");
        }
    }
}
