using DiscordBot.Models;

namespace DiscordBot.Core;

/// <summary>
///     Carries out a confirmed trade between two loaded collections.
/// </summary>
public static class TradeSwap
{
    /// <summary>
    ///     Checks that both sides can still fulfil the trade and, if so, moves the cards and money
    ///     and updates the trade stats and favorites. Nothing is changed if a check fails.
    /// </summary>
    /// <param name="session">
    ///     The trade that was confirmed.
    /// </param>
    /// <param name="sender">
    ///     The collection of the user who proposed the trade.
    /// </param>
    /// <param name="receiver">
    ///     The collection of the user who confirmed the trade.
    /// </param>
    /// <returns>
    ///     The cards that changed hands, or why the trade failed.
    /// </returns>
    public static TradeResult Execute(TradeSession session, UserCardCollection sender, UserCardCollection receiver)
    {
        var cardFromSender = FindCard(sender, session.CardToTrade);
        if (cardFromSender == null) return new TradeResult(TradeFailure.SenderCardMissing);
        if (cardFromSender.IsLocked) return new TradeResult(TradeFailure.SenderCardLocked);

        Card? cardFromReceiver = null;
        if (session.CardToReceive != null)
        {
            cardFromReceiver = FindCard(receiver, session.CardToReceive);
            if (cardFromReceiver == null) return new TradeResult(TradeFailure.ReceiverCardMissing);
            if (cardFromReceiver.IsLocked) return new TradeResult(TradeFailure.ReceiverCardLocked);
        }

        if (session.MoneyToReceive > 0 && receiver.Balance < session.MoneyToReceive)
            return new TradeResult(TradeFailure.ReceiverBalanceTooLow);

        sender.Cards.Remove(cardFromSender);
        receiver.Cards.Add(cardFromSender);

        if (cardFromReceiver != null)
        {
            receiver.Cards.Remove(cardFromReceiver);
            sender.Cards.Add(cardFromReceiver);
        }

        ClearFavoriteIfGone(sender, cardFromSender);
        if (cardFromReceiver != null)
            ClearFavoriteIfGone(receiver, cardFromReceiver);

        if (session.MoneyToReceive > 0)
        {
            receiver.Balance -= session.MoneyToReceive;
            sender.Balance += session.MoneyToReceive;
        }

        sender.CardsTraded++;
        receiver.CardsTraded++;

        return new TradeResult(null, cardFromSender, cardFromReceiver);
    }

    private static Card? FindCard(UserCardCollection collection, Card card) =>
        collection.Cards.FirstOrDefault(c => c.Name == card.Name && c.Rarity == card.Rarity);

    /// <summary>
    ///     Clears the favorite card if the collection has no copy of it left.
    /// </summary>
    private static void ClearFavoriteIfGone(UserCardCollection collection, Card tradedCard)
    {
        if (collection.FavoriteCard != null &&
            collection.FavoriteCard.Name == tradedCard.Name &&
            collection.FavoriteCard.Rarity == tradedCard.Rarity &&
            !collection.Cards.Any(c => c.Name == tradedCard.Name && c.Rarity == tradedCard.Rarity))
        {
            collection.FavoriteCard = null;
        }
    }
}
