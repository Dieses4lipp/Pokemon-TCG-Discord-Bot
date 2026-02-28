using Discord;
using Discord.Commands;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands;

/// <summary>
///     Provides a command to display a user's saved Pokémon cards.
/// </summary>
public class MyCardsCommand : ModuleBase<SocketCommandContext>
{
    /// <summary>
    ///     Displays a user's saved Pokémon cards and allows navigation through them.
    /// </summary>
    [Command("mycards")]
    public async Task MyCardsAsync()
    {
        if (!CommandHandler.BotActive)
        {
            await ReplyAsync("💤 Bot is currently inactive. Use '!turnon' to activate the bot.");
            return;
        }
        // Load the user's saved card collection from JSON.
        UserCardCollection collection = await CardStorage.LoadUserCardsAsync(Context.User.Id);
        if (collection.Cards == null || collection.Cards.Count == 0)
        {
            await ReplyAsync("You don't have any saved cards! Try !pullpack and save a card!");
            return;
        }

        // Build an embed using the first saved card.
        Embed embed = CommandHandler.BuildCardEmbed(collection.Cards[0], 1, collection.Cards.Count);
        IUserMessage? message = await ReplyAsync(embed: embed);

        // Create a session for navigating through the user's cards.
        var session = new PackSession(message.Id, Context.User.Id, collection.Cards);
        CommandHandler.ActiveSessions[message.Id] = session;

        // Add reactions for navigation.
        await message.AddReactionAsync(new Emoji("◀️"));
        await message.AddReactionAsync(new Emoji("▶️"));
        await message.AddReactionAsync(new Emoji("🗑️"));
        await message.AddReactionAsync(new Emoji("⭐"));
    }
}