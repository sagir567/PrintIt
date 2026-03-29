using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintIt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogDomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_MaterialTypeId_ColorId_SizeLabel",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "BasePrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Products",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "PriceDelta",
                table: "ProductVariants",
                newName: "PriceOffset");

            migrationBuilder.RenameColumn(
                name: "LeadTimeDays",
                table: "ProductVariants",
                newName: "WidthMm");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Products",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MainImageUrl",
                table: "Products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "ProductVariants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DepthMm",
                table: "ProductVariants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HeightMm",
                table: "ProductVariants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ProductVariants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WeightGrams",
                table: "ProductVariants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "BasePricePerKg",
                table: "MaterialTypes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive",
                table: "Products",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_IsActive",
                table: "ProductVariants",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_SizeLabel_MaterialTypeId_ColorId",
                table: "ProductVariants",
                columns: new[] { "ProductId", "SizeLabel", "MaterialTypeId", "ColorId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_IsActive",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_IsActive",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_SizeLabel_MaterialTypeId_ColorId",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "MainImageUrl",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "DepthMm",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "HeightMm",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "WeightGrams",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "BasePricePerKg",
                table: "MaterialTypes");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Products",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "WidthMm",
                table: "ProductVariants",
                newName: "LeadTimeDays");

            migrationBuilder.RenameColumn(
                name: "PriceOffset",
                table: "ProductVariants",
                newName: "PriceDelta");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Products",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "Products",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Products",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_MaterialTypeId_ColorId_SizeLabel",
                table: "ProductVariants",
                columns: new[] { "ProductId", "MaterialTypeId", "ColorId", "SizeLabel" },
                unique: true);
        }
    }
}
