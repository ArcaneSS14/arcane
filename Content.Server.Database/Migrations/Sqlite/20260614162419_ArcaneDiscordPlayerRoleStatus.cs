using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ArcaneDiscordPlayerRoleStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // arcane discord link start
            migrationBuilder.AddColumn<bool>(
                name: "has_player_role",
                table: "rmc_discord_accounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "player_role_updated_at",
                table: "rmc_discord_accounts",
                type: "TEXT",
                nullable: true);
            // arcane discord link end
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // arcane discord link start
            migrationBuilder.DropColumn(
                name: "has_player_role",
                table: "rmc_discord_accounts");

            migrationBuilder.DropColumn(
                name: "player_role_updated_at",
                table: "rmc_discord_accounts");
            // arcane discord link end
        }
    }
}
