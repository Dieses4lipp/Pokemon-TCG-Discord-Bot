using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.InventoryCommand;

/// <summary>
///     A class that handles the /innventory command showing the inventory of the executing user
/// </summary>
public static class InventoryCommandHandler
{
    /// <summary>
    ///     Handles a slash command to display and manage a user's card inventory within the Discord bot.
    /// </summary>
    /// <param name="command">
    ///     The slash command issued by the user, containing information about the command and the
    ///     user invoking it.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: true);

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is currently inactive.", ephemeral: true);
            return;
        }

        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(command.User.Id);

        if (collection.Cards == null || collection.Cards.Count == 0)
        {
            await command.FollowupAsync("You don't have any saved cards! Try `/pull` and save a card!");
            return;
        }

        var currentCard = collection.Cards[0];

        bool isFavorite = collection.FavoriteCard != null &&
                          collection.FavoriteCard.Name == currentCard.Name &&
                          collection.FavoriteCard.Rarity == currentCard.Rarity;

        var buttons = new ComponentBuilder()
                .WithButton("Previous", "inv_prev_card", ButtonStyle.Secondary)
                .WithButton("Next", "inv_next_card", ButtonStyle.Secondary)
                .WithButton("🗑️", "inv_delete_card", ButtonStyle.Danger)
                .WithButton("⭐", "inv_fav_card",
                    isFavorite ? ButtonStyle.Success : ButtonStyle.Primary,
                    disabled: isFavorite)
                .Build();

        Embed embed = CommandHandler.BuildCardEmbed(currentCard, 1, collection.Cards.Count);

        RestFollowupMessage message = await command.FollowupAsync(components: buttons, embed: embed);

        var session = new PackSession(message.Id, command.User.Id, collection.Cards);
        InventoryReactionHandler.ActiveSessions[message.Id] = session;
    }
}