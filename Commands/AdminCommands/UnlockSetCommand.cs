using Discord;
using Discord.Commands;
using DiscordBot.Core;

namespace DiscordBot.Commands.AdminCommands;

/// <summary>
///     Provides a command to unlock a locked set.
/// </summary>
public class UnlockSetCommand : ModuleBase<SocketCommandContext>
{
    /// <summary>
    ///     Unlocks a locked set if it exists.
    /// </summary>
    /// <param name="setId">
    ///     The ID of the set to unlock.
    /// </param>
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