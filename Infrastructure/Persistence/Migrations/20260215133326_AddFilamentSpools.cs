using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintIt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFilamentSpools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FilamentSpools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FilamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemainingGrams = table.Column<int>(type: "integer", nullable: false),
                    InitialGrams = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilamentSpools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FilamentSpools_Filaments_FilamentId",
                        column: x => x.FilamentId,
                        principalTable: "Filaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FilamentSpools_FilamentId",
                table: "FilamentSpools",
                column: "FilamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FilamentSpools");
        }
    }
}
