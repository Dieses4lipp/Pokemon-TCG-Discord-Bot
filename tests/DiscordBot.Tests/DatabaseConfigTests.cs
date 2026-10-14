using DiscordBot.Core;
using DiscordBot.Core.Storage;

namespace DiscordBot.Tests;

public sealed class DatabaseConfigTests
{
    private static BotDatabase FromEnvironment(params (string Name, string Value)[] variables)
    {
        var environment = variables.ToDictionary(v => v.Name, v => v.Value);
        return BotDatabase.FromEnvironment(name => environment.GetValueOrDefault(name));
    }

    [Fact]
    public void Default_IsSqliteAtTheDefaultPath()
    {
        var database = FromEnvironment();

        Assert.Equal(DatabaseProvider.Sqlite, database.Provider);
        Assert.Equal(BotDatabase.DefaultPath, database.SqlitePath);
    }

    [Fact]
    public void Sqlite_UsesDatabasePath()
    {
        string path = Path.Combine(Path.GetTempPath(), "pokebot-tests", "config", "bot.db");

        var database = FromEnvironment(("DB_PROVIDER", "SQLite"), ("DATABASE_PATH", path));

        Assert.Equal(path, database.SqlitePath);
    }

    [Fact]
    public void Postgres_UsesTheConnectionStringAndKeepsThePasswordOutOfTheDescription()
    {
        var database = FromEnvironment(("DB_PROVIDER", "postgres"), ("DB_CONNECTION", "Host=db.lan;Port=5433;Database=apps;Username=pokebot;Password=secret"));

        Assert.Equal(DatabaseProvider.Postgres, database.Provider);
        Assert.Null(database.SqlitePath);
        Assert.Equal("postgres://db.lan:5433/apps", database.Description);
    }

    [Theory]
    [InlineData("postgres", null, "DB_CONNECTION is not set")]
    [InlineData("sqlite", "Host=db.lan", "DB_CONNECTION is set but DB_PROVIDER is sqlite")]
    [InlineData(null, "Host=db.lan", "DB_CONNECTION is set but DB_PROVIDER is sqlite")]
    [InlineData("mysql", null, "DB_PROVIDER 'mysql' is unknown")]
    public void InvalidSettings_FailFast(string? provider, string? connection, string expectedMessage)
    {
        var variables = new List<(string, string)>();
        if (provider != null)
            variables.Add(("DB_PROVIDER", provider));
        if (connection != null)
            variables.Add(("DB_CONNECTION", connection));

        var error = Assert.Throws<InvalidOperationException>(() => FromEnvironment([.. variables]));

        Assert.Contains(expectedMessage, error.Message);
    }
}
