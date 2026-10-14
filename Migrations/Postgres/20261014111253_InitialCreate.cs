using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DiscordBot.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pokebot");

            migrationBuilder.CreateTable(
                name: "expedition_locations",
                schema: "pokebot",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    min_reward = table.Column<double>(type: "double precision", nullable: false),
                    max_reward = table.Column<double>(type: "double precision", nullable: false),
                    card_reward_chance = table.Column<double>(type: "double precision", nullable: false),
                    card_reward_count = table.Column<int>(type: "integer", nullable: false),
                    card_reward_set_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expedition_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "locked_sets",
                schema: "pokebot",
                columns: table => new
                {
                    set_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locked_sets", x => x.set_id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                schema: "pokebot",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "cards",
                schema: "pokebot",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    set_id = table.Column<string>(type: "text", nullable: true),
                    local_id = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    rarity = table.Column<string>(type: "text", nullable: true),
                    image = table.Column<string>(type: "text", nullable: true),
                    cardmarket_avg_cents = table.Column<long>(type: "bigint", nullable: true),
                    tcgplayer_market_cents = table.Column<long>(type: "bigint", nullable: true),
                    tcgplayer_low_cents = table.Column<long>(type: "bigint", nullable: true),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "pokebot",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    balance_cents = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    packs_pulled = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cards_traded = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    favorite_card_id = table.Column<long>(type: "bigint", nullable: true),
                    left_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_users_cards_favorite_card_id",
                        column: x => x.favorite_card_id,
                        principalSchema: "pokebot",
                        principalTable: "cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "expeditions",
                schema: "pokebot",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    location_id = table.Column<string>(type: "text", nullable: false),
                    start_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expeditions", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_expeditions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "pokebot",
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cards_user_id",
                schema: "pokebot",
                table: "cards",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_cards_users_user_id",
                schema: "pokebot",
                table: "cards",
                column: "user_id",
                principalSchema: "pokebot",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cards_users_user_id",
                schema: "pokebot",
                table: "cards");

            migrationBuilder.DropTable(
                name: "expedition_locations",
                schema: "pokebot");

            migrationBuilder.DropTable(
                name: "expeditions",
                schema: "pokebot");

            migrationBuilder.DropTable(
                name: "locked_sets",
                schema: "pokebot");

            migrationBuilder.DropTable(
                name: "settings",
                schema: "pokebot");

            migrationBuilder.DropTable(
                name: "users",
                schema: "pokebot");

            migrationBuilder.DropTable(
                name: "cards",
                schema: "pokebot");
        }
    }
}
