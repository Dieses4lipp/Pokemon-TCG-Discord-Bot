using DiscordBot.Core;
using DiscordBot.Events.Expedition;
using DiscordBot.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests;

public sealed class UserRepositoryTests : IAsyncLifetime
{
    private TestDatabase _db = default!;
    private UserRepository _users = default!;

    public async Task InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync();
        _users = new UserRepository(_db.Database, NullLogger<UserRepository>.Instance);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private static Card MakeCard(string name, double? cardmarketAvg = null) => new()
    {
        SetId = "swsh1",
        LocalId = "25",
        Name = name,
        Rarity = "Rare",
        Image = "https://assets.tcgdex.net/en/swsh/swsh1/25",
        Pricing = cardmarketAvg == null ? null : new CardPricing { Cardmarket = new CardmarketPricing { Avg = cardmarketAvg } },
    };

    [Fact]
    public async Task Load_ReturnsAnEmptyCollectionForUnknownUsers()
    {
        var collection = await _users.LoadUserCardsAsync(42);

        Assert.Equal(42UL, collection.UserId);
        Assert.Empty(collection.Cards);
        Assert.Equal(0m, collection.Balance);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsTheCollection()
    {
        var collection = new UserCardCollection
        {
            UserId = 1,
            Balance = 12.34m,
            PacksPulled = 3,
            CardsTraded = 1,
            Cards = [MakeCard("Pikachu", 1.99), MakeCard("Eevee") with { IsLocked = true }],
            ActiveExpedition = new ActiveExpedition
            {
                LocationId = "mt-moon",
                StartTimeUtc = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc),
                EndTimeUtc = new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc),
            },
        };
        await _users.SaveUserCardsAsync(collection);
        collection.FavoriteCardId = collection.Cards[0].InstanceId;
        await _users.SaveUserCardsAsync(collection);

        var loaded = await _users.LoadUserCardsAsync(1);

        Assert.Equal(12.34m, loaded.Balance);
        Assert.Equal(3, loaded.PacksPulled);
        Assert.Equal(1, loaded.CardsTraded);
        Assert.Equal(collection.Cards, loaded.Cards);
        Assert.Equal("Pikachu", loaded.FavoriteCard?.Name);
        Assert.Equal(1.99, loaded.Cards[0].Pricing?.Cardmarket?.Avg);
        Assert.Null(loaded.Cards[1].Pricing);
        Assert.Equal(["Eevee"], loaded.CardsOnExpedition.Select(c => c.Name));
        Assert.Equal("mt-moon", loaded.ActiveExpedition?.LocationId);
        Assert.Equal(collection.ActiveExpedition.EndTimeUtc, loaded.ActiveExpedition?.EndTimeUtc);
    }

    [Fact]
    public async Task Save_GivesEveryCopyItsOwnId()
    {
        var collection = new UserCardCollection { UserId = 1, Cards = [MakeCard("Pikachu"), MakeCard("Pikachu")] };

        await _users.SaveUserCardsAsync(collection);

        Assert.All(collection.Cards, c => Assert.NotEqual(0, c.InstanceId));
        Assert.NotEqual(collection.Cards[0].InstanceId, collection.Cards[1].InstanceId);
    }

    [Fact]
    public async Task Save_DeletesRemovedCardsAndTheirFavorite()
    {
        var collection = new UserCardCollection { UserId = 1, Cards = [MakeCard("Pikachu"), MakeCard("Pikachu")] };
        await _users.SaveUserCardsAsync(collection);
        collection.FavoriteCardId = collection.Cards[0].InstanceId;
        await _users.SaveUserCardsAsync(collection);

        collection.Cards.RemoveAt(0);
        await _users.SaveUserCardsAsync(collection);

        var loaded = await _users.LoadUserCardsAsync(1);
        Assert.Equal(collection.Cards[0].InstanceId, Assert.Single(loaded.Cards).InstanceId);
        Assert.Null(loaded.FavoriteCardId);
    }

    [Fact]
    public async Task SaveBothSidesOfATrade_MovesTheCardWithoutDeletingIt()
    {
        var sender = new UserCardCollection { UserId = 1, Cards = [MakeCard("Pikachu")] };
        var receiver = new UserCardCollection { UserId = 2, Balance = 10m };
        await _users.SaveUserCardsAsync(sender, receiver);
        sender = await _users.LoadUserCardsAsync(1);
        receiver = await _users.LoadUserCardsAsync(2);

        var result = TradeSwap.Execute(new TradeSession(1, 2, sender.Cards[0], null, 4m), sender, receiver);
        Assert.True(result.Succeeded);
        await _users.SaveUserCardsAsync(sender, receiver);

        var loadedSender = await _users.LoadUserCardsAsync(1);
        var loadedReceiver = await _users.LoadUserCardsAsync(2);
        Assert.Empty(loadedSender.Cards);
        Assert.Equal(result.CardFromSender!.InstanceId, Assert.Single(loadedReceiver.Cards).InstanceId);
        Assert.Equal(4m, loadedSender.Balance);
        Assert.Equal(6m, loadedReceiver.Balance);
    }

    [Fact]
    public async Task Archive_KeepsTheCollectionUntilThePurge()
    {
        await _users.SaveUserCardsAsync(new UserCardCollection { UserId = 1, Cards = [MakeCard("Pikachu")] });

        Assert.True(await _users.ArchiveUserCardsAsync(1));
        Assert.False(await _users.ArchiveUserCardsAsync(2));

        Assert.Equal(0, await _users.PurgeArchivedUsersAsync(DateTime.UtcNow.AddDays(29)));
        Assert.Equal(1, await _users.PurgeArchivedUsersAsync(DateTime.UtcNow.AddDays(31)));
        Assert.Empty((await _users.LoadUserCardsAsync(1)).Cards);
    }

    [Fact]
    public async Task Load_RestoresAnArchivedCollection()
    {
        await _users.SaveUserCardsAsync(new UserCardCollection { UserId = 1, Cards = [MakeCard("Pikachu")] });
        await _users.ArchiveUserCardsAsync(1);

        Assert.Single((await _users.LoadUserCardsAsync(1)).Cards);
        Assert.Equal(0, await _users.PurgeArchivedUsersAsync(DateTime.UtcNow.AddDays(31)));
    }
}
