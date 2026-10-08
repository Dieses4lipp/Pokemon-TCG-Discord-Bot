using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Tests;

public class TradeSwapTests
{
    private static Card MakeCard(string name, string rarity = "Rare", bool locked = false) =>
        new() { Name = name, Rarity = rarity, IsLocked = locked };

    private static UserCardCollection MakeCollection(ulong userId, decimal balance, params Card[] cards) => new()

    {
        UserId = userId,
        Balance = balance,
        Cards = [.. cards],
    };

    [Fact]
    public void Execute_SwapsCardsAndMoney()
    {
        var sender = MakeCollection(1, 10, MakeCard("Pikachu"));
        var receiver = MakeCollection(2, 50, MakeCard("Charizard"));
        var session = new TradeSession(1, 2, MakeCard("Pikachu"), MakeCard("Charizard"), 20);

        var result = TradeSwap.Execute(session, sender, receiver);

        Assert.True(result.Succeeded);
        Assert.Equal(["Charizard"], sender.Cards.Select(c => c.Name));
        Assert.Equal(["Pikachu"], receiver.Cards.Select(c => c.Name));
        Assert.Equal(30, sender.Balance);
        Assert.Equal(30, receiver.Balance);
        Assert.Equal(1, sender.CardsTraded);
        Assert.Equal(1, receiver.CardsTraded);
    }

    [Fact]
    public void Execute_CardForMoneyOnlyMovesTheOfferedCard()
    {
        var sender = MakeCollection(1, 0, MakeCard("Pikachu"));
        var receiver = MakeCollection(2, 5, MakeCard("Charizard"));
        var session = new TradeSession(1, 2, MakeCard("Pikachu"), null, 5);

        var result = TradeSwap.Execute(session, sender, receiver);

        Assert.True(result.Succeeded);
        Assert.Null(result.CardFromReceiver);
        Assert.Empty(sender.Cards);
        Assert.Equal(["Charizard", "Pikachu"], receiver.Cards.Select(c => c.Name));
        Assert.Equal(5, sender.Balance);
        Assert.Equal(0, receiver.Balance);
    }

    [Fact]
    public void Execute_MovesOnlyOneOfSeveralCopies()
    {
        var sender = MakeCollection(1, 0, MakeCard("Pikachu"), MakeCard("Pikachu"));
        var receiver = MakeCollection(2, 10);
        var session = new TradeSession(1, 2, MakeCard("Pikachu"), null, 1);

        TradeSwap.Execute(session, sender, receiver);

        Assert.Single(sender.Cards);
        Assert.Single(receiver.Cards);
    }

    public static TheoryData<TradeFailure> Failures => new()
    {
        TradeFailure.SenderCardMissing,
        TradeFailure.SenderCardLocked,
        TradeFailure.ReceiverCardMissing,
        TradeFailure.ReceiverCardLocked,
        TradeFailure.ReceiverBalanceTooLow,
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void Execute_ChangesNothingWhenACheckFails(TradeFailure expected)
    {
        var sender = MakeCollection(1, 10, expected switch
        {
            TradeFailure.SenderCardMissing => MakeCard("Eevee"),
            TradeFailure.SenderCardLocked => MakeCard("Pikachu", locked: true),
            _ => MakeCard("Pikachu"),
        });
        var receiver = MakeCollection(2, expected == TradeFailure.ReceiverBalanceTooLow ? 1 : 50, expected switch
        {
            TradeFailure.ReceiverCardMissing => MakeCard("Mew"),
            TradeFailure.ReceiverCardLocked => MakeCard("Charizard", locked: true),
            _ => MakeCard("Charizard"),
        });
        var senderBefore = sender.Cards.ToList();
        var receiverBefore = receiver.Cards.ToList();
        var session = new TradeSession(1, 2, MakeCard("Pikachu"), MakeCard("Charizard"), 20);

        var result = TradeSwap.Execute(session, sender, receiver);

        Assert.Equal(expected, result.Failure);
        Assert.Equal(senderBefore, sender.Cards);
        Assert.Equal(receiverBefore, receiver.Cards);
        Assert.Equal(10, sender.Balance);
        Assert.Equal(0, sender.CardsTraded);
        Assert.Equal(0, receiver.CardsTraded);
    }

    [Fact]
    public void Execute_MatchesCardsByNameAndRarity()
    {
        var sender = MakeCollection(1, 0, MakeCard("Pikachu", "Common"));
        var receiver = MakeCollection(2, 10);
        var session = new TradeSession(1, 2, MakeCard("Pikachu", "Holo Rare"), null, 1);

        Assert.Equal(TradeFailure.SenderCardMissing, TradeSwap.Execute(session, sender, receiver).Failure);
    }

    [Fact]
    public void Execute_ClearsTheFavoriteOnlyWhenNoCopyIsLeft()
    {
        var sender = MakeCollection(1, 0, MakeCard("Pikachu"), MakeCard("Pikachu"));
        sender.FavoriteCard = MakeCard("Pikachu");
        var receiver = MakeCollection(2, 10);
        var session = new TradeSession(1, 2, MakeCard("Pikachu"), null, 1);

        TradeSwap.Execute(session, sender, receiver);
        Assert.NotNull(sender.FavoriteCard);

        TradeSwap.Execute(session, sender, receiver);
        Assert.Null(sender.FavoriteCard);
    }
}
