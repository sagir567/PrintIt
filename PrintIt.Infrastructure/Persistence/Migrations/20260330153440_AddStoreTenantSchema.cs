using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintIt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreTenantSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaterialTypes_Name",
                table: "MaterialTypes");

            migrationBuilder.DropIndex(
                name: "IX_Filaments_MaterialTypeId_ColorId_Brand",
                table: "Filaments");

            migrationBuilder.DropIndex(
                name: "IX_Colors_Name",
                table: "Colors");

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "Products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "MaterialTypes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "Filaments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "Colors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_StoreId_Name",
                table: "Products",
                columns: new[] { "StoreId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialTypes_StoreId_Name",
                table: "MaterialTypes",
                columns: new[] { "StoreId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Filaments_MaterialTypeId",
                table: "Filaments",
                column: "MaterialTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Filaments_StoreId_MaterialTypeId_ColorId_Brand",
                table: "Filaments",
                columns: new[] { "StoreId", "MaterialTypeId", "ColorId", "Brand" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colors_StoreId_Name",
                table: "Colors",
                columns: new[] { "StoreId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_IsActive",
                table: "Stores",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Slug",
                table: "Stores",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Colors_Stores_StoreId",
                table: "Colors",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Filaments_Stores_StoreId",
                table: "Filaments",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialTypes_Stores_StoreId",
                table: "MaterialTypes",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Stores_StoreId",
                table: "Products",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Colors_Stores_StoreId",
                table: "Colors");

            migrationBuilder.DropForeignKey(
                name: "FK_Filaments_Stores_StoreId",
                table: "Filaments");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialTypes_Stores_StoreId",
                table: "MaterialTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Stores_StoreId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Products_StoreId_Name",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_MaterialTypes_StoreId_Name",
                table: "MaterialTypes");

            migrationBuilder.DropIndex(
                name: "IX_Filaments_MaterialTypeId",
                table: "Filaments");

            migrationBuilder.DropIndex(
                name: "IX_Filaments_StoreId_MaterialTypeId_ColorId_Brand",
                table: "Filaments");

            migrationBuilder.DropIndex(
                name: "IX_Colors_StoreId_Name",
                table: "Colors");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "MaterialTypes");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "Filaments");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "Colors");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialTypes_Name",
                table: "MaterialTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Filaments_MaterialTypeId_ColorId_Brand",
                table: "Filaments",
                columns: new[] { "MaterialTypeId", "ColorId", "Brand" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colors_Name",
                table: "Colors",
                column: "Name",
                unique: true);
        }
    }
}
