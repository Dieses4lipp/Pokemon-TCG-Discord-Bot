using Discord;
using Discord.Commands;
using DiscordBot.Core;
using System.Threading.Tasks;

namespace DiscordBot.Commands.AdminCommands
{
    public class LockSetCommand : ModuleBase<SocketCommandContext>
    {
        [Command("lock")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task LockSetAsync(string setId)
        {
            if (!CommandHandler.BotActive)
            {
                await ReplyAsync("The bot is currently inactive and not responding to commands.");
                return;
            }
            if (CommandHandler.LockedSets.Contains(setId))
            {
                await ReplyAsync($"Set {setId} is already locked.");
            }
            else
            {
                CommandHandler.LockedSets.Add(setId);
                await ReplyAsync($"Set {setId} has been locked.");
            }
        }
    }
}
