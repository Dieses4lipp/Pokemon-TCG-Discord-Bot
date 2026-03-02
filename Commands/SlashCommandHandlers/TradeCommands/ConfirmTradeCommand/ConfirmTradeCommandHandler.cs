using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands.SlashCommandHandlers.TradeCommands.ConfirmTradeCommand;

/// <summary>
///     A class that contains the Handler for the /confirmtrade command
/// </summary>
public static class ConfirmTradeCommandHandler
{
    /// <summary>
    ///     Handles the /confirmtrade command
    /// </summary>
    /// <param name="command">
    ///     The slash command issued by the user, containing information about the command and the
    ///     user invoking it.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: false);

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is inactive.", ephemeral: true);
            return;
        }

        // Validate session existence
        if (!CommandHandler.ActiveTrades.TryGetValue(command.User.Id, out TradeSession? session))
        {
            await command.FollowupAsync("❌ You don't have any pending trades.", ephemeral: true);
            return;
        }

        // Remove the entries so no one else can trigger this logic again
        CommandHandler.ActiveTrades.Remove(session.SenderId);
        CommandHandler.ActiveTrades.Remove(session.ReceiverId);

        // Remove the other person's reference to the session as well
        ulong partnerId = (command.User.Id == session.SenderId) ? session.ReceiverId : session.SenderId;

        // Ensure the person confirming is the intended receiver
        if (session.ReceiverId != command.User.Id)
        {
            await command.FollowupAsync("⚠️ Only the person receiving the trade can confirm it.", ephemeral: true);
            return;
        }

        // Load both collections
        var senderCol = await CardStorage.LoadUserCardsAsync(session.SenderId);
        var receiverCol = await CardStorage.LoadUserCardsAsync(session.ReceiverId);

        // Find the cards in their respective inventories We use value-based matching (Name + Rarity)
        var cardFromSender = senderCol.Cards.FirstOrDefault(c =>
            c.Name == session.CardToTrade.Name && c.Rarity == session.CardToTrade.Rarity);

        var cardFromReceiver = receiverCol.Cards.FirstOrDefault(c =>
            c.Name == session.CardToReceive.Name && c.Rarity == session.CardToReceive.Rarity);

        // Final Ownership Check (Prevent "Double Spend")
        if (cardFromSender == null || cardFromReceiver == null)
        {
            await command.FollowupAsync("❌ One of the cards is no longer in the owner's inventory. Trade cancelled.");
            return;
        }

        // PERFORM THE SWAP
        senderCol.Cards.Remove(cardFromSender);
        receiverCol.Cards.Remove(cardFromReceiver);

        senderCol.Cards.Add(cardFromReceiver);
        receiverCol.Cards.Add(cardFromSender);

        // Update stats
        senderCol.CardsTraded++;
        receiverCol.CardsTraded++;

        // Clean up Favorites (If they traded away their favorite card)
        CheckAndClearFavorite(senderCol, cardFromSender);
        CheckAndClearFavorite(receiverCol, cardFromReceiver);

        await CardStorage.SaveUserCardsAsync(senderCol);
        await CardStorage.SaveUserCardsAsync(receiverCol);

        var embed = new EmbedBuilder()
            .WithTitle("✅ Trade Successful!")
            .WithDescription($"{MentionUtils.MentionUser(session.SenderId)} and {MentionUtils.MentionUser(session.ReceiverId)} have swapped cards.")
            .AddField("New Additions",
                $"{MentionUtils.MentionUser(session.SenderId)} received `{cardFromReceiver.Name}`\n" +
                $"{MentionUtils.MentionUser(session.ReceiverId)} received `{cardFromSender.Name}`")
            .WithColor(Color.Green)
            .WithCurrentTimestamp()
            .Build();

        await command.FollowupAsync(embed: embed);
    }

    /// <summary>
    ///     Clears favorite card if it was traded
    /// </summary>
    /// <param name="col">
    ///     The collection of the user
    /// </param>
    /// <param name="tradedCard">
    ///     The card traded
    /// </param>
    private static void CheckAndClearFavorite(UserCardCollection col, Card tradedCard)
    {
        if (col.FavoriteCard != null &&
            col.FavoriteCard.Name == tradedCard.Name &&
            col.FavoriteCard.Rarity == tradedCard.Rarity)
        {
            col.FavoriteCard = null;
        }
    }
}