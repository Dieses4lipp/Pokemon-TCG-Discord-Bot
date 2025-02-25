using Discord;
using Discord.Commands;
using DiscordBot.Core;
using System.Threading.Tasks;

namespace DiscordBot.Commands.AdminCommands
{
    public class TurnOffCommand : ModuleBase<SocketCommandContext>
    {
        [Command("turnoff")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task TurnOffAsync()
        {
            CommandHandler.BotActive = false;
            await ReplyAsync("Bot is now inactive.");
        }
    }
}
