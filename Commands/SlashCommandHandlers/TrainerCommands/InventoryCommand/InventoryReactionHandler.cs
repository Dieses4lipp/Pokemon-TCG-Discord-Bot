using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.InventoryCommand;

/// <summary>
///     A class that contains handlers for button presses on the /inventory command embed
/// </summary>
public static class InventoryReactionHandler
{
    /// <summary>
    ///     The active PackSession
    /// </summary>
    public static readonly Dictionary<ulong, PackSession> ActiveSessions = [];

    /// <summary>
    ///     Handles a user's selection of a favorite card from a message component interaction.
    /// </summary>
    /// <param name="component">
    ///     The message component that triggered the interaction, representing the user's selection.
    /// </param>
    public static async Task HandleFavoriteCard(SocketMessageComponent component)
    {
        // Defer the response to give more time to process
        await component.DeferAsync(ephemeral: true);

        if (!ActiveSessions.TryGetValue(component.Message.Id, out var session)) return;

        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);
        Card favoriteCard = session.Cards[session.CurrentIndex];

        collection.FavoriteCard = favoriteCard;
        await CardStorage.SaveUserCardsAsync(collection);

        await UpdateInventoryUI(component, session, collection);
    }

    /// <summary>
    ///     Handles the selling of a card from the user's collection in response to a component interaction.
    /// </summary>
    /// <param name="component">
    ///     The component representing the user's interaction that initiates the card selling process.
    /// </param>
    public static async Task HandleSellCard(SocketMessageComponent component)
    {
        // Defer the response to give more time to process
        await component.DeferAsync(ephemeral: true);

        if (!ActiveSessions.TryGetValue(component.Message.Id, out var session)) return;

        Card cardToSell = session.Cards[session.CurrentIndex];
        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);

        if (cardToSell.IsLocked)
        {
            await component.FollowupAsync("🔒 This card is locked (on an expedition) and can't be sold.", ephemeral: true);
            return;
        }

        // Calculate market value
        double marketPrice = GetCardMarketValue(cardToSell);

        if (marketPrice <= 0)
        {
            await component.FollowupAsync("❌ Unable to sell this card - no market price available.", ephemeral: true);
            return;
        }

        int removedCount = collection.Cards.RemoveAll(c => c.Name == cardToSell.Name && c.Rarity == cardToSell.Rarity);

        if (removedCount > 0)
        {
            // Clear favorite if sold
            if (IsFavorite(collection, cardToSell))
            {
                collection.FavoriteCard = null;
            }

            // Add earnings to balance
            collection.Balance += marketPrice;
            await CardStorage.SaveUserCardsAsync(collection);

            // Show success message
            await component.FollowupAsync(
                $"✅ **Card sold!**\n\n" +
                $"**{cardToSell.Name}** ({cardToSell.Rarity})\n" +
                $"Earned: **{marketPrice:F2} EUR**\n" +
                $"New balance: **{collection.Balance:F2} EUR**",
                ephemeral: true);

            session.Cards.RemoveAt(session.CurrentIndex);

            if (session.Cards.Count == 0)
            {
                await component.Message.DeleteAsync();
                ActiveSessions.Remove(component.Message.Id);
                return;
            }

            if (session.CurrentIndex >= session.Cards.Count)
                session.CurrentIndex = session.Cards.Count - 1;

            await UpdateInventoryUI(component, session, collection);
        }
    }

    /// <summary>
    ///     Handles the movement of the card index within the user's active inventory session in
    ///     response to a message component interaction.
    /// </summary>
    /// <param name="component">
    ///     The message component that triggered the card index movement. Must not be null.
    /// </param>
    /// <param name="direction">
    ///     An integer specifying the direction to move the card index. Positive values move
    ///     forward; negative values move backward.
    /// </param>
    public static async Task HandleMoveCardIndex(SocketMessageComponent component, int direction)
    {
        // Defer the response to give more time to process
        await component.DeferAsync(ephemeral: true);

        if (!ActiveSessions.TryGetValue(component.Message.Id, out var session)) return;

        session.CurrentIndex = (session.CurrentIndex + direction + session.Cards.Count) % session.Cards.Count;
        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);

        await UpdateInventoryUI(component, session, collection);
    }

    /// <summary>
    ///     Gets the market value of a card, preferring Cardmarket prices.
    /// </summary>
    /// <param name="card">
    ///     The card to get the market value for.
    /// </param>
    /// <returns>
    ///     The market value of the card, or 0.0 if no price is available.
    /// </returns>
    private static double GetCardMarketValue(Card card)
    {
        if (card.Pricing == null)
            return 0.0;

        // Prefer Cardmarket average price
        if (card.Pricing.Cardmarket?.Avg.HasValue == true)
            return card.Pricing.Cardmarket.Avg.Value;

        // Fallback to TCGPlayer market price
        if (card.Pricing.TcgPlayer?.Market.HasValue == true)
            return card.Pricing.TcgPlayer.Market.Value;

        // Fallback to TCGPlayer low price
        if (card.Pricing.TcgPlayer?.Low.HasValue == true)
            return card.Pricing.TcgPlayer.Low.Value;

        return 0.0;
    }

    /// <summary>
    ///     Determines whether the specified card matches the favorite card in the given user card collection.
    /// </summary>
    /// <param name="collection">
    ///     The user card collection to search for a favorite card. Cannot be null.
    /// </param>
    /// <param name="currentCard">
    ///     The card to compare against the favorite card in the collection. Cannot be null.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if the current card has the same name and rarity as the favorite
    ///     card in the collection; otherwise, false.
    /// </returns>
    private static bool IsFavorite(UserCardCollection collection, Card currentCard)
    {
        return collection.FavoriteCard != null &&
               collection.FavoriteCard.Name == currentCard.Name &&
               collection.FavoriteCard.Rarity == currentCard.Rarity;
    }

    /// <summary>
    ///     Updates the inventory user interface to display the current card's details and available actions.
    /// </summary>
    /// <param name="component">
    ///     The message component that triggered the inventory update interaction.
    /// </param>
    /// <param name="session">
    ///     The session containing the user's card inventory and the index of the currently
    ///     displayed card.
    /// </param>
    /// <param name="collection">
    ///     The collection of user cards, used to determine favorite status for the current card.
    /// </param>
    private static async Task UpdateInventoryUI(SocketMessageComponent component, PackSession session, UserCardCollection collection)
    {
        var currentCard = session.Cards[session.CurrentIndex];
        bool isFav = IsFavorite(collection, currentCard);

        var buttons = new ComponentBuilder()
            .WithButton("Previous", "inv_prev_card", ButtonStyle.Secondary)
            .WithButton("Next", "inv_next_card", ButtonStyle.Secondary)
            .WithButton("💰 Sell", "inv_sell_card", ButtonStyle.Danger)
            .WithButton("⭐", "inv_fav_card",
                isFav ? ButtonStyle.Success : ButtonStyle.Primary,
                disabled: isFav)
            .Build();

        var embed = CommandHandler.BuildCardEmbed(currentCard, session.CurrentIndex + 1, session.Cards.Count);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = buttons;
        });
    }
}