using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;
using DiscordBot.Preconditions;

namespace DiscordBot.Modules.Inventory;

/// <summary>
///     The /inventory command and the buttons on the inventory view.
/// </summary>
[RequireBotActive]
public sealed class InventoryModule(SessionStore sessions, UserRepository users)
    : InteractionModuleBase<SocketInteractionContext>
{
    private SocketMessageComponent Component => (SocketMessageComponent)Context.Interaction;

    [SlashCommand("inventory", "Displays a your card collection.")]
    public async Task InventoryAsync()
    {
        await DeferAsync(ephemeral: true);

        UserCardCollection collection = await users.LoadUserCardsAsync(Context.User.Id);

        if (collection.Cards == null || collection.Cards.Count == 0)
        {
            await FollowupAsync("You don't have any saved cards! Try `/pull` and save a card!");
            return;
        }

        var currentCard = collection.Cards[0];

        bool isFavorite = IsFavorite(collection, currentCard);

        var buttons = new ComponentBuilder()
                .WithButton("Previous", "inv_prev_card", ButtonStyle.Secondary)
                .WithButton("Next", "inv_next_card", ButtonStyle.Secondary)
                .WithButton("💵", "inv_sell_card", ButtonStyle.Danger)
                .WithButton("⭐", "inv_fav_card",
                    isFavorite ? ButtonStyle.Success : ButtonStyle.Primary,
                    disabled: isFavorite)
                .Build();

        Embed embed = CardEmbeds.BuildCardEmbed(currentCard, 1, collection.Cards.Count);

        var attachments = CardEmbeds.BuildCardAttachments(currentCard);

        RestFollowupMessage message;
        try
        {
            message = attachments.Length > 0
                ? await Context.Interaction.FollowupWithFilesAsync(attachments, components: buttons, embed: embed)
                : await Context.Interaction.FollowupAsync(components: buttons, embed: embed);
        }
        finally
        {
            foreach (var attachment in attachments)
                attachment.Dispose();
        }

        sessions.Inventories[message.Id] = new PackSession(message.Id, Context.User.Id, collection.Cards);
    }

    [ComponentInteraction("inv_next_card")]
    public Task NextCardAsync() => MoveCardIndexAsync(1);

    [ComponentInteraction("inv_prev_card")]
    public Task PreviousCardAsync() => MoveCardIndexAsync(-1);

    /// <summary>
    ///     Marks the shown card as the user's favorite.
    /// </summary>
    [ComponentInteraction("inv_fav_card")]
    public async Task FavoriteCardAsync()
    {
        await DeferAsync(ephemeral: true);

        if (!sessions.Inventories.TryGetValue(Component.Message.Id, out var session)) return;

        UserCardCollection collection = await users.LoadUserCardsAsync(session.UserId);
        Card favoriteCard = session.Cards[session.CurrentIndex];

        collection.FavoriteCard = favoriteCard;
        await users.SaveUserCardsAsync(collection);

        await UpdateInventoryUIAsync(session, collection);
    }

    /// <summary>
    ///     Sells one copy of the shown card.
    /// </summary>
    [ComponentInteraction("inv_sell_card")]
    public async Task SellCardAsync()
    {
        await DeferAsync(ephemeral: true);

        if (!sessions.Inventories.TryGetValue(Component.Message.Id, out var session)) return;

        Card sessionCard = session.Cards[session.CurrentIndex];
        UserCardCollection collection = await users.LoadUserCardsAsync(session.UserId);

        // Sell exactly one unlocked copy from the freshly loaded collection; the session snapshot
        // may be stale (e.g. a copy went on an expedition after /inventory was opened)
        Card? cardToSell = collection.Cards.FirstOrDefault(c =>
            c.Name == sessionCard.Name && c.Rarity == sessionCard.Rarity && !c.IsLocked);

        if (cardToSell == null)
        {
            await FollowupAsync("🔒 No sellable copy left - this card is locked (on an expedition) or no longer in your inventory.", ephemeral: true);
            return;
        }

        // Calculate market value
        decimal marketPrice = GetCardMarketValue(cardToSell);

        if (marketPrice <= 0)
        {
            await FollowupAsync("❌ Unable to sell this card - no market price available.", ephemeral: true);
            return;
        }

        if (collection.Cards.Remove(cardToSell))
        {
            // Clear favorite if the last copy was sold
            if (IsFavorite(collection, cardToSell) &&
                !collection.Cards.Any(c => c.Name == cardToSell.Name && c.Rarity == cardToSell.Rarity))
            {
                collection.FavoriteCard = null;
            }

            // Add earnings to balance
            collection.Balance += marketPrice;
            await users.SaveUserCardsAsync(collection);

            // Show success message
            await FollowupAsync(
                $"✅ **Card sold!**\n\n" +
                $"**{cardToSell.Name}** ({cardToSell.Rarity})\n" +
                $"Earned: **{marketPrice:F2} EUR**\n" +
                $"New balance: **{collection.Balance:F2} EUR**",
                ephemeral: true);

            session.Cards.RemoveAt(session.CurrentIndex);

            if (session.Cards.Count == 0)
            {
                await Component.Message.DeleteAsync();
                sessions.Inventories.TryRemove(Component.Message.Id, out _);
                return;
            }

            if (session.CurrentIndex >= session.Cards.Count)
                session.CurrentIndex = session.Cards.Count - 1;

            await UpdateInventoryUIAsync(session, collection);
        }
    }

    /// <summary>
    ///     Moves the shown card forward or backward.
    /// </summary>
    /// <param name="direction">
    ///     Positive values move forward; negative values move backward.
    /// </param>
    private async Task MoveCardIndexAsync(int direction)
    {
        await DeferAsync(ephemeral: true);

        if (!sessions.Inventories.TryGetValue(Component.Message.Id, out var session)) return;

        session.CurrentIndex = (session.CurrentIndex + direction + session.Cards.Count) % session.Cards.Count;
        UserCardCollection collection = await users.LoadUserCardsAsync(session.UserId);

        await UpdateInventoryUIAsync(session, collection);
    }

    /// <summary>
    ///     Gets the market value of a card, preferring Cardmarket prices.
    /// </summary>
    /// <param name="card">
    ///     The card to get the market value for.
    /// </param>
    /// <returns>
    ///     The market value of the card in whole cents, or 0 if no price is available.
    /// </returns>
    private static decimal GetCardMarketValue(Card card)
    {
        double? price =
            card.Pricing?.Cardmarket?.Avg ??
            card.Pricing?.TcgPlayer?.Market ??
            card.Pricing?.TcgPlayer?.Low;

        return price is { } value ? Money.FromDouble(value) : 0m;
    }


    /// <summary>
    ///     Determines whether the specified card matches the favorite card in the given user card collection.
    /// </summary>
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
    ///     Shows the session's current card and the matching buttons.
    /// </summary>
    /// <param name="session">
    ///     The session containing the user's card inventory and the index of the currently
    ///     displayed card.
    /// </param>
    /// <param name="collection">
    ///     The collection of user cards, used to determine favorite status for the current card.
    /// </param>
    private async Task UpdateInventoryUIAsync(PackSession session, UserCardCollection collection)
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

        var embed = CardEmbeds.BuildCardEmbed(currentCard, session.CurrentIndex + 1, session.Cards.Count);
        var attachments = CardEmbeds.BuildCardAttachments(currentCard);

        try
        {
            await ModifyOriginalResponseAsync(m =>
            {
                m.Embed = embed;
                m.Components = buttons;
                m.Attachments = new Optional<IEnumerable<FileAttachment>>(attachments);
            });
        }
        finally
        {
            foreach (var attachment in attachments)
                attachment.Dispose();
        }
    }
}
