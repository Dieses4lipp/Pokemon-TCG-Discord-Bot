using DiscordBot.Core;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests;

public sealed class BotDatabaseTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pokebot-tests", Guid.NewGuid().ToString("N"));

    public BotDatabaseTests() => Directory.CreateDirectory(_directory);

    private string DbPath => Path.Combine(_directory, "bot.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task Migrate_CreatesANewDatabase()
    {
        var database = BotDatabase.Sqlite(DbPath);

        var applied = await database.MigrateAsync(NullLogger.Instance);

        Assert.Single(applied);
        Assert.Empty(await database.MigrateAsync(NullLogger.Instance));
    }

    [Fact]
    public async Task Migrate_TakesOverALegacyDatabaseWithoutTouchingItsData()
    {
        await LegacySqliteSchema.CreateAsync(DbPath);
        await ExecuteAsync("""
            INSERT INTO users (user_id, balance_cents, packs_pulled, cards_traded) VALUES (900000000000000001, 1250, 3, 1);
            INSERT INTO cards (user_id, set_id, local_id, name, rarity, cardmarket_avg_cents, is_locked)
                VALUES (900000000000000001, 'base1', '58', 'Pikachu', 'Common', 199, 1);
            UPDATE users SET favorite_card_id = 1 WHERE user_id = 900000000000000001;
            INSERT INTO expeditions (user_id, location_id, start_time_utc, end_time_utc)
                VALUES (900000000000000001, 'mt-moon', '2026-10-08T12:00:00.0000000Z', '2026-10-08T13:00:00.0000000Z');
            """);
        var database = BotDatabase.Sqlite(DbPath);

        var applied = await database.MigrateAsync(NullLogger.Instance);

        Assert.Empty(applied);
        var users = new UserRepository(database, NullLogger<UserRepository>.Instance);
        var collection = await users.LoadUserCardsAsync(900000000000000001);
        Assert.Equal(12.50m, collection.Balance);
        Assert.Equal("Pikachu", collection.FavoriteCard?.Name);
        Assert.Equal(1.99, collection.Cards[0].Pricing?.Cardmarket?.Avg);
        Assert.True(collection.Cards[0].IsLocked);
        Assert.Equal(new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc), collection.ActiveExpedition?.EndTimeUtc);
        Assert.Equal(DateTimeKind.Utc, collection.ActiveExpedition?.EndTimeUtc.Kind);

        // AUTOINCREMENT carries on after the existing rows
        collection.Cards.Add(collection.Cards[0] with { InstanceId = 0 });
        await users.SaveUserCardsAsync(collection);
        Assert.Equal(2, collection.Cards[1].InstanceId);
    }

    [Fact]
    public async Task Migrate_WritesTimesInTheLegacyFormat()
    {
        await LegacySqliteSchema.CreateAsync(DbPath);
        var database = BotDatabase.Sqlite(DbPath);
        await database.MigrateAsync(NullLogger.Instance);
        var users = new UserRepository(database, NullLogger<UserRepository>.Instance);
        await users.SaveUserCardsAsync(new Models.UserCardCollection { UserId = 1 });

        await users.ArchiveUserCardsAsync(1);

        string leftAt = (string)(await ScalarAsync("SELECT left_at_utc FROM users WHERE user_id = 1;"))!;
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{7}Z$", leftAt);
    }

    [Fact]
    public async Task Migrate_RefusesAnOlderLegacySchema()
    {
        await LegacySqliteSchema.CreateAsync(DbPath, version: 1);
        var database = BotDatabase.Sqlite(DbPath);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => database.MigrateAsync(NullLogger.Instance));

        Assert.Contains("version 1", error.Message);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={DbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={DbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
