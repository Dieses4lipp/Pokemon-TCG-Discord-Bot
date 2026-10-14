using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expedition_locations",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    duration_minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    min_reward = table.Column<double>(type: "REAL", nullable: false),
                    max_reward = table.Column<double>(type: "REAL", nullable: false),
                    card_reward_chance = table.Column<double>(type: "REAL", nullable: false),
                    card_reward_count = table.Column<int>(type: "INTEGER", nullable: false),
                    card_reward_set_id = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expedition_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "locked_sets",
                columns: table => new
                {
                    set_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locked_sets", x => x.set_id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "cards",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    set_id = table.Column<string>(type: "TEXT", nullable: true),
                    local_id = table.Column<string>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    rarity = table.Column<string>(type: "TEXT", nullable: true),
                    image = table.Column<string>(type: "TEXT", nullable: true),
                    cardmarket_avg_cents = table.Column<long>(type: "INTEGER", nullable: true),
                    tcgplayer_market_cents = table.Column<long>(type: "INTEGER", nullable: true),
                    tcgplayer_low_cents = table.Column<long>(type: "INTEGER", nullable: true),
                    is_locked = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cards", x => x.id);
                    // Inline, not a later AddForeignKey: SQLite cannot add a constraint to an existing
                    // table, but accepts a reference to users before that table exists
                    table.ForeignKey(
                        name: "FK_cards_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    balance_cents = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    packs_pulled = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    cards_traded = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    favorite_card_id = table.Column<long>(type: "INTEGER", nullable: true),
                    left_at_utc = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_users_cards_favorite_card_id",
                        column: x => x.favorite_card_id,
                        principalTable: "cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "expeditions",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    location_id = table.Column<string>(type: "TEXT", nullable: false),
                    start_time_utc = table.Column<string>(type: "TEXT", nullable: false),
                    end_time_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expeditions", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_expeditions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cards_user_id",
                table: "cards",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expedition_locations");

            migrationBuilder.DropTable(
                name: "expeditions");

            migrationBuilder.DropTable(
                name: "locked_sets");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "cards");
        }
    }
}
