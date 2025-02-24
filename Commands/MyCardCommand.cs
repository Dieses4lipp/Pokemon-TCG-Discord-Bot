using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;
using DiscordBot.Services;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class MyCardsCommand : ModuleBase<SocketCommandContext>
    {
        [Command("mycards")]
        public async Task MyCardsAsync()
        {
            if (!CommandHandler.BotActive)
            {
                await ReplyAsync("The bot is currently inactive and not responding to commands.");
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
}
