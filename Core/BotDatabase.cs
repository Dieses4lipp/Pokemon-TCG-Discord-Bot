using DiscordBot.Core.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Core;

/// <summary>
///     The database holding user collections, expeditions and bot settings: an SQLite file or a
///     Postgres database. Hands out one <see cref="BotDbContext"/> per operation and brings the
///     schema up to date on startup.
/// </summary>
public sealed class BotDatabase
{
    /// <summary>
    ///     Where the SQLite database lives unless <c>DATABASE_PATH</c> says otherwise:
    ///     <c>/app/data/bot.db</c> in the container, on the data volume.
    /// </summary>
    public static readonly string DefaultPath = Path.Combine(AppContext.BaseDirectory, "data", "bot.db");

    /// <summary>
    ///     The <c>PRAGMA user_version</c> the hand-written schema scripts left behind. The initial EF
    ///     migration builds exactly that schema.
    /// </summary>
    private const int LegacySchemaVersion = 2;

    private readonly Func<BotDbContext> _createContext;

    private BotDatabase(DatabaseProvider provider, string? sqlitePath, string description, Func<BotDbContext> createContext)
    {
        Provider = provider;
        SqlitePath = sqlitePath;
        Description = description;
        _createContext = createContext;
    }

    public DatabaseProvider Provider { get; }

    /// <summary>
    ///     Gets the path of the database file; <see langword="null"/> for Postgres.
    /// </summary>
    public string? SqlitePath { get; }

    /// <summary>
    ///     Gets where the database is, for the log: the file path, or host and database name
    ///     without credentials.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     An SQLite database in the file at <paramref name="path"/>; its directory is created.
    /// </summary>
    public static BotDatabase Sqlite(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        return new BotDatabase(DatabaseProvider.Sqlite, path, path, () => SqliteBotDbContext.Create(path));
    }

    /// <summary>
    ///     A Postgres database; the tables live in the <see cref="PostgresBotDbContext.Schema"/> schema.
    /// </summary>
    public static BotDatabase Postgres(string connectionString)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        return new BotDatabase(DatabaseProvider.Postgres, null, $"postgres://{builder.Host}:{builder.Port}/{builder.Database}",
            () => PostgresBotDbContext.Create(connectionString));
    }

    /// <summary>
    ///     Creates a context. The caller disposes it.
    /// </summary>
    public BotDbContext CreateContext() => _createContext();

    /// <summary>
    ///     Applies the migrations the database has not seen yet. An SQLite file from before EF Core
    ///     is marked as being at the initial migration first, since it already has that schema.
    /// </summary>
    /// <returns>
    ///     The migrations applied, oldest first; empty if the schema was up to date.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     The SQLite file is at a hand-written schema version other than the last one.
    /// </exception>
    public async Task<IReadOnlyList<string>> MigrateAsync(ILogger logger)
    {
        await using var context = CreateContext();

        if (Provider == DatabaseProvider.Sqlite)
        {
            // WAL lets the background purge and the interaction handlers read and write side by side
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;");
            await BaselineLegacySqliteAsync(context, logger);
        }

        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        await context.Database.MigrateAsync();
        return pending;
    }

    private static async Task BaselineLegacySqliteAsync(BotDbContext context, ILogger logger)
    {
        int userVersion = await context.Database.SqlQueryRaw<int>("SELECT user_version AS Value FROM pragma_user_version").SingleAsync();
        if (userVersion == 0)
            return;

        var history = context.GetService<IHistoryRepository>();
        if (await history.ExistsAsync())
            return;

        if (userVersion != LegacySchemaVersion)
            throw new InvalidOperationException(
                $"The database is at hand-written schema version {userVersion}; only version {LegacySchemaVersion} can be taken over by EF Core. " +
                "Start a build from before EF Core on it once to bring it to that version.");

        string initialMigration = context.Database.GetMigrations().First();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
        await context.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(initialMigration, ProductInfo.GetVersion())));
        await transaction.CommitAsync();

        logger.LogInformation("Took over the hand-written schema version {UserVersion} as EF migration {Migration}", userVersion, initialMigration);
    }
}
