using Microsoft.Data.Sqlite;

namespace DiscordBot.Core;

/// <summary>
///     The SQLite database holding user collections, expeditions and bot settings. Opens one
///     pooled connection per operation and brings the schema up to date on startup.
/// </summary>
public sealed class BotDatabase
{
    /// <summary>
    ///     Where the database lives unless <c>DATABASE_PATH</c> says otherwise: <c>/app/data/bot.db</c>
    ///     in the container, on the data volume.
    /// </summary>
    public static readonly string DefaultPath = Path.Combine(AppContext.BaseDirectory, "data", "bot.db");

    /// <summary>
    ///     Schema scripts in order; script N brings <c>PRAGMA user_version</c> from N to N + 1.
    ///     Never edit a script that has shipped, append a new one instead.
    /// </summary>
    private static readonly string[] Migrations =
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

    private readonly string _connectionString;

    public BotDatabase(string path)
    {
        FilePath = path;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            ForeignKeys = true,
        }.ToString();
    }

    /// <summary>
    ///     Gets the path of the database file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    ///     Opens a connection with foreign keys enforced. The caller disposes it.
    /// </summary>
    public async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>
    ///     Applies the schema scripts the database has not seen yet, each in its own transaction.
    /// </summary>
    /// <returns>
    ///     The schema version before the migration; 0 for a new database.
    /// </returns>
    public async Task<int> MigrateAsync()
    {
        await using var connection = await OpenAsync();

        // WAL lets the background purge and the interaction handlers read and write side by side
        await ExecuteAsync(connection, null, "PRAGMA journal_mode = WAL;");

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version;";
        int startVersion = Convert.ToInt32(await versionCommand.ExecuteScalarAsync());

        for (int version = startVersion; version < Migrations.Length; version++)
        {
            await using var transaction = connection.BeginTransaction();
            await ExecuteAsync(connection, transaction, Migrations[version]);
            // PRAGMA does not take parameters; the value is a loop counter, not input
            await ExecuteAsync(connection, transaction, $"PRAGMA user_version = {version + 1};");
            await transaction.CommitAsync();
        }

        return startVersion;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
