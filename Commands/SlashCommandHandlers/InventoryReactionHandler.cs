using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;
using System.Threading.Channels;
using static System.Collections.Specialized.BitVector32;

namespace DiscordBot.Commands.SlashCommandHandlers;

public static class InventoryReactionHandler
{
    /// <summary>
    ///     Stores active card navigation sessions mapped by message ID.
    /// </summary>
    public static readonly Dictionary<ulong, PackSession> ActiveSessions = [];

    public static async Task HandleFavoriteCard(SocketMessageComponent component)
    {
        await component.DeferAsync();

        if (!ActiveSessions.TryGetValue(component.Message.Id, out PackSession? session))
            return;

        Card favoriteCard = session.Cards[session.CurrentIndex];

        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);

        collection.FavoriteCard = favoriteCard;

        await CardStorage.SaveUserCardsAsync(collection);

        var buttons = new ComponentBuilder()
                .WithButton("Previous", "inv_prev_card", ButtonStyle.Secondary)
                .WithButton("Next", "inv_next_card", ButtonStyle.Secondary)
                .WithButton("🗑️", "inv_delete_card", ButtonStyle.Secondary)
                .WithButton("⭐", "inv_fav_card", ButtonStyle.Success, disabled: true)
                .Build();

        await component.ModifyOriginalResponseAsync(m => m.Components = buttons);
    }

    public static async Task HandleDeleteCard(SocketMessageComponent component)
    {
        await component.DeferAsync();

        if (!ActiveSessions.TryGetValue(component.Message.Id, out PackSession? session))
            return;

        // Delete the current card from the collection.
        Card cardToDelete = session.Cards[session.CurrentIndex];
        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);

        int removedCount = collection.Cards.RemoveAll(c => c.Name == cardToDelete.Name && c.Rarity == cardToDelete.Rarity);

        if (removedCount > 0)
        {
            // If the deleted card was the favorite, clear it
            if (collection.FavoriteCard != null &&
                collection.FavoriteCard.Name == cardToDelete.Name &&
                collection.FavoriteCard.Rarity == cardToDelete.Rarity)
            {
                collection.FavoriteCard = null;
            }

            await CardStorage.SaveUserCardsAsync(collection);

            // Remove from the active session list
            session.Cards.RemoveAt(session.CurrentIndex);

            // Scenario A: No cards left
            if (session.Cards.Count == 0)
            {
                await component.Message.DeleteAsync();
                ActiveSessions.Remove(component.Message.Id);
                await component.FollowupAsync("Your collection is now empty.", ephemeral: true);
                return;
            }

            // Scenario B: Cards still exist => adjust index
            if (session.CurrentIndex >= session.Cards.Count)
            {
                session.CurrentIndex = session.Cards.Count - 1;
            }

            // Rebuild the UI for the next available card
            var nextCard = session.Cards[session.CurrentIndex];
            var embed = CommandHandler.BuildCardEmbed(nextCard, session.CurrentIndex + 1, session.Cards.Count);

            // Check if the NEW card shown is the favorite
            bool isFavorite = collection.FavoriteCard == nextCard;

            var buttons = new ComponentBuilder()
                .WithButton("Previous", "inv_prev_card", ButtonStyle.Secondary)
                .WithButton("Next", "inv_next_card", ButtonStyle.Secondary)
                .WithButton("🗑️", "inv_delete_card", ButtonStyle.Secondary)
                .WithButton("⭐", "inv_fav_card",
                    isFavorite ? ButtonStyle.Success : ButtonStyle.Primary,
                    disabled: isFavorite)
                .Build();

            await component.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = embed;
                m.Components = buttons;
            });

            await component.FollowupAsync($"Card '{cardToDelete.Name}' has been removed!", ephemeral: true);
        }
        else
        {
            await component.FollowupAsync("Card not found in your collection.", ephemeral: true);
        }
    }

    public static async Task HandleMoveCardIndex(SocketMessageComponent component, int direction)
    {
        await component.DeferAsync();

        if (!ActiveSessions.TryGetValue(component.Message.Id, out var session)) return;

        session.CurrentIndex = (session.CurrentIndex + direction + session.Cards.Count) % session.Cards.Count;

        var currentCard = session.Cards[session.CurrentIndex];

        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);

        bool isFavorite = collection.FavoriteCard == currentCard;

        var buttons = new ComponentBuilder()
            .WithButton("Previous", "inv_prev_card", ButtonStyle.Secondary)
            .WithButton("Next", "inv_next_card", ButtonStyle.Secondary)
            .WithButton("🗑️", "inv_delete_card", ButtonStyle.Secondary)
            .WithButton("⭐", "inv_fav_card",
                isFavorite ? ButtonStyle.Success : ButtonStyle.Primary,
                disabled: isFavorite)
            .Build();

        var embed = CommandHandler.BuildCardEmbed(currentCard, session.CurrentIndex + 1, session.Cards.Count);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = buttons;
        });
    }
}