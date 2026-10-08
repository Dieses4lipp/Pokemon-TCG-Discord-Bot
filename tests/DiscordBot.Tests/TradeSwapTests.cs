using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Tests;

public class TradeSwapTests
{
    private static long _nextInstanceId = 1;

    private static Card MakeCard(string name, bool locked = false) =>
        new() { InstanceId = _nextInstanceId++, Name = name, Rarity = "Rare", IsLocked = locked };

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
        var session = new TradeSession(1, 2, sender.Cards[0], receiver.Cards[0], 20);

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
        var session = new TradeSession(1, 2, sender.Cards[0], null, 5);

        var result = TradeSwap.Execute(session, sender, receiver);

        Assert.True(result.Succeeded);
        Assert.Null(result.CardFromReceiver);
        Assert.Empty(sender.Cards);
        Assert.Equal(["Charizard", "Pikachu"], receiver.Cards.Select(c => c.Name));
        Assert.Equal(5, sender.Balance);
        Assert.Equal(0, receiver.Balance);
    }

    [Fact]
    public void Execute_MovesExactlyTheOfferedCopy()
    {
        var sender = MakeCollection(1, 0, MakeCard("Pikachu"), MakeCard("Pikachu"));
        var receiver = MakeCollection(2, 10);
        var offered = sender.Cards[1];

        TradeSwap.Execute(new TradeSession(1, 2, offered, null, 1), sender, receiver);

        Assert.Same(offered, Assert.Single(receiver.Cards));
        Assert.NotEqual(offered.InstanceId, Assert.Single(sender.Cards).InstanceId);
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
        var offered = MakeCard("Pikachu");
        var requested = MakeCard("Charizard");

        // A missing card is another copy of the same card, so only the instance ID tells them apart
        var sender = MakeCollection(1, 10, expected switch
        {
            TradeFailure.SenderCardMissing => MakeCard("Pikachu"),
            TradeFailure.SenderCardLocked => offered with { IsLocked = true },
            _ => offered,
        });
        var receiver = MakeCollection(2, expected == TradeFailure.ReceiverBalanceTooLow ? 1 : 50, expected switch
        {
            TradeFailure.ReceiverCardMissing => MakeCard("Charizard"),
            TradeFailure.ReceiverCardLocked => requested with { IsLocked = true },
            _ => requested,
        });
        var senderBefore = sender.Cards.ToList();
        var receiverBefore = receiver.Cards.ToList();
        var session = new TradeSession(1, 2, offered, requested, 20);

        var result = TradeSwap.Execute(session, sender, receiver);

        Assert.Equal(expected, result.Failure);
        Assert.Equal(senderBefore, sender.Cards);
        Assert.Equal(receiverBefore, receiver.Cards);
        Assert.Equal(10, sender.Balance);
        Assert.Equal(0, sender.CardsTraded);
        Assert.Equal(0, receiver.CardsTraded);
    }

    [Fact]
    public void Execute_DropsTheFavoriteOnlyWhenThatCopyLeaves()
    {
        var sender = MakeCollection(1, 0, MakeCard("Pikachu"), MakeCard("Pikachu"));
        sender.FavoriteCardId = sender.Cards[0].InstanceId;
        var receiver = MakeCollection(2, 10);

        TradeSwap.Execute(new TradeSession(1, 2, sender.Cards[1], null, 1), sender, receiver);
        Assert.NotNull(sender.FavoriteCard);

        TradeSwap.Execute(new TradeSession(1, 2, sender.Cards[0], null, 1), sender, receiver);
        Assert.Null(sender.FavoriteCard);
    }
}
