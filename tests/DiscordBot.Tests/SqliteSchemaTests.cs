using DiscordBot.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Tests;

/// <summary>
///     The EF Core SQLite migration must build the schema the hand-written scripts built: existing
///     databases are only marked as migrated, never changed, so any difference would stay forever.
/// </summary>
public sealed class SqliteSchemaTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pokebot-tests", Guid.NewGuid().ToString("N"));

    public SqliteSchemaTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task EfMigration_BuildsTheLegacySchema()
    {
        string legacyPath = Path.Combine(_directory, "legacy.db");
        string efPath = Path.Combine(_directory, "ef.db");
        await LegacySqliteSchema.CreateAsync(legacyPath);
        await using (var context = SqliteBotDbContext.Create(efPath))
            await context.Database.MigrateAsync();

        var legacy = await DescribeAsync(legacyPath);
        var ef = await DescribeAsync(efPath);

        Assert.Contains("cards index ix_cards_user_id unique=0", legacy);
        Assert.Contains("expedition_locations uses COLLATE NOCASE", legacy);
        Assert.Contains("users.favorite_card_id -> cards.id on delete SET NULL", legacy);
        Assert.Equal(legacy, ef);
    }

    /// <summary>
    ///     One line per column, foreign key, index and collation of every bot table, sorted.
    ///     Constraint names are left out: SQLite does not use them.
    /// </summary>
    private static async Task<List<string>> DescribeAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        await connection.OpenAsync();

        var tables = await QueryAsync(connection,
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE '\\_\\_EF%' ESCAPE '\\' ORDER BY name;",
            r => r.GetString(0));

        var lines = new List<string>();
        foreach (string table in tables)
        {
            if (table == "sqlite_sequence")
            {
                lines.Add("table sqlite_sequence (AUTOINCREMENT in use)");
                continue;
            }

            lines.AddRange(await QueryAsync(connection, $"SELECT name, upper(type), \"notnull\", dflt_value, pk FROM pragma_table_info('{table}');",
                // Primary keys count as NOT NULL. An INTEGER one is the rowid and never NULL anyway; a
                // TEXT one accepts NULL in the old schema (an SQLite quirk) and not in the EF one, a
                // difference that only matters to code writing NULL keys, which the bot never does
                r => $"{table}.{r.GetString(0)} {r.GetString(1)} notnull={(r.GetInt64(4) > 0 ? 1 : r.GetInt64(2))} default={(r.IsDBNull(3) ? "-" : r.GetString(3))} pk={r.GetInt64(4)}"));
            lines.AddRange(await QueryAsync(connection, $"SELECT \"from\", \"table\", \"to\", on_delete FROM pragma_foreign_key_list('{table}');",
                r => $"{table}.{r.GetString(0)} -> {r.GetString(1)}.{r.GetString(2)} on delete {r.GetString(3)}"));
            lines.AddRange(await QueryAsync(connection, $"SELECT name, \"unique\", origin FROM pragma_index_list('{table}') WHERE origin = 'c';",
                r => $"{table} index {r.GetString(0)} unique={r.GetInt64(1)}"));

            string sql = (await QueryAsync(connection, $"SELECT sql FROM sqlite_master WHERE name = '{table}';", r => r.GetString(0)))[0];
            if (sql.Contains("COLLATE NOCASE", StringComparison.OrdinalIgnoreCase))
                lines.Add($"{table} uses COLLATE NOCASE");
        }

        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    private static async Task<List<T>> QueryAsync<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> read)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<T>();
        while (await reader.ReadAsync())
            rows.Add(read(reader));
        return rows;
    }
}
