using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ArcaneDiscordRoleSponsorship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "roles_updated_at",
                table: "rmc_discord_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rmc_discord_account_roles",
                columns: table => new
                {
                    discord_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    role_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rmc_discord_account_roles", x => new { x.discord_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_rmc_discord_account_roles_rmc_discord_accounts_discord_id",
                        column: x => x.discord_id,
                        principalTable: "rmc_discord_accounts",
                        principalColumn: "rmc_discord_accounts_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rmc_discord_account_roles_role_id",
                table: "rmc_discord_account_roles",
                column: "role_id");

            migrationBuilder.Sql("""
                INSERT INTO rmc_discord_account_roles (discord_id, role_id)
                SELECT linked.discord_id, tier.discord_role
                FROM rmc_patrons AS patron
                INNER JOIN rmc_linked_accounts AS linked ON linked.player_id = patron.player_id
                INNER JOIN rmc_patron_tiers AS tier ON tier.rmc_patron_tiers_id = patron.tier_id
                ON CONFLICT (discord_id, role_id) DO NOTHING;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_rmc_patrons_rmc_patron_tiers_tier_id",
                table: "rmc_patrons");

            migrationBuilder.DropIndex(
                name: "IX_rmc_patrons_tier_id",
                table: "rmc_patrons");

            migrationBuilder.DropColumn(
                name: "tier_id",
                table: "rmc_patrons");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tier_id",
                table: "rmc_patrons",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE rmc_patrons AS patron
                SET tier_id = (
                    SELECT tier.rmc_patron_tiers_id
                    FROM rmc_linked_accounts AS linked
                    INNER JOIN rmc_discord_account_roles AS role ON role.discord_id = linked.discord_id
                    INNER JOIN rmc_patron_tiers AS tier ON tier.discord_role = role.role_id
                    WHERE linked.player_id = patron.player_id
                    ORDER BY tier.priority DESC, tier.rmc_patron_tiers_id
                    LIMIT 1
                );

                DELETE FROM rmc_patrons WHERE tier_id IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "tier_id",
                table: "rmc_patrons",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_rmc_patrons_tier_id",
                table: "rmc_patrons",
                column: "tier_id");

            migrationBuilder.AddForeignKey(
                name: "FK_rmc_patrons_rmc_patron_tiers_tier_id",
                table: "rmc_patrons",
                column: "tier_id",
                principalTable: "rmc_patron_tiers",
                principalColumn: "rmc_patron_tiers_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropTable(
                name: "rmc_discord_account_roles");

            migrationBuilder.DropColumn(
                name: "roles_updated_at",
                table: "rmc_discord_accounts");
        }
    }
}
