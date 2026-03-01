using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands.SlashCommandHandlers;

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
        if (!ActiveSessions.TryGetValue(component.Message.Id, out var session))
            return;

        if (component.User.Id != session.UserId)
            return;

        var disabledButtons = new ComponentBuilder()
            .WithButton("Previous", "prev_card", ButtonStyle.Secondary)
            .WithButton("Next", "next_card", ButtonStyle.Secondary)
            .WithButton("Saved!", "save_card", ButtonStyle.Success, disabled: true)
            .Build();

        Card cardToSave = session.Cards[session.CurrentIndex];
        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);
        var cardIdentifier = $"{cardToSave.Name}_{cardToSave.Rarity}";

        if (session.SavedCardIdentifiers.Contains(cardIdentifier))
        {
            await component.UpdateAsync(m => m.Components = disabledButtons);
            return;
        }

        session.SavedCardIdentifiers.Add(cardIdentifier);

        if (collection.Cards.Count >= 10)
        {
            Console.WriteLine($"User {session.UserId} has reached the maximum card collection size.");
            return;
        }

        collection.Cards.Add(cardToSave);
        await CardStorage.SaveUserCardsAsync(collection);

        await component.UpdateAsync(m => m.Components = disabledButtons);
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
        if (!ActiveSessions.TryGetValue(component.Message.Id, out var session)) return;

        session.CurrentIndex = (session.CurrentIndex + direction + session.Cards.Count) % session.Cards.Count;

        var currentCard = session.Cards[session.CurrentIndex];
        bool isSaved = session.SavedCardIdentifiers.Contains($"{currentCard.Name}_{currentCard.Rarity}");

        var buttons = new ComponentBuilder()
            .WithButton("Previous", "prev_card", ButtonStyle.Secondary)
            .WithButton("Next", "next_card", ButtonStyle.Secondary)
            .WithButton(isSaved ? "Saved" : "Save", "save_card",
                        isSaved ? ButtonStyle.Success : ButtonStyle.Primary,
                        disabled: isSaved)
            .Build();

        var embed = CommandHandler.BuildCardEmbed(currentCard, session.CurrentIndex + 1, session.Cards.Count);

        await component.UpdateAsync(m =>
        {
            m.Embed = embed;
            m.Components = buttons;
        });
    }
}