using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.PullCommand;

/// <summary>
///     A class that handles reactions on bot messages.
/// </summary>
public static class PullReactionHandler
{
    /// <summary>
    ///     Stores active card navigation sessions mapped by message ID.
    /// </summary>
    public static readonly Dictionary<ulong, PackSession> ActiveSessions = [];

    /// <summary>
    ///     HHandles the "Save" button click for a card in the pack opening session.
    /// </summary>
    /// <param name="component">
    ///     The component interaction triggered by the user clicking the "Save" button.
    /// </param>
    public static async Task HandleSaveCardAsync(SocketMessageComponent component)
    {
        // Defer the response to give more time to process
        await component.DeferAsync(ephemeral: true);

        if (!ActiveSessions.TryGetValue(component.Message.Id, out var session))
            return;
        if (component.User.Id != session.UserId)
            return;

        var disabledButtons = new ComponentBuilder()
            .WithButton("Previous", "prev_card", ButtonStyle.Secondary)
            .WithButton("Next", "next_card", ButtonStyle.Secondary)
            .WithButton("💾", "save_card", ButtonStyle.Success, disabled: true)
            .WithButton("💵", "sell_pack", ButtonStyle.Danger)
            .Build();

        Card cardToSave = session.Cards[session.CurrentIndex];
        var cardIdentifier = $"{cardToSave.Name}_{cardToSave.Rarity}";

        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);

        if (session.SavedCardIdentifiers.Contains(cardIdentifier))
        {
            await component.ModifyOriginalResponseAsync(m => m.Components = disabledButtons);
            return;
        }

        session.SavedCardIdentifiers.Add(cardIdentifier);

        if (collection.Cards.Count >= 10)
        {
            await component.FollowupAsync("❌ Your inventory is full (Max 10 cards)! Delete some cards in `/inventory` first.", ephemeral: true);
            return;
        }

        collection.Cards.Add(cardToSave);
        await CardStorage.SaveUserCardsAsync(collection);

        await component.ModifyOriginalResponseAsync(m => m.Components = disabledButtons);
    }

    /// <summary>
    ///     Handles the "Previous" and "Next" button clicks for navigating through cards in the pack
    ///     opening session.
    /// </summary>
    /// <param name="component">
    ///     The component interaction triggered by the user clicking either the "Previous" or "Next" button.
    /// </param>
    /// <param name="direction">
    ///     The direction to move in the card list: -1 for "Previous" and +1 for "Next".
    /// </param>
    public static async Task HandleMoveCardIndex(SocketMessageComponent component, int direction)
    {
        // Defer the response to give more time to process
        await component.DeferAsync(ephemeral: true);

        if (!ActiveSessions.TryGetValue(component.Message.Id, out var session)) return;

        session.CurrentIndex = (session.CurrentIndex + direction + session.Cards.Count) % session.Cards.Count;

        var currentCard = session.Cards[session.CurrentIndex];
        bool isSaved = session.SavedCardIdentifiers.Contains($"{currentCard.Name}_{currentCard.Rarity}");

        var buttons = new ComponentBuilder()
            .WithButton("Previous", "prev_card", ButtonStyle.Secondary)
            .WithButton("Next", "next_card", ButtonStyle.Secondary)
            .WithButton("💾", "save_card",
                        isSaved ? ButtonStyle.Success : ButtonStyle.Primary,
                        disabled: isSaved)
            .WithButton("💵", "sell_pack", ButtonStyle.Danger)
            .Build();

        var embed = CommandHandler.BuildCardEmbed(currentCard, session.CurrentIndex + 1, session.Cards.Count);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = buttons;
        });
    }

    /// <summary>
    ///     Handles the "Open Pack" button click to reveal the cards.
    /// </summary>
    /// <param name="component">
    ///     The component interaction triggered by the user clicking the "Open Pack" button.
    /// </param>
    public static async Task HandleOpenPackAsync(SocketMessageComponent component)
    {
        // Defer the response to give more time to process
        await component.DeferAsync(ephemeral: true);

        if (!ActiveSessions.TryGetValue(component.Message.Id, out var session))
            return;
        if (component.User.Id != session.UserId)
            return;

        var embed = CommandHandler.BuildCardEmbed(session.Cards[0], 1, session.Cards.Count);

        var buttons = new ComponentBuilder()
            .WithButton("Previous", "prev_card", ButtonStyle.Secondary)
            .WithButton("Next", "next_card", ButtonStyle.Secondary)
            .WithButton("💾", "save_card", ButtonStyle.Primary)
            .WithButton("💵", "sell_pack", ButtonStyle.Danger)
            .Build();

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = buttons;
            m.Attachments = new Optional<IEnumerable<FileAttachment>>(Array.Empty<FileAttachment>());
        });
    }

    /// <summary>
    ///     Handles the "Sell Pack" button click to bulk sell unsaved cards left in the pack.
    /// </summary>
    /// <param name="component">
    ///     The component interaction triggered by the user.
    /// </param>
    public static async Task HandleSellPackAsync(SocketMessageComponent component)
    {
        await component.DeferAsync(ephemeral: true);

        if (!ActiveSessions.TryGetValue(component.Message.Id, out var session))
            return;
        if (component.User.Id != session.UserId)
            return;

        double totalEarned = 0;
        int cardsSold = 0;

        foreach (var card in session.Cards)
        {
            var identifier = $"{card.Name}_{card.Rarity}";
            if (!session.SavedCardIdentifiers.Contains(identifier))
            {
                double marketPrice = 
                    card.Pricing?.TcgPlayer?.Market ?? 
                    card.Pricing?.TcgPlayer?.Low ?? 
                    card.Pricing?.Cardmarket?.Avg ?? 
                    0.50;

                totalEarned += marketPrice;
                cardsSold++;
            }
        }

        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);
        collection.Balance += totalEarned;
        await CardStorage.SaveUserCardsAsync(collection);

        Console.WriteLine($"[Sell Pack] User {component.User.Username} sold {cardsSold} cards for a total of {totalEarned:F2}! New Balance: {collection.Balance:F2}");

        var buttons = new ComponentBuilder()
            .WithButton("Pack Sold", "disabled_sell", ButtonStyle.Secondary, disabled: true)
            .Build();

        ActiveSessions.Remove(component.Message.Id);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Components = buttons;
        });

        await component.FollowupAsync($"💵 You sold {cardsSold} unsaved cards for **${totalEarned:F2}**!\nYour new balance is **${collection.Balance:F2}**.", ephemeral: true);
    }
}