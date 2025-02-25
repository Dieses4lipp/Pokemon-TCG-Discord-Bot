using Discord;
using Discord.Commands;
using DiscordBot.Core;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class HelpCommand : ModuleBase<SocketCommandContext>
    {
        [Command("help")]
        public async Task HelpAsync()
        {
            if (!CommandHandler.BotActive)
            {
                await ReplyAsync("The bot is currently inactive and not responding to commands.");
                return;
            }
            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle("Commands")
                .WithDescription("The following commands are available:")
                .AddField("!pullpack [set ID]", "Pulls a pack of 9 random Pokémon cards.")
                .AddField("!mycards", "Displays your saved Pokémon cards.")
                .AddField("!sets", "Displays a list of Pokémon card sets.")
                .AddField("!trade [user] [card index]", "Initiates a trade with another user.")
                .AddField("!confirmtrade", "Confirms a trade with another user.")
                .AddField("!canceltrade", "Cancels a trade with another user.")
                .AddField("!profile [user]", "Displays a user's profile.")
                .AddField("!unlock [set ID]", "Unlocks a specific set to allow it to be pulled again. (Admin only)")
                .AddField("!lock [set ID]", "Locks a specific set to prevent it from being pulled. (Admin only)")
                .AddField("!restart", "Restarts the bot. (Admin only)")
                .AddField("!turnon", "Turns on the bot to allow it to respond to commands. (Admin only)")
                .AddField("!turnoff", "Turns off the bot to prevent it from responding to commands. (Admin only)")
                .AddField("!stats", "Displays bot statistics. (Admin only)")
                .WithColor(Color.Blue);
            await ReplyAsync(embed: embed.Build());
        }
    }
}
