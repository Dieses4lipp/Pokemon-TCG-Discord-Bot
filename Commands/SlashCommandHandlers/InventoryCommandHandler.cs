using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands.SlashCommandHandlers;

public static class InventoryCommandHandler
{
    public static async Task Handle(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);
        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is currently inactive. Use '/turnon' to activate the bot.", ephemeral: true);
            return;
        }

        // Load the user's saved card collection from JSON.
        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(command.User.Id);

        if (collection.Cards == null || collection.Cards.Count == 0)
        {
            await command.FollowupAsync("You don't have any saved cards! Try /pull and save a card!");
            return;
        }

        // Build an embed using the first saved card.
        Embed embed = CommandHandler.BuildCardEmbed(collection.Cards[0], 1, collection.Cards.Count);
        RestFollowupMessage? message = await command.FollowupAsync(embed: embed);

        // Create a session for navigating through the user's cards.
        var session = new PackSession(message.Id, command.User.Id, collection.Cards);
        InventoryReactionHandler.ActiveSessions[message.Id] = session;

        var currentCard = session.Cards[session.CurrentIndex];

        bool isFavorite = collection.FavoriteCard == currentCard;

        var buttons = new ComponentBuilder()
                .WithButton("Previous", "inv_prev_card", ButtonStyle.Secondary)
                .WithButton("Next", "inv_next_card", ButtonStyle.Secondary)
                .WithButton("🗑️", "inv_delete_card", ButtonStyle.Secondary)
                .WithButton("⭐", "inv_fav_card",
                    isFavorite ? ButtonStyle.Success : ButtonStyle.Primary,
                    disabled: isFavorite)
                .Build();

        await message.ModifyAsync(msg => msg.Components = buttons);
    }
}