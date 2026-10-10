using DiscordBot.Core;
using DiscordBot.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests;

public sealed class LegacyJsonImporterTests : IAsyncLifetime
{
    private TestDatabase _db = default!;
    private string _legacyDirectory = default!;
    private LegacyJsonImporter _importer = default!;
    private UserRepository _users = default!;

    public async Task InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync();
        _legacyDirectory = Path.Combine(_db.Directory, "UserCards");
        Directory.CreateDirectory(Path.Combine(_legacyDirectory, "Left"));
        Directory.CreateDirectory(Path.Combine(_legacyDirectory, "State"));
        _importer = new LegacyJsonImporter(_db.Database, NullLogger<LegacyJsonImporter>.Instance);
        _users = new UserRepository(_db.Database, NullLogger<UserRepository>.Instance);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private void WriteLegacyFile(string relativePath, string json) =>
        File.WriteAllText(Path.Combine(_legacyDirectory, relativePath), json);

    // The shape the bot wrote before the database: the card's API "id" is the full card ID,
    // FavoriteCard is a copy, and the expedition lists the sent cards
    private const string PikachuJson = """
        {
          "id": "swsh1-25", "localId": "25", "Name": "Pikachu", "Rarity": "Common",
          "Image": "https://assets.tcgdex.net/en/swsh/swsh1/25", "IsLocked": false,
          "pricing": { "cardmarket": { "unit": "EUR", "avg": 0.25 }, "tcgplayer": { "unit": "USD", "market": 0.31, "low": 0.1 } }
        }
        """;

    [Fact]
    public async Task Import_MovesActiveAndArchivedUsersIntoTheDatabase()
    {
        WriteLegacyFile("1.json", $$"""
            {
              "UserId": 1,
              "Cards": [ {{PikachuJson}}, {{PikachuJson.Replace("\"IsLocked\": false", "\"IsLocked\": true")}} ],
              "PacksPulled": 4, "CardsTraded": 2, "Balance": 13.37,
              "FavoriteCard": {{PikachuJson}},
              "ActiveExpedition": {
                "LocationId": "mt-moon", "StartTimeUtc": "2026-10-08T12:00:00Z", "EndTimeUtc": "2026-10-08T13:00:00Z",
                "SentCards": [ {{PikachuJson}} ]
              }
            }
            """);
        WriteLegacyFile(Path.Combine("Left", "2.json"), $$"""{ "UserId": 2, "Cards": [ {{PikachuJson}} ], "Balance": 1.5 }""");
        File.SetLastWriteTimeUtc(Path.Combine(_legacyDirectory, "Left", "2.json"), DateTime.UtcNow.AddDays(-40));

        var result = await _importer.ImportAsync(_legacyDirectory);

        Assert.Equal(new LegacyImportResult(Users: 2, ArchivedUsers: 1, SkippedUsers: 0, Cards: 3, BotStateImported: false), result);

        var active = await _users.LoadUserCardsAsync(1);
        Assert.Equal(13.37m, active.Balance);
        Assert.Equal(4, active.PacksPulled);
        Assert.Equal(2, active.CardsTraded);
        Assert.Equal(2, active.Cards.Count);
        Assert.Equal("swsh1", active.Cards[0].SetId);
        Assert.Equal(0.25, active.Cards[0].Pricing?.Cardmarket?.Avg);
        Assert.Equal(active.Cards[0].InstanceId, active.FavoriteCardId);
        Assert.Equal("mt-moon", active.ActiveExpedition?.LocationId);
        Assert.Single(active.CardsOnExpedition);

        // Left 40 days ago: the archive time comes from the file, so the purge deletes it
        Assert.Equal(1, await _users.PurgeArchivedUsersAsync(DateTime.UtcNow));
    }

    [Fact]
    public async Task Import_UnlocksCardsWithoutARunningExpedition()
    {
        WriteLegacyFile("1.json", $$"""{ "UserId": 1, "Cards": [ {{PikachuJson.Replace("\"IsLocked\": false", "\"IsLocked\": true")}} ] }""");

        await _importer.ImportAsync(_legacyDirectory);

        Assert.False(Assert.Single((await _users.LoadUserCardsAsync(1)).Cards).IsLocked);
    }

    [Fact]
    public async Task Import_RestoresBotStateAndPaidPacksButNotTrades()
    {
        string createdAt = DateTime.UtcNow.AddHours(-1).ToString("O");
        WriteLegacyFile(Path.Combine("State", "botState.json"), $$"""
            {
              "BotActive": false, "LockedSets": [ "base1" ], "PullCount": 99,
              "ActiveTrades": [ { "SenderId": 1, "ReceiverId": 2, "CardToTrade": {{PikachuJson}}, "MoneyToReceive": 1.0, "CreatedAtUtc": "{{createdAt}}" } ],
              "PackSessions": [ {
                "MessageId": 500, "UserId": 1, "CurrentIndex": 1, "CreatedAtUtc": "{{createdAt}}",
                "Cards": [ {{PikachuJson}}, {{PikachuJson.Replace("Pikachu", "Eevee")}}, {{PikachuJson}} ],
                "SavedCardIdentifiers": [ "Pikachu_Common" ]
              } ]
            }
            """);

        var result = await _importer.ImportAsync(_legacyDirectory);
        Assert.True(result?.BotStateImported);

        var botState = new BotState();
        var sessions = new SessionStore();
        await new BotStateStore(_db.Database, botState, sessions, NullLogger<BotStateStore>.Instance).LoadAsync();

        Assert.False(botState.IsActive);
        Assert.Equal(99, botState.PullCount);
        Assert.Equal(["base1"], botState.LockedSets);
        Assert.Empty(sessions.Trades);
        var pack = sessions.Packs[500];
        Assert.Equal(1, pack.CurrentIndex);
        Assert.Equal([0, 2], pack.SavedCardIndices.Order());
    }

    [Fact]
    public async Task Import_RunsOnlyOnce()
    {
        WriteLegacyFile("1.json", $$"""{ "UserId": 1, "Cards": [ {{PikachuJson}} ] }""");

        Assert.NotNull(await _importer.ImportAsync(_legacyDirectory));
        Assert.Null(await _importer.ImportAsync(_legacyDirectory));

        Assert.Single((await _users.LoadUserCardsAsync(1)).Cards);
    }

    [Fact]
    public async Task Import_SkipsUsersTheDatabaseAlreadyHas()
    {
        await _users.SaveUserCardsAsync(new UserCardCollection { UserId = 1, Balance = 50m });
        WriteLegacyFile("1.json", $$"""{ "UserId": 1, "Cards": [ {{PikachuJson}} ], "Balance": 3 }""");

        var result = await _importer.ImportAsync(_legacyDirectory);

        Assert.Equal(1, result?.SkippedUsers);
        var collection = await _users.LoadUserCardsAsync(1);
        Assert.Equal(50m, collection.Balance);
        Assert.Empty(collection.Cards);
    }

    [Fact]
    public async Task Import_WithoutTheDirectoryRunsAgainOnceItExists()
    {
        string missing = Path.Combine(_db.Directory, "not-mounted");

        Assert.Null(await _importer.ImportAsync(missing));

        Directory.CreateDirectory(missing);
        File.WriteAllText(Path.Combine(missing, "1.json"), $$"""{ "UserId": 1, "Cards": [ {{PikachuJson}} ] }""");
        Assert.Equal(1, (await _importer.ImportAsync(missing))?.Users);
    }

    [Fact]
    public async Task Import_FailsOnABrokenFileAndImportsNothing()
    {
        WriteLegacyFile("1.json", $$"""{ "UserId": 1, "Cards": [ {{PikachuJson}} ] }""");
        WriteLegacyFile("2.json", "{ not json");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => _importer.ImportAsync(_legacyDirectory));

        Assert.Contains("2.json", error.Message);
        Assert.Empty((await _users.LoadUserCardsAsync(1)).Cards);
    }
}
