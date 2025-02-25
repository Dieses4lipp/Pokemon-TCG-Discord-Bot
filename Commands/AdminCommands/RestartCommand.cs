using Discord;
using Discord.Commands;
using DiscordBot.Core;
using System.Threading.Tasks;

namespace DiscordBot.Commands.AdminCommands
{
    public class RestartCommand : ModuleBase<SocketCommandContext>
    {
        [Command("restart")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task RestartAsync()
        {
            if (!CommandHandler.BotActive)
            {
                await ReplyAsync("Bot is currently inactive. Use !turnon to activate the bot.");
                return;
            }
            await ReplyAsync("Restarting...");
            Program.RestartBot();
            await ReplyAsync("Bot has been restarted.");
        }
    }
}
