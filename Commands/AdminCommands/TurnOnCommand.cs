using Discord;
using Discord.Commands;
using DiscordBot.Core;
using System.Threading.Tasks;

namespace DiscordBot.Commands.AdminCommands
{
    public class TurnOnCommand : ModuleBase<SocketCommandContext>
    {
        [Command("turnon")]
        [RequireBotPermission(GuildPermission.Administrator)]
        public async Task TurnOnAsync()
        {
            CommandHandler.BotActive = true;
            await ReplyAsync("Bot is now active.");
        }
    }
}
