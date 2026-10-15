using Microsoft.Data.Sqlite;

namespace DiscordBot.Tests;

/// <summary>
///     The hand-written schema scripts the bot used before EF Core, copied verbatim from
///     <c>Core/BotDatabase.cs</c> at schema version 2. Builds the <c>bot.db</c> files that exist in
///     production, so the baseline and the EF schema can be checked against them.
/// </summary>
public static class LegacySqliteSchema
{
    /// <summary>
    ///     Script N brings <c>PRAGMA user_version</c> from N to N + 1.
    /// </summary>
    public static readonly string[] Scripts =
    [
        """
        CREATE TABLE users (
            user_id          INTEGER PRIMARY KEY,
            balance_cents    INTEGER NOT NULL DEFAULT 0,
            packs_pulled     INTEGER NOT NULL DEFAULT 0,
            cards_traded     INTEGER NOT NULL DEFAULT 0,
            favorite_card_id INTEGER REFERENCES cards (id) ON DELETE SET NULL,
            left_at_utc      TEXT
        );

        CREATE TABLE cards (
            id                     INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id                INTEGER NOT NULL REFERENCES users (user_id) ON DELETE CASCADE,
            set_id                 TEXT,
            local_id               TEXT,
            name                   TEXT NOT NULL,
            rarity                 TEXT,
            image                  TEXT,
            cardmarket_avg_cents   INTEGER,
            tcgplayer_market_cents INTEGER,
            tcgplayer_low_cents    INTEGER,
            is_locked              INTEGER NOT NULL DEFAULT 0
        );

        CREATE INDEX ix_cards_user_id ON cards (user_id);

        CREATE TABLE expeditions (
            user_id        INTEGER PRIMARY KEY REFERENCES users (user_id) ON DELETE CASCADE,
            location_id    TEXT NOT NULL,
            start_time_utc TEXT NOT NULL,
            end_time_utc   TEXT NOT NULL
        );

        CREATE TABLE settings (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        CREATE TABLE locked_sets (
            set_id TEXT PRIMARY KEY
        );
        """,
        """
        CREATE TABLE expedition_locations (
            id                 TEXT PRIMARY KEY COLLATE NOCASE,
            name               TEXT NOT NULL,
            duration_minutes   INTEGER NOT NULL,
            min_reward         REAL NOT NULL,
            max_reward         REAL NOT NULL,
            card_reward_chance REAL NOT NULL,
            card_reward_count  INTEGER NOT NULL,
            card_reward_set_id TEXT
        );
        """,
    ];

    /// <summary>
    ///     Creates a database at <paramref name="path"/> the way the old <c>BotDatabase.MigrateAsync</c>
    ///     did, stopping at <paramref name="version"/>.
    /// </summary>
    public static async Task CreateAsync(string path, int version = 2)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString());
        await connection.OpenAsync();
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;");
        for (int v = 0; v < version; v++)
            await ExecuteAsync(connection, Scripts[v] + $"PRAGMA user_version = {v + 1};");
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
