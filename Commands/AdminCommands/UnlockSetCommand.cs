using Discord;
using Discord.Commands;
using DiscordBot.Core;
using System.Threading.Tasks;

namespace DiscordBot.Commands.AdminCommands
{
    public class UnlockSetCommand : ModuleBase<SocketCommandContext>
    {
        [Command("unlock")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task UnlockSetAsync(string setId)
        {
            if (!CommandHandler.BotActive)
            {
                await ReplyAsync("The bot is currently inactive and not responding to commands.");
                return;
            }
            if (CommandHandler.LockedSets.Contains(setId))
            {
                CommandHandler.LockedSets.Remove(setId);
                await ReplyAsync($"Set {setId} has been unlocked.");
            }
            else
            {
                await ReplyAsync($"Set {setId} is not locked.");
            }
        }
    }
}
