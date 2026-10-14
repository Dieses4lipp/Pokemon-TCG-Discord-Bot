using DiscordBot.Core;
using DiscordBot.Events.Expedition;
using DiscordBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests;

/// <summary>
///     The stores on Postgres. The SQLite tests cover the logic; these cover what
///     differs per provider: the schema, ID and time mapping and the bulk statements.
/// </summary>
public sealed class PostgresStoreTests(PostgresServer server) : IClassFixture<PostgresServer>
{
    private static Card MakeCard(string name, double? cardmarketAvg = null) => new()
    {
        SetId = "swsh1",
        LocalId = "25",
        Name = name,
        Rarity = "Rare",
        Pricing = cardmarketAvg == null ? null : new CardPricing { Cardmarket = new CardmarketPricing { Avg = cardmarketAvg } },
    };

    private async Task<BotDatabase> CreateDatabaseAsync()
    {
        var database = server.CreateDatabase();
        await database.MigrateAsync(NullLogger.Instance);
        return database;
    }

    [PostgresFact]
    public async Task Migrate_PutsTheTablesInTheirOwnSchema()
    {
        var database = server.CreateDatabase();

        Assert.Single(await database.MigrateAsync(NullLogger.Instance));
        Assert.Empty(await database.MigrateAsync(NullLogger.Instance));

        await using var db = database.CreateContext();
        var tables = await db.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'pokebot' ORDER BY table_name").ToListAsync();
        Assert.Equal(["__EFMigrationsHistory", "cards", "expedition_locations", "expeditions", "locked_sets", "settings", "users"], tables);
    }

    [PostgresFact]
    public async Task UserRepository_RoundTripsATradeAndTheArchive()
    {
        var users = new UserRepository(await CreateDatabaseAsync(), NullLogger<UserRepository>.Instance);
        // Above 2^31: a Discord snowflake must survive the ulong to bigint mapping
        const ulong senderId = 900000000000000001, receiverId = 900000000000000002;
        var sender = new UserCardCollection
        {
            UserId = senderId,
            Balance = 12.34m,
            Cards = [MakeCard("Pikachu", 1.99), MakeCard("Eevee") with { IsLocked = true }],
            ActiveExpedition = new ActiveExpedition
            {
                LocationId = "mt-moon",
                StartTimeUtc = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc),
                EndTimeUtc = new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc),
            },
        };
        await users.SaveUserCardsAsync(sender, new UserCardCollection { UserId = receiverId, Balance = 10m });
        sender.FavoriteCardId = sender.Cards[0].InstanceId;
        await users.SaveUserCardsAsync(sender);

        sender = await users.LoadUserCardsAsync(senderId);
        var receiver = await users.LoadUserCardsAsync(receiverId);
        Assert.Equal(12.34m, sender.Balance);
        Assert.Equal("Pikachu", sender.FavoriteCard?.Name);
        Assert.Equal(1.99, sender.Cards[0].Pricing?.Cardmarket?.Avg);
        Assert.Equal(DateTimeKind.Utc, sender.ActiveExpedition?.EndTimeUtc.Kind);
        Assert.Equal(new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc), sender.ActiveExpedition?.EndTimeUtc);

        var result = TradeSwap.Execute(new TradeSession(senderId, receiverId, sender.Cards[0], null, 4m), sender, receiver);
        Assert.True(result.Succeeded);
        await users.SaveUserCardsAsync(sender, receiver);

        var loadedSender = await users.LoadUserCardsAsync(senderId);
        Assert.Equal(["Eevee"], loadedSender.Cards.Select(c => c.Name));
        Assert.Null(loadedSender.FavoriteCardId);
        Assert.Equal(result.CardFromSender!.InstanceId, Assert.Single((await users.LoadUserCardsAsync(receiverId)).Cards).InstanceId);

        Assert.True(await users.ArchiveUserCardsAsync(senderId));
        Assert.Equal(0, await users.PurgeArchivedUsersAsync(DateTime.UtcNow.AddDays(29)));
        Assert.Equal(1, await users.PurgeArchivedUsersAsync(DateTime.UtcNow.AddDays(31)));
        Assert.Empty((await users.LoadUserCardsAsync(senderId)).Cards);
    }

    [PostgresFact]
    public async Task BotStateStore_SavesAndRestoresTheRuntimeState()
    {
        var database = await CreateDatabaseAsync();
        var state = new BotState();
        state.Restore(false, 7, ["base1", "base2"]);
        await new BotStateStore(database, state, new SessionStore(), NullLogger<BotStateStore>.Instance).SaveAsync();
        state.Restore(false, 8, ["base1"]);
        await new BotStateStore(database, state, new SessionStore(), NullLogger<BotStateStore>.Instance).SaveAsync();

        var restored = new BotState();
        await new BotStateStore(database, restored, new SessionStore(), NullLogger<BotStateStore>.Instance).LoadAsync();

        Assert.False(restored.IsActive);
        Assert.Equal(8, restored.PullCount);
        Assert.Equal(["base1"], restored.LockedSets);
    }

    [PostgresFact]
    public async Task ExpeditionLocationStore_MatchesIdsCaseInsensitively()
    {
        var store = new ExpeditionLocationStore(await CreateDatabaseAsync(), Path.Combine(AppContext.BaseDirectory, "Data", "expeditionSettings.json"),
            NullLogger<ExpeditionLocationStore>.Instance);
        await store.LoadAsync();
        int count = store.Locations.Count;
        var edited = store.GetLocationById("mt-moon")!;

        await store.SaveAsync(new ExpeditionLocation
        {
            Id = "MT-MOON",
            Name = "Edited",
            DurationMinutes = edited.DurationMinutes,
            MinReward = edited.MinReward,
            MaxReward = edited.MaxReward,
            CardRewardChance = edited.CardRewardChance,
            CardRewardCount = edited.CardRewardCount,
        });

        Assert.Equal(count, store.Locations.Count);
        Assert.Equal("Edited", store.GetLocationById("mt-moon")?.Name);
    }
}
