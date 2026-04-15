using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintIt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignAdminAuthStoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var bootstrapStoreId = new Guid("11111111-1111-1111-1111-111111111111");

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "Categories",
                type: "uuid",
                nullable: false,
                defaultValue: bootstrapStoreId);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "AdminUsers",
                type: "uuid",
                nullable: false,
                defaultValue: bootstrapStoreId);

            migrationBuilder.Sql(@"
INSERT INTO ""Stores"" (""Id"", ""Name"", ""Slug"", ""IsActive"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
VALUES ('11111111-1111-1111-1111-111111111111', 'PrintIt Default Store', 'default-store', TRUE, NOW(), NULL)
ON CONFLICT (""Id"") DO NOTHING;
");

            migrationBuilder.DropIndex(
                name: "IX_AdminUsers_NormalizedEmail",
                table: "AdminUsers");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_StoreId",
                table: "Categories",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_StoreId_NormalizedEmail",
                table: "AdminUsers",
                columns: new[] { "StoreId", "NormalizedEmail" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AdminUsers_Stores_StoreId",
                table: "AdminUsers",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Stores_StoreId",
                table: "Categories",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdminUsers_Stores_StoreId",
                table: "AdminUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Stores_StoreId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_StoreId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_AdminUsers_StoreId_NormalizedEmail",
                table: "AdminUsers");

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_NormalizedEmail",
                table: "AdminUsers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "AdminUsers");
        }
    }
}