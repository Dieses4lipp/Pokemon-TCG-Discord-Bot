using DiscordBot.Core;
using DiscordBot.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests;

public sealed class DatabaseBackupTests : IAsyncLifetime
{
    private TestDatabase _db = default!;
    private DatabaseBackup _backup = default!;

    public async Task InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync();
        _backup = new DatabaseBackup(_db.Database, NullLogger<DatabaseBackup>.Instance);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Create_WritesACopyWithTheCurrentData()
    {
        var users = new UserRepository(_db.Database, NullLogger<UserRepository>.Instance);
        await users.SaveUserCardsAsync(new UserCardCollection { UserId = 1, Balance = 7.5m, Cards = [new Card { Name = "Pikachu" }] });

        string path = await _backup.CreateAsync(DateTime.UtcNow);

        var copy = new UserRepository(BotDatabase.Sqlite(path), NullLogger<UserRepository>.Instance);
        var restored = await copy.LoadUserCardsAsync(1);
        Assert.Equal(7.5m, restored.Balance);
        Assert.Equal("Pikachu", Assert.Single(restored.Cards).Name);
    }

    [Fact]
    public async Task Create_KeepsOnlyTheNewestCopies()
    {
        var start = new DateTime(2026, 10, 1, 4, 0, 0, DateTimeKind.Utc);
        for (int day = 0; day < DatabaseBackup.KeepCount + 3; day++)
            await _backup.CreateAsync(start.AddDays(day));

        var kept = Directory.GetFiles(_backup.BackupDirectory).Select(Path.GetFileName).Order().ToList();

        Assert.Equal(DatabaseBackup.KeepCount, kept.Count);
        Assert.Equal("bot-20261004-040000.db", kept[0]);
        Assert.Equal("bot-20261010-040000.db", kept[^1]);
    }

    [Fact]
    public async Task TimeUntilNextBackup_IsADayAfterTheNewestCopy()
    {
        Assert.Equal(TimeSpan.Zero, _backup.TimeUntilNextBackup(DateTime.UtcNow));

        string path = await _backup.CreateAsync(DateTime.UtcNow);
        var writtenAt = File.GetLastWriteTimeUtc(path);

        Assert.Equal(TimeSpan.FromHours(20), _backup.TimeUntilNextBackup(writtenAt.AddHours(4)));
        Assert.Equal(TimeSpan.Zero, _backup.TimeUntilNextBackup(writtenAt.AddHours(25)));
    }
}
