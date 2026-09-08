using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class DropErpOrganPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "erp_organ_preferences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "erp_organ_preferences",
                columns: table => new
                {
                    erp_organ_preferences_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    data = table.Column<string>(type: "TEXT", nullable: false),
                    slot = table.Column<int>(type: "INTEGER", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_erp_organ_preferences", x => x.erp_organ_preferences_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_erp_organ_preferences_user_id_slot",
                table: "erp_organ_preferences",
                columns: new[] { "user_id", "slot" },
                unique: true);
        }
    }
}
