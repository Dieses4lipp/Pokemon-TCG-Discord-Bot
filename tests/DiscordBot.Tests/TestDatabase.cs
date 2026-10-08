using DiscordBot.Core;
using Microsoft.Data.Sqlite;

namespace DiscordBot.Tests;

/// <summary>
///     A migrated database in a fresh temp directory, deleted again on dispose.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private TestDatabase(string directory, BotDatabase database)
    {
        Directory = directory;
        Database = database;
    }

    /// <summary>
    ///     Gets the temp directory; tests may put other files (e.g. legacy JSON) next to the database.
    /// </summary>
    public string Directory { get; }

    public BotDatabase Database { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "pokebot-tests", Guid.NewGuid().ToString("N"));
        var database = new BotDatabase(Path.Combine(directory, "bot.db"));
        await database.MigrateAsync();
        return new TestDatabase(directory, database);
    }

    public void Dispose()
    {
        // Pooled connections keep the file open on Windows
        SqliteConnection.ClearAllPools();
        System.IO.Directory.Delete(Directory, recursive: true);
    }
}
